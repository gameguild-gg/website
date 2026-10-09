using GameGuild.Identity.Users;


namespace GameGuild.TestingLab;

/// <summary>
/// Represents feedback collected from testing sessions and QA activities
/// </summary>
[Table("testing_feedback")]
[Index(nameof(TestingRequestId))]
[Index(nameof(FeedbackFormId))]
[Index(nameof(UserId))]
[Index(nameof(SessionId))]
[Index(nameof(TestingContext))]
[Index(nameof(OverallRating))]
[Index(nameof(IsReported))]
[Index(nameof(QualityRating))]
[Index(nameof(TenantId))]
public class TestingFeedback : EntityBase
{
    /// <summary>
    /// Foreign key to the testing request
    /// </summary>
    public Guid? TestingRequestId { get; set; }

    /// <summary>
    /// Navigation property to the testing request
    /// </summary>
    public virtual TestingRequest? TestingRequest { get; set; }

    /// <summary>
    /// Foreign key to the feedback form
    /// </summary>
    public Guid? FeedbackFormId { get; set; }

    /// <summary>
    /// Navigation property to the feedback form
    /// </summary>
    public virtual TestingFeedbackForm? FeedbackForm { get; set; }

    public Guid? EventId { get; set; }

    public virtual TestingEvent? Event { get; set; }

    public Guid? ApplicationId { get; set; }

    public virtual TestingProjectApplication? Application { get; set; }

    /// <summary>
    /// Foreign key to the user who provided feedback
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Navigation property to the user who provided feedback
    /// </summary>
    public virtual User User { get; set; } = null!;

    /// <summary>
    /// Optional foreign key to the testing session
    /// </summary>
    public Guid? SessionId { get; set; }

    /// <summary>
    /// Navigation property to the testing session (optional)
    /// </summary>
    public virtual TestingSession? Session { get; set; }

    /// <summary>
    /// Testing context (online vs in-person)
    /// </summary>
    [Required]
    public TestingContext TestingContext { get; set; }

    /// <summary>
    /// Feedback data in JSON format
    /// </summary>
    [Required]
    public string FeedbackData { get; set; } = string.Empty;

    public Guid? QuestionnaireRevisionId { get; set; }

    public string? StructuredResponsesJson { get; set; }

    [NotMapped]
    public QuestionnaireResponse? StructuredResponses => string.IsNullOrWhiteSpace(StructuredResponsesJson)
        ? null
        : QuestionnaireResponse.FromJson(StructuredResponsesJson);

    /// <summary>
    /// Overall rating (1-10)
    /// </summary>
    [Range(1, 10)]
    public int? OverallRating { get; set; }

    /// <summary>
    /// Would the tester recommend this product
    /// </summary>
    public bool? WouldRecommend { get; set; }

    /// <summary>
    /// Additional notes from tester
    /// </summary>
    public string? AdditionalNotes { get; set; }

    /// <summary>
    /// Whether this feedback has been reported as inappropriate
    /// </summary>
    public bool IsReported { get; set; } = false;

    /// <summary>
    /// Quality rating of this feedback for tracking purposes
    /// </summary>
    public FeedbackQuality? QualityRating { get; set; }

    /// <summary>
    /// Reason for reporting this feedback
    /// </summary>
    [MaxLength(500)]
    public string? ReportReason { get; set; }

    /// <summary>
    /// Who reported this feedback
    /// </summary>
    public Guid? ReportedById { get; set; }

    public Guid? ReportedByUserId {
        get => ReportedById;
        set => ReportedById = value;
    }

    /// <summary>
    /// Navigation property to who reported this feedback
    /// </summary>
    public virtual User? ReportedBy { get; set; }

    /// <summary>
    /// When this feedback was reported
    /// </summary>
    public DateTime? ReportedAt { get; set; }

    // Navigation Properties
    /// <summary>
    /// Quality ratings for this feedback
    /// </summary>
    public virtual ICollection<FeedbackQualityRating> QualityRatings { get; set; } = new List<FeedbackQualityRating>();

    // Computed Properties
    /// <summary>
    /// Whether this feedback is global (tenant-independent)
    /// </summary>
    public override bool IsGlobal => TenantId == null;

    public static TestingFeedback CreateForEvent(
        Guid eventId,
        Guid applicationId,
        Guid testerUserId,
        TestingContext testingContext,
        string feedbackData,
        int? overallRating,
        bool? wouldRecommend,
        string? additionalNotes,
        Guid? tenantId)
    {
        if (eventId == Guid.Empty || applicationId == Guid.Empty || testerUserId == Guid.Empty)
        {
            throw new ArgumentException("Event, application, and tester are required.");
        }

        if (string.IsNullOrWhiteSpace(feedbackData))
        {
            throw new ArgumentException("Feedback data is required.", nameof(feedbackData));
        }

        if (overallRating is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(overallRating), "Rating must be between 1 and 10.");
        }

        return new TestingFeedback
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            ApplicationId = applicationId,
            UserId = testerUserId,
            TestingContext = testingContext,
            FeedbackData = feedbackData.Trim(),
            OverallRating = overallRating,
            WouldRecommend = wouldRecommend,
            AdditionalNotes = string.IsNullOrWhiteSpace(additionalNotes) ? null : additionalNotes.Trim(),
            TenantId = tenantId,
        };
    }

    public static TestingFeedback CreateStructuredForEvent(
        Guid eventId,
        Guid applicationId,
        Guid testerUserId,
        TestingContext testingContext,
        Guid questionnaireRevisionId,
        QuestionnaireSchema questionnaireSchema,
        QuestionnaireResponse responses,
        int? overallRating,
        bool? wouldRecommend,
        string? additionalNotes,
        Guid? tenantId)
    {
        if (questionnaireRevisionId == Guid.Empty)
        {
            throw new ArgumentException("Questionnaire revision is required.", nameof(questionnaireRevisionId));
        }

        if (!overallRating.HasValue || overallRating is < 1 or > 10)
        {
            throw new ArgumentException("Overall rating from 1 to 10 is required.", nameof(overallRating));
        }

        if (!wouldRecommend.HasValue)
        {
            throw new ArgumentException("A recommendation answer is required.", nameof(wouldRecommend));
        }

        QuestionnaireResponseValidator.EnsureValid(questionnaireSchema, responses);

        var feedback = CreateForEvent(
            eventId,
            applicationId,
            testerUserId,
            testingContext,
            responses.ToJson(),
            overallRating,
            wouldRecommend,
            additionalNotes,
            tenantId);
        feedback.QuestionnaireRevisionId = questionnaireRevisionId;
        feedback.StructuredResponsesJson = responses.ToJson();
        return feedback;
    }

    /// <summary>
    /// Whether this is positive feedback
    /// </summary>
    public bool IsPositive => OverallRating >= 7 && WouldRecommend == true;

    /// <summary>
    /// Whether this is negative feedback
    /// </summary>
    public bool IsNegative => OverallRating <= 4 || WouldRecommend == false;

    /// <summary>
    /// Average quality rating from other users
    /// </summary>
    public decimal? AverageQualityRating => QualityRatings?.Any() == true
        ? (decimal)QualityRatings.Average(qr => qr.QualityRating)
        : null;

    // Domain Methods
    /// <summary>
    /// Updates the overall rating
    /// </summary>
    public void SetOverallRating(int rating)
    {
        if (rating < 1 || rating > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 10");
        }

        OverallRating = rating;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Sets recommendation status
    /// </summary>
    public void SetRecommendation(bool wouldRecommend)
    {
        WouldRecommend = wouldRecommend;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Adds additional notes
    /// </summary>
    public void UpdateNotes(string? notes)
    {
        AdditionalNotes = notes;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Reports this feedback as inappropriate
    /// </summary>
    public void Report(Guid reportedById, string reason)
    {
        IsReported = true;
        ReportedById = reportedById;
        ReportReason = reason;
        ReportedAt = SystemClock.UtcNow;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Unreports this feedback
    /// </summary>
    public void Unreport()
    {
        IsReported = false;
        ReportedById = null;
        ReportReason = null;
        ReportedAt = null;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Sets quality rating
    /// </summary>
    public void SetQualityRating(FeedbackQuality quality)
    {
        QualityRating = quality;
        UpdatedAt = SystemClock.UtcNow;
    }
}
