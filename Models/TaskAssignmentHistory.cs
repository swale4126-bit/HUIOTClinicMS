using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HUIOTClinicMS.Data;

namespace HUIOTClinicMS.Models
{
    public class TaskAssignmentHistory
    {
        public int ID { get; set; }

        [Required]
        [MaxLength(50)]
        public string TaskType { get; set; } = string.Empty; // "PatientEncounter", "LabRequest", "Prescription"

        public int TaskID { get; set; }

        public int? AssignedFromStaffID { get; set; }
        [ForeignKey("AssignedFromStaffID")]
        public Staff? AssignedFromStaff { get; set; }

        public int AssignedToStaffID { get; set; }
        [ForeignKey("AssignedToStaffID")]
        public Staff? AssignedToStaff { get; set; }

        [Required]
        public string AssignedByID { get; set; } = string.Empty;
        [ForeignKey("AssignedByID")]
        public ApplicationUser? AssignedByUser { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(255)]
        public string? Reason { get; set; }
    }
}
