namespace HUIOTClinicMS.Models
{
    public class Prescription
    {
        public int PrescriptionID { get; set; }

        public int PatientID { get; set; }
        public Patient? Patient { get; set; }

        public int LabResultID { get; set; }
        public LabResult? LabResult { get; set; }

        public string RX { get; set; } = string.Empty;

        public int PrescribedByID { get; set; }
        public Staff? PrescribedBy { get; set; }

        public DateTime TimeDate { get; set; }

        public string Status { get; set; } = "Pending"; // Pending | Dispensed

        public Medication? Medication { get; set; }
    }
}