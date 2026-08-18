namespace HUIOTClinicMS.ViewModels
{
    public class DepartmentPatientCount
    {
        public string DepartmentName { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class ReportsViewModel
    {
        public int TotalPatients { get; set; }
        public int TotalMedicalRecords { get; set; }
        public int MedicalRecordsThisMonth { get; set; }

        public int LabRequestsPending { get; set; }
        public int LabRequestsInProgress { get; set; }
        public int LabRequestsCompleted { get; set; }

        public int PrescriptionsPending { get; set; }
        public int PrescriptionsDispensed { get; set; }

        public int TotalMedicationsDispensed { get; set; }

        public List<DepartmentPatientCount> PatientsByDepartment { get; set; } = new();

        public List<HUIOTClinicMS.Models.Patient> DailyPatients { get; set; } = new();
        public List<HUIOTClinicMS.Models.Patient> MonthlyPatients { get; set; } = new();
    }
}