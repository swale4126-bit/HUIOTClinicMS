using System.ComponentModel.DataAnnotations;

namespace HUIOTClinicMS.ViewModels
{
    public class PrescriptionCreateViewModel
    {
        public int LabResultID { get; set; }
        public int PatientID { get; set; }
        public string PatientDisplayName { get; set; } = string.Empty;
        public string TestTypeName { get; set; } = string.Empty;
        public string LabResultSummary { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter the prescription details.")]
        [Display(Name = "Prescription (Rx)")]
        public string RX { get; set; } = string.Empty;
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

        public string? Remark { get; set; }
    }
}