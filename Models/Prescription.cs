namespace HUIOTClinicMS.Models
{
    public class Prescription
    {
        public int PrescriptionID { get; set; }

        public int PatientID { get; set; }
        public Patient? Patient { get; set; }

        public int? ClinicalEncounterID { get; set; }
        public ClinicalEncounter? ClinicalEncounter { get; set; }

        public int? LabResultID { get; set; }
        public LabResult? LabResult { get; set; }

        public string RX { get; set; } = string.Empty;

        public int PrescribedByID { get; set; }
        public Staff? PrescribedBy { get; set; }

        public DateTime TimeDate { get; set; }

        public string Status { get; set; } = "Pending"; // Pending | PartiallyDispensed | Dispensed | Cancelled

        public int? AssignedToStaffID { get; set; }
        public Staff? AssignedToStaff { get; set; }

        public Medication? Medication { get; set; }
    }
}