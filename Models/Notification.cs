using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HUIOTClinicMS.Data;

namespace HUIOTClinicMS.Models
{
    public class Notification
    {
        public int ID { get; set; }

        [Required]
        public string UserID { get; set; } = string.Empty;
        [ForeignKey("UserID")]
        public ApplicationUser? User { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        // LabRequest, LabResult, Prescription, System, etc.
        [MaxLength(50)]
        public string NotificationType { get; set; } = "System";

        public int? RelatedRecordID { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
