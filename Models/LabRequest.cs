namespace HUIOTClinicMS.Models
{
    public class LabRequest
    {
        public int LabRequestID { get; set; }

        public int PatientID { get; set; }
        public Patient? Patient { get; set; }

        public int MedicalRecordID { get; set; }
        public MedicalRecord? MedicalRecord { get; set; }

        public int RequestedByID { get; set; }
        public Staff? RequestedBy { get; set; }

        public DateTime RequestDate { get; set; }
        public string? ClinicalNotes { get; set; }
        public string Status { get; set; } = "Pending";
        public ICollection<TestType> TestTypes { get; set; } = new List<TestType>();
        
        public int? ClinicalEncounterID { get; set; }
        public ClinicalEncounter? ClinicalEncounter { get; set; }

        public int? AssignedToStaffID { get; set; }
        public Staff? AssignedToStaff { get; set; }
    }
}
