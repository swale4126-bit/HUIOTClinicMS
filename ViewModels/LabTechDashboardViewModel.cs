using HUIOTClinicMS.Models;

namespace HUIOTClinicMS.ViewModels
{
    public class LabTechDashboardViewModel
    {
        public int PendingRequests { get; set; }
        public int CompletedToday { get; set; }
        public int TotalResults { get; set; }
        public bool MyTasksOnly { get; set; }
        public int AssignedToMePendingCount { get; set; }

        public List<LabRequest> PendingLabRequests { get; set; } = new();
        public List<LabRequest> InProgressLabRequests { get; set; } = new();
        public List<LabRequest> CompletedLabRequests { get; set; } = new();
    }
}