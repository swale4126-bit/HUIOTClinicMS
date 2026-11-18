using System.ComponentModel.DataAnnotations;
using HUIOTClinicMS.Models.Enums;
using HUIOTClinicMS.Models.Interfaces;

namespace HUIOTClinicMS.Models
{
    public class ClinicalEncounter : IAuditable
    {
        [Key]
        public int ID { get; set; }
        
        [Required]
        public int PatientID { get; set; }
        public Patient? Patient { get; set; }
        
        [Required]
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }
        
        [Required]
        public EncounterType Type { get; set; } = EncounterType.Routine;
        
        [Required]
        public WorkflowState Status { get; set; } = WorkflowState.Registered;

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        public ICollection<MedicalRecord> ClinicalNotes { get; set; } = new List<MedicalRecord>();
        public ICollection<LabRequest> LabRequests { get; set; } = new List<LabRequest>();
        public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
        public TriageAssessment? TriageAssessment { get; set; }
        public ReferralRecord? ReferralRecord { get; set; }

        public int? AssignedToStaffID { get; set; }
        public Staff? AssignedToStaff { get; set; }
    }
}
