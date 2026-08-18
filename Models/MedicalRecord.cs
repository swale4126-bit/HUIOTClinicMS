using Microsoft.AspNetCore.SignalR.Protocol;

namespace HUIOTClinicMS.Models
{
    public class MedicalRecord
    {
        public int ID { get; set; }

        public int PatientID { get; set; }
        public Patient? Patient { get; set; }

        public string? ChiefComplaint { get; set; }
        public string? GeneralAppearance { get; set; }
        public string? Diagnosis { get; set; }
        public string? Treatment { get; set; }
        public string? Notes { get; set; }

        public DateTime DateTime { get; set; }

        public int CreatedByID { get; set; }
        public Staff? CreatedBy { get; set; }

        public ICollection<LabRequest>? LabRequests { get; set; }
        public ICollection<LabResult>? LabResults { get; set; }
    }
}
