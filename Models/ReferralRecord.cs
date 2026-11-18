using System.ComponentModel.DataAnnotations;
using HUIOTClinicMS.Models.Interfaces;

namespace HUIOTClinicMS.Models
{
    public class ReferralRecord : IAuditable
    {
        [Key]
        public int ID { get; set; }

        [Required]
        public int EncounterID { get; set; }
        public ClinicalEncounter? Encounter { get; set; }

        [Required]
        public string DestinationFacility { get; set; } = string.Empty;
        
        [Required]
        public string ReasonForTransfer { get; set; } = string.Empty;
        
        public string? ClinicalSummary { get; set; }
        
        public string? TransferredBy { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
