using HUIOTClinicMS.Models;

namespace HUIOTClinicMS.ViewModels
{
    public class DetailedPatientReportDto
    {
        public Patient Patient { get; set; } = default!;
        public DateTime VisitDate { get; set; }
        public ClinicalEncounter? Encounter { get; set; }
        public TriageAssessment? Triage { get; set; }
        public List<MedicalRecord> MedicalRecords { get; set; } = new();
        public List<LabRequest> LabRequests { get; set; } = new();
        public List<LabResult> LabResults { get; set; } = new();
        public List<Prescription> Prescriptions { get; set; } = new();
        public ReferralRecord? Referral { get; set; }
    }

    public class DetailedReportViewModel
    {
        public string ReportType { get; set; } = string.Empty;
        public string PeriodKey { get; set; } = "daily";
        public DateTime GeneratedDate { get; set; } = DateTime.Now;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public int TotalVisits { get; set; }
        public int EmergencyVisits { get; set; }
        public int RoutineVisits { get; set; }
        public int LabTestsCount { get; set; }
        public int PrescriptionsCount { get; set; }
        public int ReferralsCount { get; set; }

        public List<DetailedPatientReportDto> PatientsData { get; set; } = new();
    }
}
