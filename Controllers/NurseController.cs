using Microsoft.AspNetCore.Authorization;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HUIOTClinicMS.Controllers
{
    [Authorize(Roles = "Nurse")]
    public class NurseController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NurseController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;

            // Get total patients count to match Health Officer dashboard
            var totalPatients = await _context.Patients.CountAsync();

            // Query active queues assigned to Consultation
            var activeQueues = await _context.Queues
                .Include(q => q.Patient)
                    .ThenInclude(p => p.Department)
                .Where(q => q.ServicePoint != null && q.ServicePoint.Code == "CON" && q.QueueStatus != "Completed" && q.QueueStatus != "Cancelled")
                .OrderBy(q => q.CreatedAt)
                .ToListAsync();

            var patientIds = activeQueues.Select(q => q.PatientID).Distinct().ToList();
            var recentMedicalRecords = await _context.MedicalRecords
                .Where(m => patientIds.Contains(m.PatientID))
                .OrderByDescending(m => m.DateTime)
                .ToListAsync();

            var queue = activeQueues.Select(q => {
                var p = q.Patient;
                var mostRecentRecord = recentMedicalRecords.FirstOrDefault(m => m.PatientID == p.ID);
                return new NurseQueueItem
                {
                    PatientID = p.ID,
                    FullName = $"{p.FirstName} {p.LastName}",
                    CardNumber = p.CardNumber ?? "—",
                    DepartmentName = p.Department?.DepartmentName ?? "—",
                    TimeIn = q.CreatedAt,
                    HasMedicalRecordToday = mostRecentRecord != null && mostRecentRecord.DateTime.Date == today,
                    MedicalRecordIDToday = mostRecentRecord?.ID,
                    QueueStatus = q.QueueStatus,
                    QueueType = q.QueueType
                };
            }).ToList();

            var recentLabResults = await _context.LabResults
                .Include(r => r.MedicalRecord)
                    .ThenInclude(m => m.Patient)
                .Include(r => r.TestType)
                .OrderByDescending(r => r.ID)
                .Take(50)
                .ToListAsync();

            var groupedResults = recentLabResults
                .Where(r => r.MedicalRecord != null)
                .GroupBy(r => r.MedicalRecordID)
                .Select(g => new NurseGroupedLabResult
                {
                    MedicalRecordID = g.Key,
                    Patient = g.First().MedicalRecord?.Patient,
                    Results = g.ToList(),
                    PrimaryLabResultID = g.First().ID
                })
                .ToList();

            var medicalRecordIds = groupedResults.Select(g => g.MedicalRecordID).ToList();
            var prescribedMedicalRecordIds = await _context.Prescriptions
                .Include(p => p.LabResult)
                .Where(p => p.LabResult != null && medicalRecordIds.Contains(p.LabResult.MedicalRecordID))
                .Select(p => p.LabResult!.MedicalRecordID)
                .Distinct()
                .ToListAsync();

            var vm = new NurseDashboardViewModel
            {
                PatientsRegisteredToday = totalPatients,
                PendingTriage = queue.Count(q => !q.HasMedicalRecordToday),
                TotalRecords = await _context.MedicalRecords.CountAsync(),
                TodaysQueue = queue,
                RecentGroupedLabResults = groupedResults.Where(g => !prescribedMedicalRecordIds.Contains(g.MedicalRecordID)).ToList(),
                PrescribedPatients = groupedResults.Where(g => prescribedMedicalRecordIds.Contains(g.MedicalRecordID)).ToList()
            };

            return View(vm);
        }
    }
}