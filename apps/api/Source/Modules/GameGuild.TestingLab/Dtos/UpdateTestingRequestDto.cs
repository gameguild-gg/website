
namespace GameGuild.TestingLab;

public class UpdateTestingRequestDto {
  public Guid? ProjectVersionId { get; set; }

  [MaxLength(255)] public string? Title { get; set; }

  public string? Description { get; set; }

  [MaxLength(500)] public string? DownloadUrl { get; set; }

  public InstructionType? InstructionsType { get; set; }

  public string? InstructionsContent { get; set; }

  [MaxLength(500)] public string? InstructionsUrl { get; set; }

  public Guid? InstructionsFileId { get; set; }

  public int? MaxTesters { get; set; }

  public string? FeedbackFormContent { get; set; }

  public DateTime? StartDate { get; set; }

  public DateTime? EndDate { get; set; }

  public TestingRequestStatus? Status { get; set; }

  public void UpdateTestingRequest(TestingRequest testingRequest) {
    if (ProjectVersionId.HasValue)
        {
            testingRequest.ProjectVersionId = ProjectVersionId.Value;
        }

        if (!string.IsNullOrEmpty(Title))
        {
            testingRequest.Title = Title;
        }

        if (Description != null)
        {
            testingRequest.Description = Description;
        }

        if (DownloadUrl != null)
        {
            testingRequest.DownloadUrl = DownloadUrl;
        }

        if (InstructionsType.HasValue)
        {
            testingRequest.InstructionsType = InstructionsType.Value;
        }

        if (InstructionsContent != null)
        {
            testingRequest.InstructionsContent = InstructionsContent;
        }

        if (InstructionsUrl != null)
        {
            testingRequest.InstructionsUrl = InstructionsUrl;
        }

        if (InstructionsFileId.HasValue)
        {
            testingRequest.InstructionsFileId = InstructionsFileId.Value;
        }

        if (MaxTesters.HasValue)
        {
            testingRequest.MaxTesters = MaxTesters.Value;
        }

        if (FeedbackFormContent != null)
        {
            testingRequest.FeedbackFormContent = FeedbackFormContent;
        }

        if (StartDate.HasValue)
        {
            testingRequest.StartDate = StartDate.Value;
        }

        if (EndDate.HasValue)
        {
            testingRequest.EndDate = EndDate.Value;
        }

        if (Status.HasValue)
        {
            testingRequest.Status = Status.Value;
        }
    }
}
