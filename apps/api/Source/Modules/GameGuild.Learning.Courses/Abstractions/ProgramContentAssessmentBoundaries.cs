using System.Text.Json;

namespace GameGuild.Learning.Courses;

public interface IProgramContentLearnerProjector
{
    ProgramContentType ContentType { get; }
    JsonElement Project(JsonElement authoringDocument);
}

public enum ProgramContentAcademicMutation
{
    Authoring,
    Start,
    UpdateProgress,
    Submit,
    Complete,
    Grade,
    Delete,
}

/// <summary>
/// Lets an assessment adapter reserve academic mutations without coupling Courses
/// to the grading runtime or to a particular assessment type.
/// </summary>
public interface IProgramContentAcademicMutationGuard
{
    string? GetRejection(ProgramContent content, ProgramContentAcademicMutation mutation);
}

/// <summary>
/// Lets a feature module participate in the same content lifecycle transaction
/// without coupling Courses to that module's persistence model.
/// </summary>
public interface IProgramContentDeleteParticipant
{
    bool CanHandle(ProgramContent content);
    Task PrepareDeleteAsync(ProgramContent content, CancellationToken cancellationToken = default);
}

/// <summary>
/// Lets a feature module synchronize its published projection before the
/// authoring transaction is committed, without coupling Courses to that
/// module's persistence model.
/// </summary>
public interface IProgramContentPublicationParticipant
{
    bool CanHandle(ProgramContent content);

    Task PreparePublishAsync(
        ProgramContent content,
        AuthoringContentPayload payload,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task FinalizePublishAsync(
        ProgramContent content,
        AuthoringContentPayload payload,
        Guid actorId,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public static class ProgramContentAcademicMutationGuard
{
    public static void EnsureAllowed(
        IEnumerable<IProgramContentAcademicMutationGuard> guards,
        ProgramContent content,
        ProgramContentAcademicMutation mutation)
    {
        foreach (var guard in guards)
        {
            var rejection = guard.GetRejection(content, mutation);
            if (rejection is not null)
            {
                throw new InvalidOperationException(rejection);
            }
        }
    }
}
