using System.ComponentModel.DataAnnotations;
using GameGuild.Common.Entities;

namespace GameGuild.Modules.Jam.Models
{
    public class JamSubmission : BaseEntity
    {
        [Required]
        public Guid JamId
        {
            get;
            set;
        }

        [Required]
        public Guid ProjectVersionId
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

        public string? SubmissionNotes
        {
            get;
            set;
        }
    }
}
