using System;
using System.ComponentModel.DataAnnotations;
using cms.Common.Entities;
using Cms.Models;

namespace cms.Modules.Test.Models
{
    public class SessionRegistration : BaseEntity
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

        public TeamRole? ProjectRole
        {
            get;
            set;
        }

        public string? RegistrationNotes
        {
            get;
            set;
        }

        [Required]
        public AttendanceStatus AttendanceStatus
        {
            get;
            set;
        } = AttendanceStatus.Registered;

        public DateTime? AttendedAt
        {
            get;
            set;
        }
    }
}
