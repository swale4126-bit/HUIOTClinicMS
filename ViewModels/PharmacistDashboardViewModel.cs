using HUIOTClinicMS.Models;

namespace HUIOTClinicMS.ViewModels
{
    public class PharmacistDashboardViewModel
    {
        public int PrescriptionsToday { get; set; }
        public int TotalMedicationsInRegistry { get; set; }
        public int TotalPrescriptions { get; set; }

        public List<Prescription> RecentPrescriptions { get; set; } = new();
    }
}