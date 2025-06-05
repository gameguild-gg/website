using System;
using System.ComponentModel.DataAnnotations;
using cms.Common.Entities;

namespace cms.Modules.Jam.Models
{
    public class JamScore : BaseEntity
    {
        [Required]
        public Guid SubmissionId
        {
            get;
            set;
        }

        [Required]
        public Guid CriteriaId
        {
            get;
            set;
        }

        [Required]
        public Guid JudgeUserId
        {
            get;
            set;
        }

        [Required]
        public int Score
        {
            get;
            set;
        }

        public string? Feedback
        {
            get;
            set;
        }
    }
}
