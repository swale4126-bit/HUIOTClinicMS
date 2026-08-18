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
    }
}
