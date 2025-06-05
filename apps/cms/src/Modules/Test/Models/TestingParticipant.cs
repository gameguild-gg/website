using System;
using System.ComponentModel.DataAnnotations;
using cms.Common.Entities;

namespace cms.Modules.Test.Models
{
    public class TestingParticipant : BaseEntity
    {
        [Required]
        public Guid TestingRequestId
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

        [Required]
        public bool InstructionsAcknowledged
        {
            get;
            set;
        } = false;

        public DateTime? InstructionsAcknowledgedAt
        {
            get;
            set;
        }

        [Required]
        public DateTime StartedAt
        {
            get;
            set;
        } = DateTime.UtcNow;

        public DateTime? CompletedAt
        {
            get;
            set;
        }
    }
}
