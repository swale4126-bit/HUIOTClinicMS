using HUIOTClinicMS.Models;

namespace HUIOTClinicMS.ViewModels
{
    public class NurseQueueItem
    {
        public int PatientID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string CardNumber { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public DateTime TimeIn { get; set; }
        public bool HasMedicalRecordToday { get; set; }
        public int? MedicalRecordIDToday { get; set; }
        public string QueueStatus { get; set; } = string.Empty;
        public string QueueType { get; set; } = string.Empty;
    }

    public class NurseDashboardViewModel
    {
        public int PatientsRegisteredToday { get; set; }
        public int PendingTriage { get; set; }
        public int TotalRecords { get; set; }

        public List<NurseQueueItem> TodaysQueue { get; set; } = new();
        public List<NurseGroupedLabResult> RecentGroupedLabResults { get; set; } = new();
        public List<NurseGroupedLabResult> PrescribedPatients { get; set; } = new();
    }

    public class NurseGroupedLabResult
    {
        public int MedicalRecordID { get; set; }
        public Patient? Patient { get; set; }
        public List<LabResult> Results { get; set; } = new List<LabResult>();
        public int PrimaryLabResultID { get; set; } 
    }
}