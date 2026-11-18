using System.ComponentModel.DataAnnotations;
using HUIOTClinicMS.Models;

namespace HUIOTClinicMS.ViewModels
{
    public class ReassignTaskViewModel
    {
        [Required]
        public string TaskType { get; set; } = string.Empty; // "PatientEncounter", "LabRequest", "Prescription"

        [Required]
        public int TaskID { get; set; }

        public string TaskTitle { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;

        public int? CurrentAssignedStaffID { get; set; }
        public string CurrentAssignedStaffName { get; set; } = "Unassigned";

        [Required(ErrorMessage = "Please select an eligible healthcare professional.")]
        [Display(Name = "Assign To")]
        public int NewAssignedStaffID { get; set; }

        [Display(Name = "Reason for Assignment / Reassignment")]
        [MaxLength(250)]
        public string? Reason { get; set; }

        public List<TaskAssignmentHistory> History { get; set; } = new();
        public List<Staff> EligibleStaff { get; set; } = new();
    }
}
