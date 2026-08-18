using HUIOTClinicMS.Models;

namespace HUIOTClinicMS.ViewModels
{
    public class PatientDetailsViewModel
    {
        public Patient Patient { get; set; } = default!;
        public EmergencyContact? EmergencyContact { get; set; }
    }
}