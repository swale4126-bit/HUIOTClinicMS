using System.ComponentModel.DataAnnotations;

namespace HUIOTClinicMS.ViewModels
{
    public class PatientCreateViewModel
    {
        // ----- Personal Info -----
        [Required(ErrorMessage = "University ID Number is required.")]
        [Display(Name = "University ID Number")]
        [RegularExpression(@"^[a-zA-Z]{1,10}/\d{1,6}/\d{2,4}$", ErrorMessage = "Student ID must follow university format: Letters/Number/Year (e.g., NaScR/1245/16).")]
        public string IDNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "First name is required.")]
        [Display(Name = "First Name")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "First name must be between 2 and 50 characters.")]
        [RegularExpression(@"^[a-zA-Z\s'-]+$", ErrorMessage = "First name must contain only letters (no numbers or special symbols allowed).")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [Display(Name = "Last Name")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Last name must be between 2 and 50 characters.")]
        [RegularExpression(@"^[a-zA-Z\s'-]+$", ErrorMessage = "Last name must contain only letters (no numbers or special symbols allowed).")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select gender.")]
        [RegularExpression("^(Male|Female)$", ErrorMessage = "Gender must be either Male or Female.")]
        public string Gender { get; set; } = string.Empty;

        [Required(ErrorMessage = "Date of birth is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime DateOfBirth { get; set; }

        [Display(Name = "Phone Number")]
        [RegularExpression(@"^(\+251|0)[79][0-9\s-]{8,12}$", ErrorMessage = "Phone number must be a valid phone number starting with 09, 07, or +251 (e.g., 0912345678). Letters are not allowed.")]
        public string? PhoneNumber { get; set; }

        // ----- Academic Info -----
        [Required(ErrorMessage = "Please select a department.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a department.")]
        [Display(Name = "Department")]
        public int DepartmentID { get; set; }

        [Required(ErrorMessage = "Please select a dorm block.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a dorm block.")]
        [Display(Name = "Dorm Block")]
        public int DormBlockID { get; set; }

        // ----- Emergency Contact -----
        [Required(ErrorMessage = "Emergency contact name is required.")]
        [Display(Name = "Contact Name")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Contact name must be between 2 and 100 characters.")]
        [RegularExpression(@"^[a-zA-Z\s'-]+$", ErrorMessage = "Contact name must contain only letters (no numbers or special symbols allowed).")]
        public string ContactName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Emergency contact phone is required.")]
        [Display(Name = "Contact Phone")]
        [RegularExpression(@"^(\+251|0)[79][0-9\s-]{8,12}$", ErrorMessage = "Contact phone must be a valid phone number starting with 09, 07, or +251 (e.g., 0912345678). Letters are not allowed.")]
        public string ContactPhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select or specify relationship.")]
        [RegularExpression(@"^[a-zA-Z\s'-]{2,50}$", ErrorMessage = "Relationship must contain only letters.")]
        public string Relationship { get; set; } = string.Empty;

        // ----- Clinical Handover & Assignment -----
        [Required(ErrorMessage = "Please select a destination triage nurse on duty.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a destination triage nurse on duty.")]
        [Display(Name = "Destination Triage Nurse")]
        public int DestinationNurseStaffID { get; set; }

        public bool IsEmergency { get; set; }

        [Display(Name = "Administrative Handover Note")]
        [MaxLength(255)]
        public string? HandoverNote { get; set; }

        public IEnumerable<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>? EligibleNurses { get; set; }
    }
}