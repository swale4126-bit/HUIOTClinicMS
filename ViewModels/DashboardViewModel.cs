

using HUIOTClinicMS.Models;

namespace HUIOTClinicMS.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalPatients { get; set; }
        public int MedicalRecordsToday { get; set; }
        public int PendingLabRequests { get; set; }
        public int ActiveStaff { get; set; }

        public List<MedicalRecord> RecentMedicalRecords { get; set; } = new();
        public List<LabRequest> RecentLabRequests { get; set; } = new();
    }
}