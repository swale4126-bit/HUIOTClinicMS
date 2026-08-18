using Microsoft.AspNetCore.SignalR.Protocol;

namespace HUIOTClinicMS.Models
{
    public class TestType
    {
        public int ID { get; set; }
        public string TestName { get; set; } = string.Empty;

      
        public ICollection<LabRequest> LabRequests { get; set; } = new List<LabRequest>();
    }
}
