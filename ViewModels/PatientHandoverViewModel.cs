using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HUIOTClinicMS.ViewModels
{
    public class PatientHandoverViewModel
    {
        public int PatientID { get; set; }

        public string PatientName { get; set; } = string.Empty;

        public string IDNumber { get; set; } = string.Empty;

        public string? CardNumber { get; set; }

        public string? DepartmentName { get; set; }

        public string? DormBlockName { get; set; }

        public int? RegistrationNurseStaffID { get; set; }

        public string? RegistrationNurseName { get; set; }

        [Required(ErrorMessage = "Please select a destination nurse to receive the patient.")]
        [Display(Name = "Destination Nurse")]
        public int DestinationNurseStaffID { get; set; }

        [Display(Name = "Administrative Handover Note")]
        [MaxLength(255, ErrorMessage = "Handover note cannot exceed 255 characters.")]
        public string? HandoverNote { get; set; }

        public IEnumerable<SelectListItem>? EligibleNurses { get; set; }

        public bool IsExistingPatient { get; set; }
        
        [Display(Name = "STAT Emergency Handover")]
        public bool IsEmergency { get; set; }
    }
}
