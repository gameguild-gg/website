using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using GameGuild.Learning.Assessments;
using GameGuild.Learning.Assessments.Grading.Authoring;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Runtime;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class ContextualAccessAndGradingRealSessionPostgreSqlHttpTests(
    ApiPostgreSqlFixture fixture)
{
    private const string AssessmentsRoute = "/v1/assessments";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new ReviewMethodsJsonConverter(), new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task RealMemberSessions_EnforceContextualAccessAcrossOfficialGradingFlows()
    {
        var marker = Guid.NewGuid().ToString("N");
        await EnsureDefaultTenantAsync(marker);
        var owner = await SignUpAsync("owner", marker);
        var learnerA = await SignUpAsync("learner-a", marker);
        var learnerB = await SignUpAsync("learner-b", marker);
        var outsider = await SignUpAsync("outsider", marker);

        learnerA.TenantId.Should().Be(owner.TenantId);
        learnerB.TenantId.Should().Be(owner.TenantId);
        outsider.TenantId.Should().Be(owner.TenantId);

        using var ownerClient = CreateAuthenticatedClient(owner);
        using var learnerAClient = CreateAuthenticatedClient(learnerA);
        using var learnerBClient = CreateAuthenticatedClient(learnerB);
        using var outsiderClient = CreateAuthenticatedClient(outsider);

        var course = await CreateCourseAsync(ownerClient, $"grading-{marker}");
        var ownerAccess = await GetAccessAsync(ownerClient, course.Id);
        ownerAccess.IsOwner.Should().BeTrue();
        ownerAccess.CanEdit.Should().BeTrue();
        ownerAccess.CanPublish.Should().BeTrue();
        ownerAccess.CanReviewAsStaff.Should().BeTrue();
        ownerAccess.CanLearn.Should().BeFalse();

        await EnrollAsync(ownerClient, course.Id, learnerA.UserId);
        await EnrollAsync(ownerClient, course.Id, learnerB.UserId);

        var learnerAccess = await GetAccessAsync(learnerAClient, course.Id);
        learnerAccess.HasActiveEnrollment.Should().BeTrue();
        learnerAccess.CanLearn.Should().BeTrue();
        learnerAccess.CanEdit.Should().BeFalse();
        learnerAccess.CanPublish.Should().BeFalse();
        learnerAccess.CanReviewAsStaff.Should().BeFalse();
        learnerAccess.CanAccessWorkspace.Should().BeFalse();

        var outsiderAccess = await GetAccessAsync(outsiderClient, course.Id);
        outsiderAccess.CanLearn.Should().BeFalse();
        outsiderAccess.CanAccessWorkspace.Should().BeFalse();

        var automated = await CreatePublishedQuizAsync(
            ownerClient,
            course.Id,
            $"automated-{marker}",
            ReviewMethods.AutomatedReview);
        var reviewed = await CreatePublishedQuizAsync(
            ownerClient,
            course.Id,
            $"reviewed-{marker}",
            ReviewMethods.AutomatedReview | ReviewMethods.InstructorReview);

        await PostAsync<ProgramDto>(ownerClient, $"/v1/courses/{course.Id}:publish", new { });

        await AssertOfficialFlowAsync(
            automated,
            learnerAClient,
            learnerBClient,
            outsiderClient,
            ownerClient,
            requiresInstructorReview: false,
            expectedScoreUnits: 200);
        await AssertOfficialFlowAsync(
            reviewed,
            learnerAClient,
            learnerBClient,
            outsiderClient,
            ownerClient,
            requiresInstructorReview: true,
            expectedScoreUnits: 175);

        using var reopenedLearnerClient = await SignInAsync(learnerA);
        var reopenedAccess = await GetAccessAsync(reopenedLearnerClient, course.Id);
        reopenedAccess.CanLearn.Should().BeTrue();
        reopenedAccess.CanReviewAsStaff.Should().BeFalse();

        var learnerOwnedCourse = await CreateCourseAsync(
            reopenedLearnerClient,
            $"learner-owned-{marker}");
        var learnerAsOwner = await GetAccessAsync(reopenedLearnerClient, learnerOwnedCourse.Id);
        learnerAsOwner.IsOwner.Should().BeTrue();
        learnerAsOwner.CanEdit.Should().BeTrue();
        learnerAsOwner.CanReviewAsStaff.Should().BeTrue();
        (await GetAccessAsync(reopenedLearnerClient, course.Id)).IsOwner.Should().BeFalse();

        using var wrongTenantClient = CreateAuthenticatedClient(
            owner with { TenantId = Guid.NewGuid() });
        using var crossTenantResponse = await wrongTenantClient.GetAsync(
            $"/v1/courses/{course.Id}/access/capabilities");
        crossTenantResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);

        using var removal = await ownerClient.DeleteAsync(
            $"/v1/courses/{course.Id}/users/{learnerB.UserId}");
        await EnsureSuccessAsync(removal);
        var revokedAccess = await GetAccessAsync(learnerBClient, course.Id);
        revokedAccess.HasActiveEnrollment.Should().BeFalse();
        revokedAccess.CanLearn.Should().BeFalse();
    }

    private async Task EnsureDefaultTenantAsync(string marker)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (await context.Set<Tenant>().AnyAsync(tenant => tenant.IsDefault))
        {
            return;
        }

        context.Set<Tenant>().Add(new Tenant
        {
            Id = Guid.NewGuid(),
            Name = $"Default grading tenant {marker}",
            Slug = $"default-grading-{marker}",
            AdminEmail = $"admin-{marker}@grading.test",
            IsActive = true,
            IsDefault = true,
        });
        await context.SaveChangesAsync();
    }

    private async Task<AuthenticatedActor> SignUpAsync(string label, string marker)
    {
        var email = $"{label}-{marker}@grading.test";
        var password = $"Contextual1!{Guid.NewGuid():N}";
        using var client = fixture.RealAuthenticationFactory.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/auth/sign-up", new
        {
            email,
            password,
            username = $"{label}-{marker}",
        });
        await EnsureSuccessAsync(response);
        var signIn = (await response.Content.ReadFromJsonAsync<SignInResponse>(JsonOptions))!;
        signIn.AccessToken.Should().NotBeNullOrWhiteSpace();
        signIn.TenantId.Should().NotBeNull();
        return new AuthenticatedActor(
            signIn.UserId,
            signIn.TenantId!.Value,
            email,
            password,
            signIn.AccessToken);
    }

    private async Task<HttpClient> SignInAsync(AuthenticatedActor actor)
    {
        using var anonymous = fixture.RealAuthenticationFactory.CreateClient();
        using var response = await anonymous.PostAsJsonAsync("/v1/auth/sign-in", new
        {
            actor.Email,
            actor.Password,
            actor.TenantId,
        });
        await EnsureSuccessAsync(response);
        var signIn = (await response.Content.ReadFromJsonAsync<SignInResponse>(JsonOptions))!;
        signIn.UserId.Should().Be(actor.UserId);
        signIn.TenantId.Should().Be(actor.TenantId);
        return CreateAuthenticatedClient(actor with { AccessToken = signIn.AccessToken });
    }

    private HttpClient CreateAuthenticatedClient(AuthenticatedActor actor)
    {
        var client = fixture.RealAuthenticationFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            actor.AccessToken);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", actor.TenantId.ToString());
        return client;
    }

    private static async Task<ProgramDto> CreateCourseAsync(HttpClient client, string slug) =>
        await PostAsync<ProgramDto>(
            client,
            "/v1/courses",
            new CreateProgramDto($"Course {slug}", "Contextual access E2E", slug));

    private static async Task EnrollAsync(HttpClient owner, Guid courseId, Guid userId)
    {
        using var response = await owner.PostAsync(
            $"/v1/courses/{courseId}/users/{userId}",
            content: null);
        await EnsureSuccessAsync(response);
    }

    private static Task<CourseAccessCapabilities> GetAccessAsync(HttpClient client, Guid courseId) =>
        GetAsync<CourseAccessCapabilities>(
            client,
            $"/v1/courses/{courseId}/access/capabilities");

    private static async Task<PublishedQuiz> CreatePublishedQuizAsync(
        HttpClient owner,
        Guid courseId,
        string slug,
        ReviewMethods reviewMethods)
    {
        var document = TrueFalseQuiz();
        var content = await PostAsync<ProgramContentDto>(
            owner,
            $"/v1/courses/{courseId}/content",
            new CreateProgramContentDto
            {
                ProgramId = courseId,
                Title = $"Quiz {slug}",
                Slug = slug,
                Description = "Real-session grading fixture",
                Type = ProgramContentType.Questionnaire,
                // The first graded document must enter through the atomic
                // assessment draft endpoint below, not generic content CRUD.
                JsonBody = null,
                IsRequired = true,
                EstimatedMinutesSource = EstimatedMinutesSource.Auto,
                Visibility = Visibility.Public,
            });

        var saved = await PutAsync<AssessmentDraftResult>(
            owner,
            $"{AssessmentsRoute}/course/{courseId}/content/{content.Id}/draft",
            new SaveAssessmentDraftRequest(
                content.Version,
                null,
                content.Title,
                slug,
                content.Description,
                document,
                Visibility.Public,
                true,
                null,
                EstimatedMinutesSource.Auto,
                reviewMethods,
                ScoreValue.FromUnits(100),
                MaxAttempts: 1,
                ContentCompletionMode: ContentCompletionMode.OnReleaseAndPass,
                ResultReleaseMode: ResultReleaseMode.Manual,
                ReviewConfigurationCanonicalJson: reviewMethods.HasFlag(ReviewMethods.InstructorReview)
                    ? "{\"schemaVersion\":1,\"instructor\":{\"requireOverrideReason\":false}}"
                    : null));

        if (saved.AssessmentId is not Guid assessmentId)
        {
            throw new InvalidOperationException("Saving the quiz draft did not create an assessment.");
        }
        var assessment = await GetAsync<AssessmentDto>(owner, $"{AssessmentsRoute}/{assessmentId}");
        var prepared = await PostAsync<PreparedAssessmentRevisionResult>(
            owner,
            $"{AssessmentsRoute}/{assessmentId}/revisions/prepare",
            new PrepareAssessmentRevisionRequest(assessment.Version));
        assessment = await GetAsync<AssessmentDto>(owner, $"{AssessmentsRoute}/{assessmentId}");
        await PostAsync<PreparedAssessmentRevisionResult>(
            owner,
            $"{AssessmentsRoute}/{assessmentId}/revisions/publish",
            new PublishAssessmentRevisionRequest(prepared.RevisionId, assessment.Version));

        return new PublishedQuiz(content.Id, assessmentId);
    }

    private static async Task AssertOfficialFlowAsync(
        PublishedQuiz quiz,
        HttpClient learner,
        HttpClient otherLearner,
        HttpClient outsider,
        HttpClient owner,
        bool requiresInstructorReview,
        int expectedScoreUnits)
    {
        var started = await PostAsync<AssessmentSubmissionViewV1>(
            learner,
            $"{AssessmentsRoute}/content/{quiz.ContentId}/runtime-submissions/individual",
            new StartContentRuntimeSubmissionRequest($"start-{Guid.NewGuid():N}"));

        using (var otherLearnerRead = await otherLearner.GetAsync(
                   $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}"))
        {
            otherLearnerRead.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        using (var outsiderStart = await outsider.PostAsJsonAsync(
                   $"{AssessmentsRoute}/content/{quiz.ContentId}/runtime-submissions/individual",
                   new StartContentRuntimeSubmissionRequest($"outsider-{Guid.NewGuid():N}"),
                   JsonOptions))
        {
            outsiderStart.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        using (var outsiderQueue = await outsider.GetAsync(
                   $"{AssessmentsRoute}/{quiz.AssessmentId}/grading-queue"))
        {
            outsiderQueue.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        var submitted = await PostAsync<AssessmentSubmissionViewV1>(
            learner,
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}/submit",
            new SubmitAssessmentRuntimeRequest(
                TrueFalseAnswer(true),
                $"submit-{Guid.NewGuid():N}"));

        var beforeRelease = await GetAsync<AssessmentSubmissionViewV1>(
            learner,
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}");
        beforeRelease.Execution.LearnerVisibleResult.Should().BeNull();
        beforeRelease.Execution.Released.Should().BeFalse();

        using (var unauthorizedReview = await outsider.PostAsJsonAsync(
                   $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}/instructor-review",
                   new ResolveInstructorReviewRequest(
                       Resolution(expectedScoreUnits),
                       $"outsider-review-{Guid.NewGuid():N}"),
                   JsonOptions))
        {
            unauthorizedReview.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        AssessmentSubmissionViewV1 finalized;
        if (requiresInstructorReview)
        {
            submitted.Execution.RequiresInstructorReview.Should().BeTrue();
            finalized = await PostAsync<AssessmentSubmissionViewV1>(
                owner,
                $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}/instructor-review",
                new ResolveInstructorReviewRequest(
                    Resolution(expectedScoreUnits),
                    $"owner-review-{Guid.NewGuid():N}"));
        }
        else
        {
            finalized = await GetAsync<AssessmentSubmissionViewV1>(
                owner,
                $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}");
        }

        finalized.Execution.RequiresInstructorReview.Should().BeFalse();
        finalized.Execution.InstructorVisibleResult!.Score.Should()
            .Be(ScoreValue.FromUnits(expectedScoreUnits));

        var release = new ReleaseGradeResultCommand(
            finalized.Execution.ActiveRoundId!.Value,
            finalized.Version,
            $"release-{Guid.NewGuid():N}",
            "Approved in the real-session E2E");
        using (var outsiderRelease = await outsider.PostAsJsonAsync(
                   $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}/release",
                   release,
                   JsonOptions))
        {
            outsiderRelease.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        await PostAsync<GradeResultReleaseResponse>(
            owner,
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}/release",
            release);

        var released = await GetAsync<AssessmentSubmissionViewV1>(
            learner,
            $"{AssessmentsRoute}/runtime-submissions/{started.SubmissionId}");
        released.Execution.Released.Should().BeTrue();
        released.Execution.LearnerVisibleResult!.Score.Should()
            .Be(ScoreValue.FromUnits(expectedScoreUnits));
    }

    private static InstructorReviewResolutionV1 Resolution(int scoreUnits) => new(
        1,
        [new InstructorItemResolutionV1("q1", ScoreValue.FromUnits(scoreUnits))],
        "Reviewed by the course owner");

    private static AssessmentResponseEnvelopeV1 TrueFalseAnswer(bool value) => new(
        1,
        "quiz",
        "quiz-answer/v1",
        JsonSerializer.SerializeToElement(new
        {
            answers = new Dictionary<string, object>
            {
                ["q1"] = new { type = "TRUE_FALSE", value },
            },
        }));

    private static JsonElement TrueFalseQuiz()
    {
        using var document = JsonDocument.Parse("""
            {
              "schemaVersion": 1,
              "order": [["q1", "quiz"]],
              "blocks": {
                "q1": {
                  "type": "TRUE_FALSE",
                  "stem": "The statement is true.",
                  "points": 200,
                  "correctAnswer": true,
                  "settings": { "allowRetry": false }
                }
              },
              "grading": { "schemaVersion": 2, "items": { "q1": {} } }
            }
            """);
        return document.RootElement.Clone();
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string uri, object request)
    {
        using var response = await client.PostAsJsonAsync(uri, request, JsonOptions);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private static async Task<T> PutAsync<T>(HttpClient client, string uri, object request)
    {
        using var response = await client.PutAsJsonAsync(uri, request, JsonOptions);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string uri)
    {
        using var response = await client.GetAsync(uri);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(
            $"Expected a successful HTTP response, received {(int)response.StatusCode}: {body}");
    }

    private sealed record AuthenticatedActor(
        Guid UserId,
        Guid TenantId,
        string Email,
        string Password,
        string AccessToken);

    private sealed record PublishedQuiz(Guid ContentId, Guid AssessmentId);
}
