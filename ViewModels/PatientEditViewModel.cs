using System.ComponentModel.DataAnnotations;

namespace HUIOTClinicMS.ViewModels
{
    public class PatientEditViewModel
    {
        public int ID { get; set; }

        [Required]
        [Display(Name = "University ID Number")]
        public string IDNumber { get; set; } = string.Empty;

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public string Gender { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        public string? PhoneNumber { get; set; }

        [Required]
        [Display(Name = "Department")]
        public int DepartmentID { get; set; }

        [Required]
        [Display(Name = "Dorm Block")]
        public int DormBlockID { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; }

        // Read-only display context
        public string CardNumber { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}