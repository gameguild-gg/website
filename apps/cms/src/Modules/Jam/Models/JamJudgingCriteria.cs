using System;
using System.ComponentModel.DataAnnotations;
using cms.Common.Entities;

namespace cms.Modules.Jam.Models
{
    public class JamJudgingCriteria : BaseEntity
    {
        [Required]
        public Guid JamId
        {
            get;
            set;
        }

        [Required, MaxLength(100)]
        public string Name
        {
            get;
            set;
        } = string.Empty;

        public string? Description
        {
            get;
            set;
        }

        public decimal Weight
        {
            get;
            set;
        } = 1.0m;

        [Required]
        public int MaxScore
        {
            get;
            set;
        } = 5;
    }
}
