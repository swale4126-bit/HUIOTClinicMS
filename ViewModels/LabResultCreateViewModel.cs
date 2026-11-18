using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using HUIOTClinicMS.Models;

namespace HUIOTClinicMS.ViewModels
{
    public class LabResultCreateViewModel
    {
        [Required(ErrorMessage = "Please select a Lab Request / Patient.")]
        [Display(Name = "Patient / Lab Request")]
        public int LabRequestID { get; set; }

        [Required(ErrorMessage = "Please select at least one Test Type.")]
        [Display(Name = "Test Type")]
        public List<int> SelectedTestTypeIds { get; set; } = new List<int>();

        public List<TestType> AvailableTestTypes { get; set; } = new List<TestType>();

        public Dictionary<int, string> Results { get; set; } = new Dictionary<int, string>();

        public Dictionary<int, string> Remarks { get; set; } = new Dictionary<int, string>();
        
        [Required(ErrorMessage = "Please select an eligible receiving nurse to review results and prescribe.")]
        [Display(Name = "Destination Nurse (To Review Results & Prescribe)")]
        public int ReceivingNurseStaffID { get; set; }

        [MaxLength(255, ErrorMessage = "Handover note cannot exceed 255 characters.")]
        [Display(Name = "Clinical Handover Note (Optional)")]
        public string? HandoverNote { get; set; }

        public IEnumerable<SelectListItem>? EligibleNurses { get; set; }

        public string? PatientDisplayName { get; set; }
        public int? EncounterID { get; set; }
    }
}
