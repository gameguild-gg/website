using System;
using System.ComponentModel.DataAnnotations;
using cms.Common.Entities;

namespace cms.Modules.Test.Models
{
    public class TestingFeedbackForm : BaseEntity
    {
        [Required]
        public Guid TestingRequestId
        {
            get;
            set;
        }

        [Required]
        public string FormSchema
        {
            get;
            set;
        } = string.Empty; // JSON

        public bool IsForOnline
        {
            get;
            set;
        } = true;

        public bool IsForSessions
        {
            get;
            set;
        } = true;
    }
}
