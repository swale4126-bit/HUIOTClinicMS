using System.ComponentModel.DataAnnotations;

namespace HUIOTClinicMS.Models
{
    public class Patient
    {
        public int ID { get; set; }

        [Required]
        public string IDNumber { get; set; } = string.Empty;

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        public string Gender { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        public int DepartmentID { get; set; }
        public Department? Department { get; set; }

        public int DormBlockID { get; set; }
        public DormBlock? DormBlock { get; set; }

        public string? PhoneNumber { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? CardNumber { get; set; }
    }
}