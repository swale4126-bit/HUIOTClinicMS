using HUIOTClinicMS.Models;

namespace HUIOTClinicMS.ViewModels
{
    public class StudentDashboardViewModel
    {
        public Patient? Patient { get; set; }
        public int MyVisits { get; set; }
        public int PendingLabResults { get; set; }
        public int ActivePrescriptions { get; set; }

        public List<MedicalRecord> RecentVisits { get; set; } = new();
    }
}