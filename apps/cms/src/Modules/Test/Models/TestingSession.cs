using System;
using System.ComponentModel.DataAnnotations;
using cms.Common.Entities;

namespace cms.Modules.Test.Models
{
    public class TestingSession : BaseEntity
    {
        [Required]
        public Guid TestingRequestId
        {
            get;
            set;
        }

        [Required]
        public Guid LocationId
        {
            get;
            set;
        }

        [Required, MaxLength(255)]
        public string SessionName
        {
            get;
            set;
        } = string.Empty;

        [Required]
        public DateTime SessionDate
        {
            get;
            set;
        }

        [Required]
        public DateTime StartTime
        {
            get;
            set;
        }

        [Required]
        public DateTime EndTime
        {
            get;
            set;
        }

        [Required]
        public int MaxTesters
        {
            get;
            set;
        }

        public int RegisteredTesterCount
        {
            get;
            set;
        } = 0;

        public int RegisteredProjectMemberCount
        {
            get;
            set;
        } = 0;

        public int RegisteredProjectCount
        {
            get;
            set;
        } = 0;

        [Required]
        public SessionStatus Status
        {
            get;
            set;
        } = SessionStatus.Scheduled;

        [Required]
        public Guid ManagerUserId
        {
            get;
            set;
        }

        [Required]
        public Guid CreatedBy
        {
            get;
            set;
        }
    }
}
