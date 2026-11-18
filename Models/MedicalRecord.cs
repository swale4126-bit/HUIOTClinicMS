using HUIOTClinicMS.Models.Interfaces;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace HUIOTClinicMS.Models
{
    public class MedicalRecord : IAuditable
    {
        public int ID { get; set; }

        public int PatientID { get; set; }
        public Patient? Patient { get; set; }
        
        public int? EncounterID { get; set; }
        public ClinicalEncounter? Encounter { get; set; }

        public string? ChiefComplaint { get; set; }
        public string? GeneralAppearance { get; set; }
        public string? Diagnosis { get; set; }
        public string? Treatment { get; set; }
        public string? Notes { get; set; }

        public DateTime DateTime { get; set; }

        public int CreatedByID { get; set; }
        public Staff? CreatedByStaff { get; set; } // Rename nav property slightly or keep it, wait, let's just add IAuditable fields

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        public ICollection<LabRequest>? LabRequests { get; set; }
        public ICollection<LabResult>? LabResults { get; set; }
    }
}
