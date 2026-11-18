using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.Models;
using HUIOTClinicMS.Models.Enums;
using HUIOTClinicMS.ViewModels;

namespace HUIOTClinicMS.Controllers
{
    [Authorize(Roles = "HealthOfficer,Nurse,LabTechnician,Pharmacist")]
    public class QueueController : Controller
    {
        private readonly ApplicationDbContext _context;

        public QueueController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? spId, string? queueType)
        {
            var servicePoints = await _context.ServicePoints.Where(s => s.IsActive).ToListAsync();
            
            if (!servicePoints.Any())
            {
                return View(new QueueDashboardViewModel()); // No service points configured yet
            }

            if (User.IsInRole("Nurse"))
            {
                servicePoints = servicePoints.Where(s => s.Code == "REG" || s.Code == "TRI").ToList();
            }
            else if (User.IsInRole("LabTechnician"))
            {
                servicePoints = servicePoints.Where(s => s.Code == "LAB").ToList();
            }
            else if (User.IsInRole("Pharmacist"))
            {
                servicePoints = servicePoints.Where(s => s.Code == "PHA").ToList();
            }

            int? actualSpId = spId;

            if (actualSpId == null && !string.IsNullOrEmpty(queueType))
            {
                var matchingSp = servicePoints.FirstOrDefault(s => s.Name.Contains(queueType, StringComparison.OrdinalIgnoreCase)
                                                                || (queueType.Equals("Consultation", StringComparison.OrdinalIgnoreCase) && s.Code == "TRI")
                                                                || (queueType.Equals("Triage", StringComparison.OrdinalIgnoreCase) && s.Code == "TRI"));
                if (matchingSp != null)
                {
                    actualSpId = matchingSp.ID;
                }
            }
            
            if (actualSpId == null)
            {
                // For Health Officer, if spId is 0, we show all departments. If it's null, we also default to 0 for Health Officers.
                if (User.IsInRole("HealthOfficer"))
                {
                    actualSpId = 0;
                }
                else
                {
                    actualSpId = servicePoints.FirstOrDefault(s => s.Code == "TRI")?.ID ?? servicePoints.First().ID;
                }
            }

            var today = DateTime.Today;
            
            var query = _context.Queues
                .Include(q => q.Patient)
                .Where(q => q.CreatedAt >= today);

            if (actualSpId > 0)
            {
                var selectedSp = servicePoints.FirstOrDefault(s => s.ID == actualSpId);
                if (selectedSp != null && selectedSp.Code == "TRI")
                {
                    var conSpId = (await _context.ServicePoints.FirstOrDefaultAsync(s => s.Code == "CON"))?.ID;
                    query = conSpId.HasValue
                        ? query.Where(q => q.ServicePointID == actualSpId || q.ServicePointID == conSpId.Value)
                        : query.Where(q => q.ServicePointID == actualSpId);
                }
                else
                {
                    query = query.Where(q => q.ServicePointID == actualSpId);
                }
            }

            var queues = await query
                .OrderByDescending(q => q.PriorityLevel == "Emergency")
                .ThenByDescending(q => q.PriorityLevel == "High")
                .ThenBy(q => q.CreatedAt)
                .ToListAsync();

            var currentServing = actualSpId > 0 ? queues.FirstOrDefault(q => q.QueueStatus == "InService" || q.QueueStatus == "Called") : null;
            var waiting = actualSpId > 0 ? queues.Where(q => q.QueueStatus == "Waiting").ToList() : queues.Where(q => q.QueueStatus == "Waiting" || q.QueueStatus == "InService" || q.QueueStatus == "Called").ToList();

            int? currentServingEncounterId = null;
            int? currentServingLabRequestId = null;
            if (currentServing != null)
            {
                var enc = await _context.ClinicalEncounters
                    .Where(e => e.PatientID == currentServing.PatientID && e.Status != WorkflowState.Discharged && e.Status != WorkflowState.Transferred)
                    .OrderByDescending(e => e.StartTime)
                    .FirstOrDefaultAsync();
                currentServingEncounterId = enc?.ID;

                var labReq = await _context.LabRequests
                    .Where(l => l.PatientID == currentServing.PatientID && (l.Status == LabRequestStatuses.Pending || l.Status == LabRequestStatuses.InProgress))
                    .OrderByDescending(l => l.RequestDate)
                    .FirstOrDefaultAsync();
                currentServingLabRequestId = labReq?.LabRequestID;
            }

            var vm = new QueueDashboardViewModel
            {
                ServicePoints = servicePoints,
                SelectedServicePointID = actualSpId ?? 0,
                SelectedServicePoint = actualSpId > 0 ? servicePoints.FirstOrDefault(s => s.ID == actualSpId) : null,
                CurrentServingQueue = currentServing,
                CurrentServingEncounterID = currentServingEncounterId,
                CurrentServingLabRequestID = currentServingLabRequestId,
                WaitingQueues = waiting,
                WaitingCount = queues.Count(q => q.QueueStatus == "Waiting"),
                CalledCount = queues.Count(q => q.QueueStatus == "Called"),
                InServiceCount = queues.Count(q => q.QueueStatus == "InService"),
                CompletedCount = queues.Count(q => q.QueueStatus == "Completed")
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> CallNext(int servicePointId)
        {
            var today = DateTime.Today;

            var targetSp = await _context.ServicePoints.FindAsync(servicePointId);
            int? conSpId = (targetSp != null && targetSp.Code == "TRI")
                ? (await _context.ServicePoints.FirstOrDefaultAsync(s => s.Code == "CON"))?.ID
                : null;

            // Check if already serving someone
            var currentServing = await _context.Queues
                .Where(q => (q.ServicePointID == servicePointId || (conSpId.HasValue && q.ServicePointID == conSpId.Value))
                         && q.CreatedAt >= today && (q.QueueStatus == "InService" || q.QueueStatus == "Called"))
                .FirstOrDefaultAsync();

            if (currentServing != null)
            {
                currentServing.QueueStatus = "Completed";
                var prevLog = await _context.ServiceLogs.FirstOrDefaultAsync(l => l.QueueID == currentServing.ID && l.ServiceEndedAt == null);
                if (prevLog != null)
                {
                    prevLog.ServiceEndedAt = DateTime.Now;
                }
                await _context.SaveChangesAsync();
            }

            // Find next waiting (Emergency first, then High, then first-come-first-served)
            var nextQueue = await _context.Queues
                .Include(q => q.Patient)
                .Where(q => (q.ServicePointID == servicePointId || (conSpId.HasValue && q.ServicePointID == conSpId.Value))
                         && q.CreatedAt >= today && q.QueueStatus == "Waiting")
                .OrderByDescending(q => q.PriorityLevel == "Emergency")
                .ThenByDescending(q => q.PriorityLevel == "High")
                .ThenBy(q => q.CreatedAt)
                .FirstOrDefaultAsync();

            if (nextQueue != null)
            {
                // Atomic update directly to InService (removes intermediate Called state)
                int rowsAffected = await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE Queues SET QueueStatus = 'InService' WHERE ID = {0} AND QueueStatus = 'Waiting'", nextQueue.ID);

                if (rowsAffected > 0)
                {
                    var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity!.Name);
                    var log = new ServiceLog
                    {
                        QueueID = nextQueue.ID,
                        CalledAt = DateTime.Now,
                        ServiceStartedAt = DateTime.Now,
                        ServedByID = currentUser?.Id
                    };

                    _context.ServiceLogs.Add(log);
                    await _context.SaveChangesAsync();
                    
                    if (User.IsInRole("LabTechnician"))
                    {
                        var labRequest = await _context.LabRequests
                            .Where(l => l.PatientID == nextQueue.PatientID && (l.Status == LabRequestStatuses.Pending || l.Status == LabRequestStatuses.InProgress))
                            .OrderByDescending(l => l.RequestDate)
                            .FirstOrDefaultAsync();

                        TempData["SuccessMessage"] = $"Now serving Token {nextQueue.TokenNumber} ({nextQueue.Patient?.FirstName} {nextQueue.Patient?.LastName}). Please record findings and hand over to receiving nurse.";
                        if (labRequest != null)
                        {
                            return RedirectToAction("Create", "LabResults", new { labRequestId = labRequest.LabRequestID });
                        }
                        return RedirectToAction("Create", "LabResults");
                    }

                    TempData["SuccessMessage"] = $"Now serving Token {nextQueue.TokenNumber} ({nextQueue.Patient?.FirstName} {nextQueue.Patient?.LastName}). Opened chart.";

                    // Find or create active encounter and redirect directly to Encounter Workspace
                    var encounter = await _context.ClinicalEncounters
                        .Where(e => e.PatientID == nextQueue.PatientID && e.Status != WorkflowState.Discharged && e.Status != WorkflowState.Transferred)
                        .OrderByDescending(e => e.StartTime)
                        .FirstOrDefaultAsync();

                    if (encounter == null)
                    {
                        encounter = new ClinicalEncounter
                        {
                            PatientID = nextQueue.PatientID,
                            StartTime = DateTime.UtcNow,
                            Type = nextQueue.PriorityLevel == "Emergency" ? EncounterType.Emergency : EncounterType.Routine,
                            Status = nextQueue.PriorityLevel == "Emergency" ? WorkflowState.InEmergency : WorkflowState.Registered,
                            AssignedToStaffID = currentUser?.StaffID,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = User.Identity?.Name
                        };
                        _context.ClinicalEncounters.Add(encounter);
                        await _context.SaveChangesAsync();
                    }
                    else if (currentUser?.StaffID != null && encounter.AssignedToStaffID == null)
                    {
                        encounter.AssignedToStaffID = currentUser.StaffID;
                        await _context.SaveChangesAsync();
                    }

                    return RedirectToAction("Details", "ClinicalEncounters", new { id = encounter.ID });
                }
                else
                {
                    TempData["ErrorMessage"] = "The patient was already called by another staff member.";
                }
            }

            return RedirectToAction(nameof(Index), new { spId = servicePointId });
        }

        [HttpPost]
        public async Task<IActionResult> StartService(int queueId, int servicePointId)
        {
            var queue = await _context.Queues.FindAsync(queueId);
            if (queue != null && queue.QueueStatus == "Called")
            {
                queue.QueueStatus = "InService";
                
                var log = await _context.ServiceLogs.FirstOrDefaultAsync(l => l.QueueID == queueId);
                if (log != null)
                {
                    log.ServiceStartedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index), new { spId = servicePointId });
        }

        [HttpPost]
        public async Task<IActionResult> CompleteService(int queueId, int servicePointId)
        {
            var queue = await _context.Queues
                .Include(q => q.Patient)
                .Include(q => q.ServicePoint)
                .FirstOrDefaultAsync(q => q.ID == queueId);

            if (queue == null || (queue.QueueStatus != "Called" && queue.QueueStatus != "InService"))
            {
                return RedirectToAction(nameof(Index), new { spId = servicePointId });
            }

            var spCode = queue.ServicePoint?.Code ?? "";
            bool hasServiceGiven = false;
            string serviceRequirementMsg = "";

            if (spCode == "TRI" || spCode == "CON" || queue.QueueType == "Triage" || queue.QueueType == "Consultation" || queue.QueueType == "Routine" || queue.QueueType == "Emergency")
            {
                // Find active or recent encounter
                var encounter = await _context.ClinicalEncounters
                    .Where(e => e.PatientID == queue.PatientID && e.Status != WorkflowState.Discharged && e.Status != WorkflowState.Transferred)
                    .OrderByDescending(e => e.StartTime)
                    .FirstOrDefaultAsync();

                int? encounterId = encounter?.ID;
                var cutoffTime = queue.CreatedAt.AddHours(-2);

                // 1. Triage assessment / vitals recorded
                bool hasTriage = await _context.TriageAssessments
                    .AnyAsync(t => (encounterId.HasValue && t.EncounterID == encounterId.Value) || (t.Encounter != null && t.Encounter.PatientID == queue.PatientID && t.CreatedAt >= cutoffTime));

                // 2. Consultation record / clinical notes / diagnosis / treatment
                bool hasMedRecord = await _context.MedicalRecords
                    .AnyAsync(m => (encounterId.HasValue && m.EncounterID == encounterId.Value) || (m.PatientID == queue.PatientID && m.DateTime >= cutoffTime));

                // 3. Lab request ordered
                bool hasLabRequest = await _context.LabRequests
                    .AnyAsync(l => (encounterId.HasValue && l.ClinicalEncounterID == encounterId.Value) || (l.PatientID == queue.PatientID && l.RequestDate >= cutoffTime));

                // 4. Prescription ordered
                bool hasPrescription = await _context.Prescriptions
                    .AnyAsync(p => (encounterId.HasValue && p.ClinicalEncounterID == encounterId.Value) || (p.PatientID == queue.PatientID && p.TimeDate >= cutoffTime));

                // 5. Referral record issued
                bool hasReferral = await _context.ReferralRecords
                    .AnyAsync(r => (encounterId.HasValue && r.EncounterID == encounterId.Value) || (r.Encounter != null && r.Encounter.PatientID == queue.PatientID && r.CreatedAt >= cutoffTime));

                hasServiceGiven = hasTriage || hasMedRecord || hasLabRequest || hasPrescription || hasReferral;
                serviceRequirementMsg = $"Cannot complete token {queue.TokenNumber}: No clinical service (triage vitals, consultation notes/diagnosis, lab request, prescription, or referral) has been recorded for patient {queue.Patient?.FirstName} {queue.Patient?.LastName}. Please click 'Open Encounter Chart' to record clinical service, or click 'Cancel' if the patient did not attend.";
            }
            else if (spCode == "LAB" || queue.QueueType == "Laboratory")
            {
                var patientMedRecordIds = await _context.MedicalRecords
                    .Where(m => m.PatientID == queue.PatientID)
                    .Select(m => m.ID)
                    .ToListAsync();

                bool hasLabResult = await _context.LabResults
                    .AnyAsync(r => patientMedRecordIds.Contains(r.MedicalRecordID));

                hasServiceGiven = hasLabResult;
                serviceRequirementMsg = $"Cannot complete laboratory queue: No laboratory test results have been entered for patient {queue.Patient?.FirstName} {queue.Patient?.LastName}. Please enter findings and complete handover.";
            }
            else if (spCode == "PHA" || queue.QueueType == "Pharmacy")
            {
                bool hasDispensed = await _context.Prescriptions
                    .AnyAsync(p => p.PatientID == queue.PatientID && (p.Status == "Dispensed" || p.Status == "PartiallyDispensed"));

                hasServiceGiven = hasDispensed;
                serviceRequirementMsg = $"Cannot complete pharmacy queue: No prescription has been dispensed for patient {queue.Patient?.FirstName} {queue.Patient?.LastName}. Please dispense medication before completing.";
            }
            else
            {
                hasServiceGiven = true;
            }

            if (!hasServiceGiven)
            {
                TempData["ErrorMessage"] = serviceRequirementMsg;
                return RedirectToAction(nameof(Index), new { spId = servicePointId });
            }

            queue.QueueStatus = "Completed";

            var log = await _context.ServiceLogs.FirstOrDefaultAsync(l => l.QueueID == queueId && l.ServiceEndedAt == null);
            if (log != null)
            {
                log.ServiceEndedAt = DateTime.Now;
            }

            // Update clinical encounter if no ongoing lab or pharmacy is pending
            var activeEnc = await _context.ClinicalEncounters
                .Where(e => e.PatientID == queue.PatientID && e.Status != WorkflowState.Discharged && e.Status != WorkflowState.Transferred)
                .OrderByDescending(e => e.StartTime)
                .FirstOrDefaultAsync();

            if (activeEnc != null)
            {
                bool pendingLab = await _context.LabRequests.AnyAsync(l => l.PatientID == queue.PatientID && (l.Status == LabRequestStatuses.Pending || l.Status == LabRequestStatuses.InProgress));
                bool pendingRx = await _context.Prescriptions.AnyAsync(p => p.PatientID == queue.PatientID && p.Status == "Pending");

                if (!pendingLab && !pendingRx && activeEnc.Status != WorkflowState.InEmergency)
                {
                    activeEnc.Status = WorkflowState.Discharged;
                    activeEnc.EndTime = DateTime.UtcNow;
                    activeEnc.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Clinical service completed successfully for Token {queue.TokenNumber} ({queue.Patient?.FirstName} {queue.Patient?.LastName}).";

            return RedirectToAction(nameof(Index), new { spId = servicePointId });
        }
        
        [HttpPost]
        public async Task<IActionResult> CancelService(int queueId, int servicePointId)
        {
            var queue = await _context.Queues.FindAsync(queueId);
            if (queue != null && (queue.QueueStatus == "Called" || queue.QueueStatus == "InService"))
            {
                queue.QueueStatus = "Cancelled";

                var log = await _context.ServiceLogs.FirstOrDefaultAsync(l => l.QueueID == queueId);
                if (log != null)
                {
                    log.ServiceEndedAt = DateTime.Now; // Log when it was cancelled
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index), new { spId = servicePointId });
        }
    }
}
