using System.ComponentModel.DataAnnotations;
using HUIOTClinicMS.Models;

namespace HUIOTClinicMS.ViewModels
{
    public class LabRequestCreateViewModel
    {
        [Required]
        public int MedicalRecordID { get; set; }
        public int? EncounterID { get; set; }
        public int PatientID { get; set; }
        public string PatientDisplayName { get; set; } = string.Empty;
        public bool IsStat { get; set; } = false;

        [Required(ErrorMessage = "Select at least one test.")]
        [MinLength(1, ErrorMessage = "Select at least one test.")]
        public List<int> SelectedTestTypeIds { get; set; } = new();

        public string? ClinicalNotes { get; set; }

        public List<TestType> AvailableTestTypes { get; set; } = new();

        // Populated only when no MedicalRecordID was supplied via the route,
        // so the user can pick a patient's medical record directly on this page.
        public List<MedicalRecord> AvailableMedicalRecords { get; set; } = new();

        /// <summary>Staff.ID of the Lab Technician this request is assigned to.</summary>
        [Display(Name = "Assign to Lab Technician")]
        public int? AssignedToStaffID { get; set; }

        /// <summary>Active LabTechnician staff records for the assignment dropdown.</summary>
        public List<Staff> AvailableLabTechs { get; set; } = new();
    }

    public class LabRequestEditViewModel
    {
        public int LabRequestID { get; set; }
        public string PatientDisplayName { get; set; } = string.Empty;
        public string RequestedByDisplayName { get; set; } = string.Empty;
        public DateTime RequestDate { get; set; }
        public string TestTypeNames { get; set; } = string.Empty;

        public string? ClinicalNotes { get; set; }

        [Required]
        public string Status { get; set; } = string.Empty;
    }
}