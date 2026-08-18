using HUIOTClinicMS.Models;

namespace HUIOTClinicMS.ViewModels
{
    public class DetailedPatientReportDto
    {
        public Patient Patient { get; set; } = default!;
        public List<MedicalRecord> MedicalRecords { get; set; } = new();
        public List<LabRequest> LabRequests { get; set; } = new();
        public List<LabResult> LabResults { get; set; } = new();
        public List<Prescription> Prescriptions { get; set; } = new();
    }

    public class DetailedReportViewModel
    {
        public string ReportType { get; set; } = string.Empty;
        public DateTime GeneratedDate { get; set; } = DateTime.Now;
        public List<DetailedPatientReportDto> PatientsData { get; set; } = new();
    }
}
