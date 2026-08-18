using System.ComponentModel.DataAnnotations;

namespace HUIOTClinicMS.ViewModels
{
    public class PatientCreateViewModel
    {
        // ----- Personal Info -----
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

        // ----- Academic Info -----
        [Required]
        [Display(Name = "Department")]
        public int DepartmentID { get; set; }

        [Required]
        [Display(Name = "Dorm Block")]
        public int DormBlockID { get; set; }

        // ----- Emergency Contact -----
        [Required]
        [Display(Name = "Contact Name")]
        public string ContactName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Contact Phone")]
        public string ContactPhoneNumber { get; set; } = string.Empty;

        [Required]
        public string Relationship { get; set; } = string.Empty;
    }
}