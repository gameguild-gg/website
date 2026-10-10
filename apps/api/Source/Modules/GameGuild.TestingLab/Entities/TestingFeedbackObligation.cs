namespace GameGuild.TestingLab;

[Table("testing_feedback_obligations")]
public sealed class TestingFeedbackObligation : EntityBase
{
    public Guid EventId { get; private set; }

    public Guid SlotId { get; private set; }

    public Guid ApplicationId { get; private set; }

    public Guid TesterUserId { get; private set; }

    public Guid? FeedbackId { get; private set; }

    public Guid? QuestionnaireRevisionId { get; private set; }

    public TestingFeedbackObligationStatus Status { get; private set; } = TestingFeedbackObligationStatus.Pending;

    public DateTime? FulfilledAt { get; private set; }

    private TestingFeedbackObligation()
    {
    }

    public static TestingFeedbackObligation Create(
        Guid eventId,
        Guid slotId,
        Guid applicationId,
        Guid testerUserId,
        Guid? tenantId,
        Guid? questionnaireRevisionId = null)
    {
        if (eventId == Guid.Empty || slotId == Guid.Empty || applicationId == Guid.Empty || testerUserId == Guid.Empty)
        {
            throw new ArgumentException("Event, slot, application, and tester are required.");
        }

        return new TestingFeedbackObligation
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            SlotId = slotId,
            ApplicationId = applicationId,
            TesterUserId = testerUserId,
            TenantId = tenantId,
            QuestionnaireRevisionId = questionnaireRevisionId,
        };
    }

    public bool IsFulfilled => Status is TestingFeedbackObligationStatus.Fulfilled or TestingFeedbackObligationStatus.Waived;

    public void Fulfill(Guid feedbackId)
    {
        if (feedbackId == Guid.Empty)
        {
            throw new ArgumentException("Feedback is required.", nameof(feedbackId));
        }

        if (IsFulfilled)
        {
            throw new InvalidOperationException("The feedback obligation is already complete.");
        }

        FeedbackId = feedbackId;
        Status = TestingFeedbackObligationStatus.Fulfilled;
        FulfilledAt = SystemClock.UtcNow;
        Touch();
    }

    public void Waive()
    {
        if (IsFulfilled)
        {
            throw new InvalidOperationException("The feedback obligation is already complete.");
        }

        Status = TestingFeedbackObligationStatus.Waived;
        FulfilledAt = SystemClock.UtcNow;
        Touch();
    }
}
