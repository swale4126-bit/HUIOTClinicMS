using System.ComponentModel.DataAnnotations;

namespace HUIOTClinicMS.ViewModels
{
    public class MedicalRecordCreateViewModel
    {
        [Required]
        public int PatientID { get; set; }

        public int? EncounterID { get; set; }

        // Display-only, populated when patient is pre-selected
        public string? PatientDisplayName { get; set; }
        public string? VitalsSummary { get; set; }

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