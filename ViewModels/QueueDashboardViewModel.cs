using HUIOTClinicMS.Models;

namespace HUIOTClinicMS.ViewModels
{
    public class QueueDashboardViewModel
    {
        public List<ServicePoint> ServicePoints { get; set; } = new();
        public int SelectedServicePointID { get; set; }
        public ServicePoint? SelectedServicePoint { get; set; }

        public Queue? CurrentServingQueue { get; set; }
        public int? CurrentServingEncounterID { get; set; }
        public int? CurrentServingLabRequestID { get; set; }
        public List<Queue> WaitingQueues { get; set; } = new();
        
        public int WaitingCount { get; set; }
        public int CalledCount { get; set; }
        public int InServiceCount { get; set; }
        public int CompletedCount { get; set; }
    }
}
