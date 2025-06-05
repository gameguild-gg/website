using System.ComponentModel;

namespace cms.Common.Enums;

public enum FeedbackFormQuestionType
{
    [Description("Single line text input")]
    ShortAnswer,

    [Description("Multi-line text area")]
    LongAnswer,

    [Description("Select one option from multiple choices")]
    MultipleChoice,

    [Description("Select multiple options from a list")]
    Checkbox,

    [Description("Numeric rating scale (e.g., 1-5 stars)")]
    RatingScale,

    [Description("Simple yes/no question")]
    YesNo,

    [Description("Select one option from dropdown menu")]
    Dropdown
}
