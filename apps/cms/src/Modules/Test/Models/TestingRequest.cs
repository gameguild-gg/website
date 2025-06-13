using System.ComponentModel.DataAnnotations;
using GameGuild.Common.Entities;

namespace GameGuild.Modules.Test.Models
{
    public class TestingRequest : BaseEntity
    {
        [Required]
        public Guid ProjectVersionId
        {
            get;
            set;
        }

        [Required, MaxLength(255)]
        public string Title
        {
            get;
            set;
        } = string.Empty;

        public string? Description
        {
            get;
            set;
        }

        [Required]
        public InstructionType InstructionsType
        {
            get;
            set;
        }

        public string? InstructionsContent
        {
            get;
            set;
        }

        [MaxLength(500)]
        public string? InstructionsUrl
        {
            get;
            set;
        }

        public Guid? InstructionsFileId
        {
            get;
            set;
        }

        public int? MaxTesters
        {
            get;
            set;
        }

        public int CurrentTesterCount
        {
            get;
            set;
        } = 0;

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

        [Required]
        public TestingRequestStatus Status
        {
            get;
            set;
        } = TestingRequestStatus.Draft;

        [Required]
        public Guid CreatedBy
        {
            get;
            set;
        }
    }
}
