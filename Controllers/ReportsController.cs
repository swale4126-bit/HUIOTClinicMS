using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.Models;
using HUIOTClinicMS.Models.Enums;
using HUIOTClinicMS.ViewModels;

namespace HUIOTClinicMS.Controllers
{
    [Authorize(Roles = "HealthOfficer,Nurse")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ReportsController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate)
        {
            var vm = new ReportsViewModel
            {
                StartDate = startDate ?? DateTime.Today,
                EndDate = endDate ?? DateTime.Today,

                TotalPatients = await _context.Patients.CountAsync(),
                TotalMedicalRecords = await _context.MedicalRecords.CountAsync(),
                MedicalRecordsThisMonth = await _context.MedicalRecords
                    .CountAsync(m => m.DateTime.Month == DateTime.Today.Month && m.DateTime.Year == DateTime.Today.Year),

                LabRequestsPending = await _context.LabRequests.CountAsync(l => l.Status == LabRequestStatuses.Pending),
                LabRequestsInProgress = await _context.LabRequests.CountAsync(l => l.Status == LabRequestStatuses.InProgress),
                LabRequestsCompleted = await _context.LabRequests.CountAsync(l => l.Status == LabRequestStatuses.Completed),

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

        private async Task<DetailedReportViewModel> GetDetailedReportDataAsync(DateTime startDate, DateTime endDate, string title, string periodKey)
        {
            // 1. Clinical Encounters in period
            var encounters = await _context.ClinicalEncounters
                .Include(e => e.Patient)
                    .ThenInclude(p => p!.Department)
                .Include(e => e.Patient)
                    .ThenInclude(p => p!.DormBlock)
                .Include(e => e.TriageAssessment)
                .Include(e => e.AssignedToStaff)
                .Include(e => e.ReferralRecord)
                .Where(e => e.StartTime >= startDate && e.StartTime < endDate)
                .OrderBy(e => e.StartTime)
                .ToListAsync();

            // 2. Medical Records in period
            var medicalRecords = await _context.MedicalRecords
                .Include(m => m.Patient)
                .Where(m => m.DateTime >= startDate && m.DateTime < endDate)
                .ToListAsync();

            // 3. Lab Requests in period
            var labRequests = await _context.LabRequests
                .Include(l => l.TestTypes)
                .Include(l => l.Patient)
                .Where(l => l.RequestDate >= startDate && l.RequestDate < endDate)
                .ToListAsync();

            var medRecordIds = medicalRecords.Select(m => m.ID).ToList();
            var labResults = await _context.LabResults
                .Include(lr => lr.TestType)
                .Where(lr => medRecordIds.Contains(lr.MedicalRecordID))
                .ToListAsync();

            // 4. Prescriptions in period
            var prescriptions = await _context.Prescriptions
                .Include(p => p.Medication)
                .Include(p => p.Patient)
                .Where(p => p.TimeDate >= startDate && p.TimeDate < endDate)
                .ToListAsync();

            // 5. Referrals in period
            var referrals = await _context.ReferralRecords
                .Include(r => r.Encounter)
                    .ThenInclude(e => e!.Patient)
                .Where(r => r.CreatedAt >= startDate && r.CreatedAt < endDate)
                .ToListAsync();

            // 6. New patients registered in period
            var newPatients = await _context.Patients
                .Include(p => p.Department)
                .Include(p => p.DormBlock)
                .Where(p => p.CreatedAt >= startDate && p.CreatedAt < endDate)
                .ToListAsync();

            // Aggregate all unique patient IDs with activity
            var allPatientIds = encounters.Select(e => e.PatientID)
                .Union(medicalRecords.Select(m => m.PatientID))
                .Union(labRequests.Select(l => l.PatientID))
                .Union(prescriptions.Select(p => p.PatientID))
                .Union(referrals.Where(r => r.Encounter != null).Select(r => r.Encounter!.PatientID))
                .Union(newPatients.Select(p => p.ID))
                .Distinct()
                .ToList();

            var patientsMap = await _context.Patients
                .Include(p => p.Department)
                .Include(p => p.DormBlock)
                .Where(p => allPatientIds.Contains(p.ID))
                .ToDictionaryAsync(p => p.ID);

            var reportVm = new DetailedReportViewModel
            {
                ReportType = title,
                PeriodKey = periodKey,
                GeneratedDate = DateTime.Now,
                StartDate = startDate,
                EndDate = endDate,
                TotalVisits = allPatientIds.Count,
                EmergencyVisits = encounters.Count(e => e.Type == EncounterType.Emergency || e.Status == WorkflowState.InEmergency),
                RoutineVisits = encounters.Count(e => e.Type != EncounterType.Emergency && e.Status != WorkflowState.InEmergency),
                LabTestsCount = labRequests.SelectMany(l => l.TestTypes).Count(),
                PrescriptionsCount = prescriptions.Count,
                ReferralsCount = referrals.Count
            };

            foreach (var pid in allPatientIds)
            {
                if (!patientsMap.TryGetValue(pid, out var patient)) continue;

                var patientEncounters = encounters.Where(e => e.PatientID == pid).ToList();
                var primaryEncounter = patientEncounters.OrderByDescending(e => e.StartTime).FirstOrDefault();
                var patientMedRecords = medicalRecords.Where(m => m.PatientID == pid).ToList();
                var patientLabRequests = labRequests.Where(l => l.PatientID == pid).ToList();
                var pMedIds = patientMedRecords.Select(m => m.ID).ToList();
                var patientLabResults = labResults.Where(lr => pMedIds.Contains(lr.MedicalRecordID)).ToList();
                var patientPrescriptions = prescriptions.Where(pr => pr.PatientID == pid).ToList();
                var patientReferral = referrals.FirstOrDefault(r => r.Encounter != null && r.Encounter.PatientID == pid) ?? primaryEncounter?.ReferralRecord;

                var visitDate = primaryEncounter?.StartTime ?? 
                                patientMedRecords.FirstOrDefault()?.DateTime ?? 
                                patient.CreatedAt;

                reportVm.PatientsData.Add(new DetailedPatientReportDto
                {
                    Patient = patient,
                    VisitDate = visitDate,
                    Encounter = primaryEncounter,
                    Triage = primaryEncounter?.TriageAssessment,
                    MedicalRecords = patientMedRecords,
                    LabRequests = patientLabRequests,
                    LabResults = patientLabResults,
                    Prescriptions = patientPrescriptions,
                    Referral = patientReferral
                });
            }

            // Sort by visit date descending
            reportVm.PatientsData = reportVm.PatientsData.OrderByDescending(d => d.VisitDate).ToList();

            return reportVm;
        }

        // GET: Reports/DailyReportPdf?startDate=...&endDate=...
        public async Task<IActionResult> DailyReportPdf(DateTime? startDate, DateTime? endDate)
        {
            DateTime start;
            DateTime end;
            string title;

            if (startDate.HasValue && endDate.HasValue)
            {
                start = startDate.Value.Date;
                var rawEnd = endDate.Value.Date;
                if (start > rawEnd)
                {
                    var temp = start;
                    start = rawEnd;
                    rawEnd = temp;
                }
                end = rawEnd.AddDays(1);
                title = start == rawEnd
                    ? $"Daily Clinical Activity & Health Statistics Report ({start:MMMM dd, yyyy})"
                    : $"Daily Clinical Activity & Health Statistics Report ({start:MMM dd, yyyy} - {rawEnd:MMM dd, yyyy})";
            }
            else if (startDate.HasValue)
            {
                start = startDate.Value.Date;
                end = start.AddDays(1);
                title = $"Daily Clinical Activity & Health Statistics Report ({start:MMMM dd, yyyy})";
            }
            else
            {
                start = DateTime.Today;
                end = start.AddDays(1);
                title = $"Daily Clinical Activity & Health Statistics Report ({DateTime.Today:MMMM dd, yyyy})";
            }

            var data = await GetDetailedReportDataAsync(start, end, title, "daily");
            return View("DetailedReportPdf", data);
        }

        // GET: Reports/MonthlyReportPdf?startDate=...&endDate=...
        public async Task<IActionResult> MonthlyReportPdf(DateTime? startDate, DateTime? endDate)
        {
            DateTime start;
            DateTime end;
            string title;

            if (startDate.HasValue && endDate.HasValue)
            {
                start = startDate.Value.Date;
                var rawEnd = endDate.Value.Date;
                if (start > rawEnd)
                {
                    var temp = start;
                    start = rawEnd;
                    rawEnd = temp;
                }
                end = rawEnd.AddDays(1);
                title = $"Monthly Clinical Activity & Health Statistics Report ({start:MMM dd, yyyy} - {rawEnd:MMM dd, yyyy})";
            }
            else if (startDate.HasValue)
            {
                start = new DateTime(startDate.Value.Year, startDate.Value.Month, 1);
                end = start.AddMonths(1);
                title = $"Monthly Clinical Activity & Health Statistics Report ({start:MMMM yyyy})";
            }
            else
            {
                start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                end = start.AddMonths(1);
                title = $"Monthly Clinical Activity & Health Statistics Report ({DateTime.Today:MMMM yyyy})";
            }

            var data = await GetDetailedReportDataAsync(start, end, title, "monthly");
            return View("DetailedReportPdf", data);
        }

        // GET: Reports/AnnualReportPdf?startDate=...&endDate=...
        public async Task<IActionResult> AnnualReportPdf(DateTime? startDate, DateTime? endDate)
        {
            DateTime start;
            DateTime end;
            string title;

            if (startDate.HasValue && endDate.HasValue)
            {
                start = startDate.Value.Date;
                var rawEnd = endDate.Value.Date;
                if (start > rawEnd)
                {
                    var temp = start;
                    start = rawEnd;
                    rawEnd = temp;
                }
                end = rawEnd.AddDays(1);
                title = $"Annual Clinical Activity & Health Statistics Report ({start:MMM dd, yyyy} - {rawEnd:MMM dd, yyyy})";
            }
            else if (startDate.HasValue)
            {
                start = new DateTime(startDate.Value.Year, 1, 1);
                end = start.AddYears(1);
                title = $"Annual Clinical Activity & Health Statistics Report ({start.Year})";
            }
            else
            {
                start = new DateTime(DateTime.Today.Year, 1, 1);
                end = start.AddYears(1);
                title = $"Annual Clinical Activity & Health Statistics Report ({DateTime.Today.Year})";
            }

            var data = await GetDetailedReportDataAsync(start, end, title, "annual");
            return View("DetailedReportPdf", data);
        }

        // GET: Reports/ExportDocx?period=daily|monthly|annual&startDate=...&endDate=...
        public async Task<IActionResult> ExportDocx(string? period, DateTime? startDate, DateTime? endDate)
        {
            DateTime start;
            DateTime end;
            string title;
            string filePrefix;
            var normPeriod = (period ?? "daily").ToLower();

            if (startDate.HasValue && endDate.HasValue)
            {
                start = startDate.Value.Date;
                var rawEnd = endDate.Value.Date;
                if (start > rawEnd)
                {
                    var temp = start;
                    start = rawEnd;
                    rawEnd = temp;
                }
                end = rawEnd.AddDays(1);
                var categoryName = char.ToUpper(normPeriod[0]) + (normPeriod.Length > 1 ? normPeriod.Substring(1) : "");
                title = start == rawEnd
                    ? $"{categoryName} Clinical Activity & Health Statistics Report ({start:MMM dd, yyyy})"
                    : $"{categoryName} Clinical Activity & Health Statistics Report ({start:MMM dd, yyyy} - {rawEnd:MMM dd, yyyy})";
                filePrefix = $"{categoryName}_Report_{start:yyyyMMdd}_{rawEnd:yyyyMMdd}";
            }
            else
            {
                if (normPeriod == "monthly")
                {
                    start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    end = start.AddMonths(1);
                    title = $"Monthly Clinical Activity & Health Statistics Report ({DateTime.Today:MMMM yyyy})";
                    filePrefix = $"Monthly_Report_{DateTime.Today:yyyy_MM}";
                }
                else if (normPeriod == "annual")
                {
                    start = new DateTime(DateTime.Today.Year, 1, 1);
                    end = start.AddYears(1);
                    title = $"Annual Clinical Activity & Health Statistics Report ({DateTime.Today.Year})";
                    filePrefix = $"Annual_Report_{DateTime.Today.Year}";
                }
                else
                {
                    start = DateTime.Today;
                    end = start.AddDays(1);
                    title = $"Daily Clinical Activity & Health Statistics Report ({DateTime.Today:MMM dd, yyyy})";
                    filePrefix = $"Daily_Report_{DateTime.Today:yyyy_MM_dd}";
                }
            }

            var data = await GetDetailedReportDataAsync(start, end, title, normPeriod);
            var wordBytes = GenerateWordDocumentHtml(data);

            return File(wordBytes, "application/vnd.ms-word", $"{filePrefix}.doc");
        }

        private byte[] GenerateWordDocumentHtml(DetailedReportViewModel model)
        {
            string logoBase64 = "";
            var logoPath = Path.Combine(_env.WebRootPath, "images", "hu-logo.png");
            if (System.IO.File.Exists(logoPath))
            {
                logoBase64 = Convert.ToBase64String(System.IO.File.ReadAllBytes(logoPath));
            }

            var sb = new StringBuilder();
            sb.Append(@"<html xmlns:o='urn:schemas-microsoft-com:office:office' xmlns:w='urn:schemas-microsoft-com:office:word' xmlns='http://www.w3.org/TR/REC-html40'>
<head>
    <meta charset='utf-8'>
    <title>" + model.ReportType + @"</title>
    <!--[if gte mso 9]>
    <xml>
        <w:WordDocument>
            <w:View>Print</w:View>
            <w:Zoom>100</w:Zoom>
            <w:DoNotOptimizeForBrowser/>
        </w:WordDocument>
    </xml>
    <![endif]-->
    <style>
        body { font-family: 'Calibri', Arial, sans-serif; font-size: 10pt; color: #1e293b; margin: 15mm; }
        .header-table { width: 100%; border: none; margin-bottom: 15pt; }
        .header-table td { border: none; padding: 4pt; }
        .report-title { font-size: 15pt; font-weight: bold; color: #002D59; text-align: center; }
        .report-sub { font-size: 11pt; color: #475569; text-align: center; font-weight: bold; }
        .kpi-table { width: 100%; border-collapse: collapse; margin-bottom: 15pt; }
        .kpi-table th { background-color: #f1f5f9; color: #0f172a; border: 1pt solid #cbd5e1; font-size: 9pt; text-align: center; padding: 6pt; }
        .kpi-table td { border: 1pt solid #cbd5e1; font-size: 13pt; font-weight: bold; text-align: center; padding: 8pt; color: #002D59; }
        .register-table { width: 100%; border-collapse: collapse; margin-bottom: 20pt; }
        .register-table th { background-color: #002D59; color: #ffffff; border: 1pt solid #0f172a; padding: 5pt; font-size: 8.5pt; text-align: left; }
        .register-table td { border: 1pt solid #cbd5e1; padding: 4pt 6pt; font-size: 8pt; vertical-align: top; }
        .register-table tr:nth-child(even) { background-color: #f8fafc; }
        .badge { display: inline-block; padding: 2pt 4pt; border-radius: 3pt; font-size: 7.5pt; font-weight: bold; }
        .badge-danger { background-color: #fee2e2; color: #b91c1c; }
        .badge-success { background-color: #dcfce7; color: #15803d; }
        .sign-table { width: 100%; border: none; margin-top: 30pt; }
        .sign-table td { border: none; padding: 10pt; font-size: 9pt; }
    </style>
</head>
<body>
    <table class='header-table'>
        <tr>
            <td style='width: 80pt; text-align: center;'>");

            if (!string.IsNullOrEmpty(logoBase64))
            {
                sb.Append("<img src='data:image/png;base64," + logoBase64 + "' style='width: 65pt; height: 65pt;' />");
            }

            sb.Append(@"</td>
            <td>
                <div class='report-title'>HAWASSA UNIVERSITY</div>
                <div class='report-sub'>INSTITUTE OF TECHNOLOGY (IoT) STUDENT CLINIC</div>
                <div style='text-align: center; font-size: 9pt; color: #64748b;'>Official Comprehensive Clinical Attendance & Health Statistics Register</div>
                <div style='text-align: center; font-size: 11pt; font-weight: bold; color: #0f172a; margin-top: 4pt;'>" + model.ReportType + @"</div>
                <div style='text-align: center; font-size: 8.5pt; color: #64748b;'>Period: " + model.StartDate.ToString("yyyy-MM-dd") + @" to " + model.EndDate.AddDays(-1).ToString("yyyy-MM-dd") + @" &bull; Generated: " + model.GeneratedDate.ToString("yyyy-MM-dd HH:mm") + @"</div>
            </td>
        </tr>
    </table>

    <table class='kpi-table'>
        <tr>
            <th>Total Patient Visits</th>
            <th>Routine Consultations</th>
            <th>Emergency Cases</th>
            <th>Diagnostic Lab Tests</th>
            <th>Prescriptions Dispensed</th>
            <th>Hospital Referrals (HUCSH)</th>
        </tr>
        <tr>
            <td>" + model.TotalVisits + @"</td>
            <td>" + model.RoutineVisits + @"</td>
            <td>" + model.EmergencyVisits + @"</td>
            <td>" + model.LabTestsCount + @"</td>
            <td>" + model.PrescriptionsCount + @"</td>
            <td>" + model.ReferralsCount + @"</td>
        </tr>
    </table>

    <div style='font-size: 10pt; font-weight: bold; color: #002D59; margin-bottom: 6pt;'>MASTER PATIENT CLINICAL ATTENDANCE REGISTER</div>
    <table class='register-table'>
        <thead>
            <tr>
                <th style='width: 25pt;'>#</th>
                <th style='width: 65pt;'>Date/Time</th>
                <th style='width: 80pt;'>Student ID & Card</th>
                <th style='width: 95pt;'>Patient Name & Dept</th>
                <th style='width: 45pt;'>Sex / Age</th>
                <th style='width: 90pt;'>Vitals / Complaint</th>
                <th style='width: 85pt;'>Diagnosis</th>
                <th style='width: 80pt;'>Lab Diagnostics</th>
                <th style='width: 85pt;'>Prescription</th>
                <th style='width: 65pt;'>Disposition</th>
            </tr>
        </thead>
        <tbody>");

            int idx = 1;
            foreach (var item in model.PatientsData)
            {
                var p = item.Patient;
                var triageText = item.Triage != null 
                    ? $"BP: {item.Triage.BloodPressure ?? "—"}, HR: {item.Triage.HeartRate?.ToString() ?? "—"}, Temp: {item.Triage.Temperature?.ToString() ?? "—"}°C" 
                    : "—";

                var complaints = item.MedicalRecords.Any(m => !string.IsNullOrEmpty(m.ChiefComplaint))
                    ? string.Join("; ", item.MedicalRecords.Select(m => m.ChiefComplaint).Distinct())
                    : (item.Encounter?.Type == EncounterType.Emergency ? "STAT Emergency evaluation" : "Routine clinical consultation");

                var diagnoses = item.MedicalRecords.Any(m => !string.IsNullOrEmpty(m.Diagnosis))
                    ? string.Join("; ", item.MedicalRecords.Select(m => m.Diagnosis).Distinct())
                    : "Under diagnostic review";

                var labs = item.LabResults.Any()
                    ? string.Join("; ", item.LabResults.Select(r => $"{r.TestType?.TestName}: {r.Result}"))
                    : (item.LabRequests.Any() ? $"{item.LabRequests.SelectMany(l => l.TestTypes).Count()} test(s) ordered" : "None");

                var meds = item.Prescriptions.Any()
                    ? string.Join("; ", item.Prescriptions.Select(pr => !string.IsNullOrEmpty(pr.Medication?.MedicationName) ? $"{pr.Medication.MedicationName} ({pr.Medication.PrescribedDose ?? pr.RX})" : pr.RX))
                    : "None";

                var disposition = item.Referral != null 
                    ? $"Referred to HUCSH" 
                    : (item.Encounter?.Status == WorkflowState.Discharged ? "Discharged" : "Under Care");

                sb.Append($@"
            <tr>
                <td>{idx++}</td>
                <td>{item.VisitDate:MM-dd HH:mm}</td>
                <td><b>{p.IDNumber}</b><br/><span style='color:#64748b;'>{p.CardNumber ?? "—"}</span></td>
                <td><b>{p.FirstName} {p.LastName}</b><br/><span style='color:#64748b;'>{p.Department?.DepartmentName ?? "—"}</span></td>
                <td>{p.Gender}<br/>{(DateTime.Today.Year - p.DateOfBirth.Year)} yrs</td>
                <td>{complaints}<br/><small style='color:#64748b;'>{triageText}</small></td>
                <td><b>{diagnoses}</b></td>
                <td>{labs}</td>
                <td>{meds}</td>
                <td><b>{disposition}</b></td>
            </tr>");
            }

            if (!model.PatientsData.Any())
            {
                sb.Append("<tr><td colspan='10' style='text-align:center; padding: 20pt; color: #64748b;'>No clinical patient visits recorded for this period.</td></tr>");
            }

            sb.Append(@"
        </tbody>
    </table>

    <table class='sign-table'>
        <tr>
            <td style='width: 33%; text-align: center;'>
                _______________________________<br/>
                <b>Prepared By</b><br/>
                <span style='color:#64748b;'>Attending Clinician (Nurse / HO)</span>
            </td>
            <td style='width: 33%; text-align: center;'>
                _______________________________<br/>
                <b>Verified By</b><br/>
                <span style='color:#64748b;'>Clinical Supervisor</span>
            </td>
            <td style='width: 34%; text-align: center;'>
                _______________________________<br/>
                <b>Approved By & Clinic Stamp</b><br/>
                <span style='color:#64748b;'>HU IoT Health Services Director</span>
            </td>
        </tr>
    </table>
</body>
</html>");

            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }
}