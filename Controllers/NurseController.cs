using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.Models;
using HUIOTClinicMS.Models.Enums;
using HUIOTClinicMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HUIOTClinicMS.Controllers
{
    [Authorize(Roles = "Nurse")]
    public class NurseController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NurseController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // True dynamic counts:
            // 1. Patients registered today using local-day boundary [today, tomorrow)
            var patientsRegisteredToday = await _context.Patients
                .CountAsync(p => p.CreatedAt >= today && p.CreatedAt < tomorrow);

            // 2. Total registered patients in directory
            var totalPatients = await _context.Patients.CountAsync();

            var currentUser = await _userManager.GetUserAsync(User);
            var myStaffId = currentUser?.StaffID;

            // Query incoming handovers assigned to this nurse
            var incomingHandovers = new List<NurseHandoverItem>();
            if (myStaffId.HasValue)
            {
                var handovers = await _context.TaskAssignmentHistories
                    .Include(h => h.AssignedFromStaff)
                    .Where(h => h.TaskType == "PatientHandover" && h.AssignedToStaffID == myStaffId.Value && h.AssignedAt >= today)
                    .OrderByDescending(h => h.AssignedAt)
                    .ToListAsync();

                var patientHandoverIds = handovers.Select(h => h.TaskID).Distinct().ToList();
                var patients = await _context.Patients
                    .Include(p => p.Department)
                    .Include(p => p.DormBlock)
                    .Where(p => patientHandoverIds.Contains(p.ID))
                    .ToDictionaryAsync(p => p.ID);

                var handoverEncounters = await _context.ClinicalEncounters
                    .Where(e => patientHandoverIds.Contains(e.PatientID) && e.Status != WorkflowState.Discharged && e.Status != WorkflowState.Transferred)
                    .ToListAsync();

                var handoverEncountersMap = handoverEncounters
                    .GroupBy(e => e.PatientID)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.StartTime).First());

                foreach (var h in handovers)
                {
                    if (patients.TryGetValue(h.TaskID, out var p))
                    {
                        var hasActive = handoverEncountersMap.TryGetValue(p.ID, out var activeEnc);
                        incomingHandovers.Add(new NurseHandoverItem
                        {
                            HandoverID = h.ID,
                            PatientID = p.ID,
                            FullName = $"{p.FirstName} {p.LastName}",
                            CardNumber = p.CardNumber ?? "—",
                            IDNumber = p.IDNumber,
                            DepartmentName = p.Department?.DepartmentName ?? "—",
                            DormBlockName = p.DormBlock?.BlockName ?? "—",
                            HandoverFromStaffName = h.AssignedFromStaff != null ? $"{h.AssignedFromStaff.FirstName} {h.AssignedFromStaff.LastName}" : "Registration Desk",
                            HandoverTime = h.AssignedAt,
                            HandoverNote = h.Reason,
                            HasActiveEncounter = hasActive,
                            ActiveEncounterID = activeEnc?.ID,
                            IsEmergency = h.Reason != null && h.Reason.StartsWith("[STAT EMERGENCY]")
                        });
                    }
                }
            }

            // Query incoming laboratory result handovers assigned to this nurse
            var incomingLabHandovers = new List<NurseLabHandoverItem>();
            if (myStaffId.HasValue)
            {
                var labHandovers = await _context.TaskAssignmentHistories
                    .Include(h => h.AssignedFromStaff)
                    .Where(h => h.TaskType == "LabResultHandover" && h.AssignedToStaffID == myStaffId.Value && h.AssignedAt >= today)
                    .OrderByDescending(h => h.AssignedAt)
                    .ToListAsync();

                if (labHandovers.Any())
                {
                    var labRequestIds = labHandovers.Select(h => h.TaskID).Distinct().ToList();
                    var labRequests = await _context.LabRequests
                        .Include(l => l.Patient)
                            .ThenInclude(p => p!.Department)
                        .Include(l => l.TestTypes)
                        .Include(l => l.ClinicalEncounter)
                        .Where(l => labRequestIds.Contains(l.LabRequestID))
                        .ToDictionaryAsync(l => l.LabRequestID);

                    var handoverMedRecordIds = labRequests.Values.Select(l => l.MedicalRecordID).Distinct().ToList();
                    var labResults = await _context.LabResults
                        .Include(r => r.TestType)
                        .Where(r => handoverMedRecordIds.Contains(r.MedicalRecordID))
                        .ToListAsync();

                    var labResultsByMedRecord = labResults
                        .GroupBy(r => r.MedicalRecordID)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    foreach (var h in labHandovers)
                    {
                        if (labRequests.TryGetValue(h.TaskID, out var req))
                        {
                            var patient = req.Patient;
                            labResultsByMedRecord.TryGetValue(req.MedicalRecordID, out var resultsList);
                            resultsList ??= new List<LabResult>();

                            incomingLabHandovers.Add(new NurseLabHandoverItem
                            {
                                HandoverID = h.ID,
                                LabRequestID = req.LabRequestID,
                                MedicalRecordID = req.MedicalRecordID,
                                EncounterID = req.ClinicalEncounterID,
                                PatientID = req.PatientID,
                                FullName = patient != null ? $"{patient.FirstName} {patient.LastName}" : "Unknown Patient",
                                CardNumber = patient?.CardNumber ?? "—",
                                IDNumber = patient?.IDNumber ?? "—",
                                DepartmentName = patient?.Department?.DepartmentName ?? "—",
                                TestNames = req.TestTypes.Any() ? string.Join(", ", req.TestTypes.Select(t => t.TestName)) : "General Tests",
                                Results = resultsList,
                                HandedOverByTechnicianName = h.AssignedFromStaff != null ? $"{h.AssignedFromStaff.FirstName} {h.AssignedFromStaff.LastName}" : "Lab Technician",
                                CompletedTime = h.AssignedAt,
                                HandoverNote = h.Reason,
                                PrimaryLabResultID = resultsList.FirstOrDefault()?.ID ?? 0,
                                IsEmergency = req.ClinicalEncounter?.Type == EncounterType.Emergency
                            });
                        }
                    }
                }
            }

            // Query active queues assigned to Triage or Consultation for today
            var activeQueues = await _context.Queues
                .Include(q => q.Patient)
                    .ThenInclude(p => p.Department)
                .Include(q => q.ServicePoint)
                .Where(q => q.ServicePoint != null && (q.ServicePoint.Code == "TRI" || q.ServicePoint.Code == "CON") && q.QueueStatus != "Completed" && q.QueueStatus != "Cancelled" && q.CreatedAt >= today && q.CreatedAt < tomorrow)
                .OrderBy(q => q.CreatedAt)
                .ToListAsync();

            var patientIds = activeQueues.Select(q => q.PatientID).Distinct().ToList();
            var recentMedicalRecords = await _context.MedicalRecords
                .Where(m => patientIds.Contains(m.PatientID))
                .OrderByDescending(m => m.DateTime)
                .ToListAsync();

            var activeQueueEncounters = await _context.ClinicalEncounters
                .Include(e => e.AssignedToStaff)
                .Where(e => patientIds.Contains(e.PatientID) && e.Status != WorkflowState.Discharged && e.Status != WorkflowState.Transferred)
                .ToListAsync();

            var activeEncountersMap = activeQueueEncounters
                .GroupBy(e => e.PatientID)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.StartTime).First());

            var queue = activeQueues.Select(q => {
                var p = q.Patient;
                var mostRecentRecord = recentMedicalRecords.FirstOrDefault(m => m.PatientID == p.ID);
                activeEncountersMap.TryGetValue(p.ID, out var enc);
                var isAssignedToMe = myStaffId.HasValue && enc?.AssignedToStaffID == myStaffId.Value;
                var assignedStaffName = enc?.AssignedToStaff != null ? $"{enc.AssignedToStaff.FirstName} {enc.AssignedToStaff.LastName}" : null;
                var hasLabResults = q.QueueType == "Result Review" || enc?.Status == WorkflowState.WaitingConsultation;
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
                    QueueType = q.QueueType,
                    TokenNumber = q.TokenNumber,
                    ServicePointCode = q.ServicePoint?.Code ?? "TRI",
                    ActiveEncounterID = enc?.ID,
                    AssignedStaffId = enc?.AssignedToStaffID,
                    AssignedStaffName = assignedStaffName,
                    IsAssignedToMe = isAssignedToMe,
                    HasLabResultsReady = hasLabResults
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
                PatientsRegisteredToday = patientsRegisteredToday,
                PendingTriage = queue.Count(q => !q.HasMedicalRecordToday),
                TotalRecords = totalPatients,
                IncomingHandovers = incomingHandovers,
                IncomingLabHandovers = incomingLabHandovers,
                TodaysQueue = queue,
                RecentGroupedLabResults = groupedResults.Where(g => !prescribedMedicalRecordIds.Contains(g.MedicalRecordID)).ToList(),
                PrescribedPatients = groupedResults.Where(g => prescribedMedicalRecordIds.Contains(g.MedicalRecordID)).ToList()
            };

            return View(vm);
        }
    }
}