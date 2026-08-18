using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HUIOTClinicMS.Data;

namespace HUIOTClinicMS.Models
{
    public class ServiceLog
    {
        public int ID { get; set; }

        public int QueueID { get; set; }
        [ForeignKey("QueueID")]
        public Queue? Queue { get; set; }

        public string? ServedByID { get; set; }
        [ForeignKey("ServedByID")]
        public ApplicationUser? ServedBy { get; set; }

        public DateTime? CalledAt { get; set; }
        public DateTime? ServiceStartedAt { get; set; }
        public DateTime? ServiceEndedAt { get; set; }
    }
}
