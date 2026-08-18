using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HUIOTClinicMS.Models
{
    public class Queue
    {
        public int ID { get; set; }

        [Required]
        [MaxLength(20)]
        public string TokenNumber { get; set; } = string.Empty;

        public int PatientID { get; set; }
        [ForeignKey("PatientID")]
        public Patient? Patient { get; set; }

        public int ServicePointID { get; set; }
        [ForeignKey("ServicePointID")]
        public ServicePoint? ServicePoint { get; set; }

        // Normal, High, Emergency
        [MaxLength(20)]
        public string PriorityLevel { get; set; } = "Normal";

        // Waiting, Called, InService, Completed, Cancelled
        [MaxLength(20)]
        public string QueueStatus { get; set; } = "Waiting";

        // Consultation, Result Review, etc.
        [MaxLength(50)]
        public string QueueType { get; set; } = "Consultation";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
