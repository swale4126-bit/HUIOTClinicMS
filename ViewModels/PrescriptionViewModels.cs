using System.ComponentModel.DataAnnotations;
using HUIOTClinicMS.Models;

namespace HUIOTClinicMS.ViewModels
{
    public class PrescriptionCreateViewModel
    {
        public int? LabResultID { get; set; }
        public int? EncounterID { get; set; }
        public int PatientID { get; set; }
        public string PatientDisplayName { get; set; } = string.Empty;
        public string? TestTypeName { get; set; }
        public string? LabResultSummary { get; set; }
        public string? ClinicalNotesSummary { get; set; }

        [Required(ErrorMessage = "Enter the prescription details.")]
        [Display(Name = "Prescription (Rx)")]
        public string RX { get; set; } = string.Empty;

        /// <summary>Staff.ID of the Pharmacist this prescription is assigned to.</summary>
        [Required(ErrorMessage = "Please select a pharmacist to receive this prescription.")]
        [Display(Name = "Assign to Pharmacist")]
        public int? AssignedToStaffID { get; set; }

        /// <summary>Active Pharmacist staff records for the assignment dropdown.</summary>
        public List<Staff> AvailablePharmacists { get; set; } = new();
    }

    public class PrescriptionEditViewModel
    {
        public int PrescriptionID { get; set; }
        public string PatientDisplayName { get; set; } = string.Empty;
        public string PrescribedByDisplayName { get; set; } = string.Empty;
        public DateTime TimeDate { get; set; }
        public string Status { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter the prescription details.")]
        [Display(Name = "Prescription (Rx)")]
        public string RX { get; set; } = string.Empty;
    }

    public class MedicationDispenseViewModel
    {
        public int PrescriptionID { get; set; }
        public string PatientDisplayName { get; set; } = string.Empty;
        public string RX { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter the medication name.")]
        [Display(Name = "Medication Name")]
        public string MedicationName { get; set; } = string.Empty;

        [Display(Name = "Medication Type")]
        public string? MedicationType { get; set; }

        [Display(Name = "Dose")]
        public string? PrescribedDose { get; set; }

        public decimal? Amount { get; set; }

        [Display(Name = "Quantity Dispensed")]
        public int? DispensedQuantity { get; set; }

        [Display(Name = "Dispense Status")]
        public string DispenseStatus { get; set; } = "Dispensed"; // "Dispensed" or "PartiallyDispensed"

        public string? Remark { get; set; }
    }
}