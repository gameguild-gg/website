using System.ComponentModel.DataAnnotations;
using GameGuild.Common.Entities;

namespace GameGuild.Modules.Test.Models
{
    public class FeedbackQualityRating : BaseEntity
    {
        [Required]
        public Guid FeedbackId
        {
            get;
            set;
        }

        [Required]
        public Guid RatedByUserId
        {
            get;
            set;
        }

        [Required]
        [Range(1, 5)]
        public int QualityRating
        {
            get;
            set;
        }

        public string? Reason
        {
            get;
            set;
        }
    }
}
