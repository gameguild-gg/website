using System;
using System.ComponentModel.DataAnnotations;
using cms.Common.Entities;

namespace cms.Modules.Test.Models
{
    public class SessionWaitlist : BaseEntity
    {
        [Required]
        public Guid SessionId
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
        public RegistrationType RegistrationType
        {
            get;
            set;
        }

        [Required]
        public int Position
        {
            get;
            set;
        }

        public string? RegistrationNotes
        {
            get;
            set;
        }
    }
}
