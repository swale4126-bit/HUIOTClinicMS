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
        public string TokenNumber { get; set; } = string.Empty;
        public string ServicePointCode { get; set; } = string.Empty;
        public int? ActiveEncounterID { get; set; }
        public int? AssignedStaffId { get; set; }
        public string? AssignedStaffName { get; set; }
        public bool IsAssignedToMe { get; set; }
        public bool HasLabResultsReady { get; set; }
    }

    public class NurseDashboardViewModel
    {
        public int PatientsRegisteredToday { get; set; }
        public int PendingTriage { get; set; }
        public int TotalRecords { get; set; }

        public List<NurseQueueItem> TodaysQueue { get; set; } = new();
        public List<NurseHandoverItem> IncomingHandovers { get; set; } = new();
        public List<NurseLabHandoverItem> IncomingLabHandovers { get; set; } = new();
        public List<NurseGroupedLabResult> RecentGroupedLabResults { get; set; } = new();
        public List<NurseGroupedLabResult> PrescribedPatients { get; set; } = new();
    }

    public class NurseLabHandoverItem
    {
        public int HandoverID { get; set; }
        public int LabRequestID { get; set; }
        public int MedicalRecordID { get; set; }
        public int? EncounterID { get; set; }
        public int PatientID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string CardNumber { get; set; } = string.Empty;
        public string IDNumber { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string TestNames { get; set; } = string.Empty;
        public List<LabResult> Results { get; set; } = new();
        public string HandedOverByTechnicianName { get; set; } = string.Empty;
        public DateTime CompletedTime { get; set; }
        public string? HandoverNote { get; set; }
        public int PrimaryLabResultID { get; set; }
        public bool IsEmergency { get; set; }
    }

    public class NurseHandoverItem
    {
        public int HandoverID { get; set; }
        public int PatientID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string CardNumber { get; set; } = string.Empty;
        public string IDNumber { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string DormBlockName { get; set; } = string.Empty;
        public string HandoverFromStaffName { get; set; } = string.Empty;
        public DateTime HandoverTime { get; set; }
        public string? HandoverNote { get; set; }
        public bool HasActiveEncounter { get; set; }
        public int? ActiveEncounterID { get; set; }
        public bool IsEmergency { get; set; }
    }

    public class NurseGroupedLabResult
    {
        public int MedicalRecordID { get; set; }
        public Patient? Patient { get; set; }
        public List<LabResult> Results { get; set; } = new List<LabResult>();
        public int PrimaryLabResultID { get; set; } 
    }
}