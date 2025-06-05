using System;
using System.ComponentModel.DataAnnotations;
using cms.Common.Entities;

namespace cms.Modules.Jam.Models
{
    public class Jam : BaseEntity
    {
        [Required, MaxLength(255)]
        public string Name
        {
            get;
            set;
        } = string.Empty;

        [Required, MaxLength(255)]
        public string Slug
        {
            get;
            set;
        } = string.Empty;

        [MaxLength(500)]
        public string? Theme
        {
            get;
            set;
        }

        public string? Description
        {
            get;
            set;
        }

        public string? Rules
        {
            get;
            set;
        }

        public string? SubmissionCriteria
        {
            get;
            set;
        }

        [Required]
        public DateTime StartDate
        {
            get;
            set;
        }

        [Required]
        public DateTime EndDate
        {
            get;
            set;
        }

        public DateTime? VotingEndDate
        {
            get;
            set;
        }

        public int? MaxParticipants
        {
            get;
            set;
        }

        public int ParticipantCount
        {
            get;
            set;
        } = 0;

        [Required]
        public JamStatus Status
        {
            get;
            set;
        } = JamStatus.Upcoming;

        [Required]
        public Guid CreatedBy
        {
            get;
            set;
        }
    }
}
