using System.ComponentModel.DataAnnotations;
using HUIOTClinicMS.Models.Interfaces;

namespace HUIOTClinicMS.Models
{
    public class TriageAssessment : IAuditable
    {
        [Key]
        public int ID { get; set; }

        [Required]
        public int EncounterID { get; set; }
        public ClinicalEncounter? Encounter { get; set; }

        public decimal? Temperature { get; set; }
        public int? HeartRate { get; set; }
        public string? BloodPressure { get; set; }
        public decimal? Weight { get; set; }
        
        public string? PriorityLevel { get; set; } // Routine, Urgent, Emergency
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
