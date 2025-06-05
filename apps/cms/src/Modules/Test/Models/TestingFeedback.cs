using System;
using System.ComponentModel.DataAnnotations;
using cms.Common.Entities;

namespace cms.Modules.Test.Models
{
    public class TestingFeedback : BaseEntity
    {
        [Required]
        public Guid TestingRequestId
        {
            get;
            set;
        }

        [Required]
        public Guid FeedbackFormId
        {
            get;
            set;
        }

        [Required]
        public Guid UserId
        {
            get;
            set;
        }

        public Guid? SessionId
        {
            get;
            set;
        }

        [Required]
        public TestingContext TestingContext
        {
            get;
            set;
        }

        [Required]
        public string FeedbackData
        {
            get;
            set;
        } = string.Empty; // JSON

        public string? AdditionalNotes
        {
            get;
            set;
        }
    }
}
