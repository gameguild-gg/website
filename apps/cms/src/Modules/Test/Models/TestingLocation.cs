using System;
using System.ComponentModel.DataAnnotations;
using cms.Common.Entities;

namespace cms.Modules.Test.Models
{
    public class TestingLocation : BaseEntity
    {
        [Required, MaxLength(255)]
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

        public string? Address
        {
            get;
            set;
        }

        [Required]
        public int MaxTestersCapacity
        {
            get;
            set;
        }

        [Required]
        public int MaxProjectsCapacity
        {
            get;
            set;
        }

        public string? EquipmentAvailable
        {
            get;
            set;
        }

        [Required]
        public LocationStatus Status
        {
            get;
            set;
        } = LocationStatus.Active;
    }
}
