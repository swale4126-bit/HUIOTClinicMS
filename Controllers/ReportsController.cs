using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.ViewModels;

namespace HUIOTClinicMS.Controllers
{
    [Authorize(Roles = "HealthOfficer")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var vm = new ReportsViewModel
            {
                TotalPatients = await _context.Patients.CountAsync(),
                TotalMedicalRecords = await _context.MedicalRecords.CountAsync(),
                MedicalRecordsThisMonth = await _context.MedicalRecords
                    .CountAsync(m => m.DateTime.Month == DateTime.Today.Month && m.DateTime.Year == DateTime.Today.Year),

                LabRequestsPending = await _context.LabRequests.CountAsync(l => l.Status == "Pending"),
                LabRequestsInProgress = await _context.LabRequests.CountAsync(l => l.Status == "In Progress"),
                LabRequestsCompleted = await _context.LabRequests.CountAsync(l => l.Status == "Completed"),

                PrescriptionsPending = await _context.Prescriptions.CountAsync(p => p.Status == "Pending"),
                PrescriptionsDispensed = await _context.Prescriptions.CountAsync(p => p.Status == "Dispensed"),

                TotalMedicationsDispensed = await _context.Medications.CountAsync(),

                PatientsByDepartment = await _context.Patients
                    .GroupBy(p => p.Department != null ? p.Department.DepartmentName : "—")
                    .Select(g => new DepartmentPatientCount { DepartmentName = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count)
                    .ToListAsync(),
                    
                DailyPatients = await _context.Patients
                    .Include(p => p.Department)
                    .Where(p => p.CreatedAt.Date == DateTime.Today)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync(),
                    
                MonthlyPatients = await _context.Patients
                    .Include(p => p.Department)
                    .Where(p => p.CreatedAt.Month == DateTime.Today.Month && p.CreatedAt.Year == DateTime.Today.Year)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync()
            };

            return View(vm);
        }

        private async Task<DetailedReportViewModel> GetDetailedReportDataAsync(DateTime startDate, DateTime endDate, string title)
        {
            var patients = await _context.Patients
                .Include(p => p.Department)
                .Where(p => p.CreatedAt >= startDate && p.CreatedAt < endDate)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var patientIds = patients.Select(p => p.ID).ToList();

            var medicalRecords = await _context.MedicalRecords
                .Where(m => patientIds.Contains(m.PatientID))
                .ToListAsync();

            var medicalRecordIds = medicalRecords.Select(m => m.ID).ToList();

            var labRequests = await _context.LabRequests
                .Include(l => l.TestTypes)
                .Where(l => patientIds.Contains(l.PatientID))
                .ToListAsync();

            var labResults = await _context.LabResults
                .Include(lr => lr.TestType)
                .Where(lr => medicalRecordIds.Contains(lr.MedicalRecordID))
                .ToListAsync();

            var prescriptions = await _context.Prescriptions
                .Include(p => p.Medication)
                .Where(p => patientIds.Contains(p.PatientID))
                .ToListAsync();

            var dto = new DetailedReportViewModel
            {
                ReportType = title,
                GeneratedDate = DateTime.Now
            };

            foreach (var p in patients)
            {
                dto.PatientsData.Add(new DetailedPatientReportDto
                {
                    Patient = p,
                    MedicalRecords = medicalRecords.Where(m => m.PatientID == p.ID).ToList(),
                    LabRequests = labRequests.Where(l => l.PatientID == p.ID).ToList(),
                    LabResults = labResults.Where(lr => medicalRecords.Where(m => m.PatientID == p.ID).Select(m => m.ID).Contains(lr.MedicalRecordID)).ToList(),
                    Prescriptions = prescriptions.Where(pr => pr.PatientID == p.ID).ToList()
                });
            }

            return dto;
        }

        public async Task<IActionResult> DailyReportPdf()
        {
            var startDate = DateTime.Today;
            var endDate = startDate.AddDays(1);
            var data = await GetDetailedReportDataAsync(startDate, endDate, $"Daily Detailed Report ({DateTime.Today:MMM dd, yyyy})");
            return View("DetailedReportPdf", data);
        }

        public async Task<IActionResult> MonthlyReportPdf()
        {
            var startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var endDate = startDate.AddMonths(1);
            var data = await GetDetailedReportDataAsync(startDate, endDate, $"Monthly Detailed Report ({DateTime.Today:MMMM yyyy})");
            return View("DetailedReportPdf", data);
        }
    }
}