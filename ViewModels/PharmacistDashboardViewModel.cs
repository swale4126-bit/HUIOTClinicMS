using HUIOTClinicMS.Models;

namespace HUIOTClinicMS.ViewModels
{
    public class PharmacistDashboardViewModel
    {
        public int PrescriptionsToday { get; set; }
        public int TotalMedicationsInRegistry { get; set; }
        public int TotalPrescriptions { get; set; }
        public bool MyTasksOnly { get; set; }
        public int AssignedToMePendingCount { get; set; }

        public List<Prescription> RecentPrescriptions { get; set; } = new();
    }
}