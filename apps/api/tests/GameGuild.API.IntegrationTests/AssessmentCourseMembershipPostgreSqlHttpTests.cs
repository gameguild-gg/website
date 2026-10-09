using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using GameGuild.Learning.Assessments;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CourseProgram = GameGuild.Learning.Courses.Program;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class AssessmentCourseMembershipPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    [Theory]
    [InlineData("OwnedActive", HttpStatusCode.Created)]
    [InlineData("OwnedLegacyUnscoped", HttpStatusCode.Created)]
    [InlineData("ForeignOwner", HttpStatusCode.Forbidden)]
    [InlineData("Cancelled", HttpStatusCode.BadRequest)]
    [InlineData("Expired", HttpStatusCode.BadRequest)]
    [InlineData("Paused", HttpStatusCode.BadRequest)]
    [InlineData("Deleted", HttpStatusCode.BadRequest)]
    [InlineData("ForeignTenant", HttpStatusCode.BadRequest)]
    [InlineData("ForeignCourse", HttpStatusCode.BadRequest)]
    public async Task CanonicalProgramEnrollment_StartRequiresOwnedActiveCourseMembership(
        string membershipCase,
        HttpStatusCode expectedStatus)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var tenantId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        var learnerId = Guid.NewGuid();
        var otherLearnerId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var foreignCourseId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        Guid assessmentId;

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.AddRange(
                new Tenant { Id = tenantId, Name = $"Membership {suffix}", Slug = $"membership-{suffix}", AdminEmail = $"admin-{suffix}@membership.test", IsActive = true },
                new Tenant { Id = foreignTenantId, Name = $"Other {suffix}", Slug = $"other-membership-{suffix}", AdminEmail = $"other-admin-{suffix}@membership.test", IsActive = true },
                User(learnerId, suffix, "learner"),
                User(otherLearnerId, suffix, "other-learner"),
                User(instructorId, suffix, "instructor"));
            context.AddRange(new[] { learnerId, otherLearnerId, instructorId }.Select(userId => new TenantMember
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId,
                Role = "Member",
                IsActive = true,
            }));
            context.AddRange(
                new CourseProgram { Id = courseId, TenantId = tenantId, CreatorId = instructorId, Title = "Membership course", Slug = $"membership-course-{suffix}" },
                new CourseProgram { Id = foreignCourseId, TenantId = tenantId, CreatorId = instructorId, Title = "Other course", Slug = $"other-course-{suffix}" });
            await context.SaveChangesAsync();

            var assessment = Assessment.Create(courseId, "Membership attempt", AssessmentType.Assignment, ScoreValue.FromUnits(100));
            assessment.TenantId = tenantId;
            assessmentId = assessment.Id;
            var enrollment = new ProgramEnrollment
            {
                Id = enrollmentId,
                ProgramId = membershipCase == "ForeignCourse" ? foreignCourseId : courseId,
                UserId = membershipCase == "ForeignOwner" ? otherLearnerId : learnerId,
                TenantId = membershipCase switch
                {
                    "OwnedLegacyUnscoped" => null,
                    "ForeignTenant" => foreignTenantId,
                    _ => tenantId,
                },
                EnrollmentStatus = membershipCase switch
                {
                    "Cancelled" => EnrollmentStatus.Cancelled,
                    "Expired" => EnrollmentStatus.Expired,
                    "Paused" => EnrollmentStatus.Paused,
                    _ => EnrollmentStatus.Active,
                },
                DeletedAt = membershipCase == "Deleted" ? DateTime.UtcNow : null,
            };
            context.Add(assessment);
            context.Add(enrollment);
            await context.SaveChangesAsync();
        }

        using var client = fixture.CreateAuthenticatedClient(learnerId, tenantId);
        var response = await client.PostAsJsonAsync(
            $"/v1/assessments/{assessmentId}/submissions/start",
            new StartSubmissionRequest(enrollmentId));
        response.StatusCode.Should().Be(expectedStatus, await response.Content.ReadAsStringAsync());

        await using var verification = fixture.Factory.Services.CreateAsyncScope();
        var verifiedContext = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var attempts = await verifiedContext.Set<AssessmentSubmission>()
            .Where(value => value.AssessmentId == assessmentId)
            .ToListAsync();
        if (expectedStatus == HttpStatusCode.Created)
        {
            attempts.Should().ContainSingle();
            attempts[0].EnrollmentId.Should().Be(enrollmentId);
            attempts[0].UserId.Should().Be(learnerId);
        }
        else
        {
            attempts.Should().BeEmpty("a rejected membership must not persist an attempt");
        }
    }

    private static User User(Guid id, string suffix, string name) => new()
    {
        Id = id,
        Name = name,
        Email = $"{name}-{suffix}@membership.test",
        Username = $"{name}-{suffix}",
        IsActive = true,
        IsEmailVerified = true,
    };
}
