using System;

namespace HUIOTClinicMS.ViewModels
{
    public class PatientHandoverConfirmationViewModel
    {
        public int HandoverID { get; set; }
        public int PatientID { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string IDNumber { get; set; } = string.Empty;
        public string? CardNumber { get; set; }
        public string? DepartmentName { get; set; }
        public string? DormBlockName { get; set; }
        public string OriginNurseName { get; set; } = string.Empty;
        public string DestinationNurseName { get; set; } = string.Empty;
        public DateTime HandoverTime { get; set; }
        public string? HandoverNote { get; set; }
        public bool IsEmergency { get; set; }
        public string? TokenNumber { get; set; }
        public string StatusMessage { get; set; } = "Handover Dispatched — In Triage Queue";
    }
}
