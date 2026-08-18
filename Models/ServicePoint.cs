using System.ComponentModel.DataAnnotations;

namespace HUIOTClinicMS.Models
{
    public class ServicePoint
    {
        public int ID { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? RoomNumber { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
