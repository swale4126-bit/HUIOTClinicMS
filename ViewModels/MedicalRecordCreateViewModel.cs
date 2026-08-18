using System.ComponentModel.DataAnnotations;

namespace HUIOTClinicMS.ViewModels
{
    public class MedicalRecordCreateViewModel
    {
        [Required]
        public int PatientID { get; set; }

        // Display-only, populated when patient is pre-selected (e.g. from Nurse queue)
        public string? PatientDisplayName { get; set; }

        [Required]
        [Display(Name = "Chief Complaint")]
        public string ChiefComplaint { get; set; } = string.Empty;

        [Display(Name = "General Appearance")]
        public string? GeneralAppearance { get; set; }

        public string? Diagnosis { get; set; }

        public string? Treatment { get; set; }

        public string? Notes { get; set; }
    }
}