using HUIOTClinicMS.Models;

namespace HUIOTClinicMS.ViewModels
{
    public class PatientDetailsViewModel
    {
        public Patient Patient { get; set; } = default!;
        public EmergencyContact? EmergencyContact { get; set; }
        public List<ClinicalEncounter> Encounters { get; set; } = new();
        public List<MedicalRecord> MedicalRecords { get; set; } = new();
        public List<LabRequest> LabRequests { get; set; } = new();
        public List<LabResult> LabResults { get; set; } = new();
        public List<Prescription> Prescriptions { get; set; } = new();
        public List<TaskAssignmentHistory> HandoverHistory { get; set; } = new();
    }
}