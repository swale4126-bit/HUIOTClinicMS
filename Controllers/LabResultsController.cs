
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HUIOTClinicMS.Models;
using HUIOTClinicMS.Models.Enums;
using HUIOTClinicMS.Data;

namespace HUIOTClinicMS.Controllers
{
    [Authorize]
    public class LabResultsController : Controller
    {
    private readonly ApplicationDbContext _context;
    private readonly HUIOTClinicMS.Services.INotificationService _notificationService;

    public LabResultsController(ApplicationDbContext context, HUIOTClinicMS.Services.INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    // GET: LABRESULTS
    public async Task<IActionResult> Index()    
    {
        var results = await _context.LabResults
            .Include(l => l.MedicalRecord)
                .ThenInclude(m => m.Patient)
            .Include(l => l.TestType)
            .Include(l => l.ResultPerformedBy)
            .ToListAsync();
        return View(results);
    }

    // GET: LABRESULTS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var labresult = await _context.LabResults
            .Include(l => l.MedicalRecord)
                .ThenInclude(m => m.Patient)
            .Include(l => l.TestType)
            .Include(l => l.ResultPerformedBy)
            .FirstOrDefaultAsync(m => m.ID == id);
        if (labresult == null)
        {
            return NotFound();
        }

        return View(labresult);
    }

    // GET: LABRESULTS/Create
    [Authorize(Roles = "LabTechnician")]
    public async Task<IActionResult> Create(int? labRequestId)
    {
        var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity!.Name);
        var myStaffId = currentUser?.StaffID;

        var baseQuery = _context.LabRequests
            .Include(l => l.Patient)
            .Include(l => l.RequestedBy)
            .Include(l => l.TestTypes)
            .Include(l => l.ClinicalEncounter)
            .Where(l => l.Status == LabRequestStatuses.Pending || l.Status == LabRequestStatuses.InProgress);

        if (myStaffId.HasValue)
        {
            // Only show laboratory requests assigned to the logged-in technician (or specific labRequestId if navigated directly)
            baseQuery = baseQuery.Where(l => l.AssignedToStaffID == myStaffId.Value || (labRequestId.HasValue && l.LabRequestID == labRequestId.Value));
        }

        var pendingRequests = await baseQuery
            .OrderByDescending(l => l.RequestDate)
            .ToListAsync();

        ViewBag.LabRequests = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
            pendingRequests.Select(l => new {
                ID = l.LabRequestID,
                Name = $"{l.Patient?.FirstName} {l.Patient?.LastName} - {l.RequestDate:MM/dd} ({l.TestTypes.Count} tests)"
            }), 
            "ID", "Name", labRequestId);

        var vm = new HUIOTClinicMS.ViewModels.LabResultCreateViewModel();
        var eligibleNurses = await GetEligibleNursesAsync();
        vm.EligibleNurses = eligibleNurses;
        
        if (labRequestId.HasValue)
        {
            vm.LabRequestID = labRequestId.Value;
            var req = pendingRequests.FirstOrDefault(r => r.LabRequestID == labRequestId.Value);
            if (req != null)
            {
                vm.AvailableTestTypes = req.TestTypes.ToList();
                vm.SelectedTestTypeIds = req.TestTypes.Select(t => t.ID).ToList();
                vm.PatientDisplayName = req.Patient != null ? $"{req.Patient.FirstName} {req.Patient.LastName} ({req.Patient.CardNumber ?? req.Patient.IDNumber})" : "—";
                vm.EncounterID = req.ClinicalEncounterID;

                // Smart pre-selection: default to requesting clinician if active, or current encounter assigned nurse
                if (req.RequestedBy != null && (req.RequestedBy.Role == "Nurse" || req.RequestedBy.Role == "HealthOfficer") && req.RequestedBy.IsActive && eligibleNurses.Any(n => n.Value == req.RequestedByID.ToString()))
                {
                    vm.ReceivingNurseStaffID = req.RequestedByID;
                }
                else if (req.ClinicalEncounter?.AssignedToStaffID.HasValue == true && eligibleNurses.Any(n => n.Value == req.ClinicalEncounter.AssignedToStaffID.Value.ToString()))
                {
                    vm.ReceivingNurseStaffID = req.ClinicalEncounter.AssignedToStaffID.Value;
                }
            }
        }
        else
        {
            vm.AvailableTestTypes = await _context.TestTypes.ToListAsync();
        }

        return View(vm);
    }

    // POST: LABRESULTS/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "LabTechnician")]
    public async Task<IActionResult> Create(HUIOTClinicMS.ViewModels.LabResultCreateViewModel vm)
    {
        var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity!.Name);
        if (currentUser?.StaffID == null)
        {
            ModelState.AddModelError(string.Empty, "Your account isn't linked to a Staff record, so results cannot be submitted.");
        }

        var labRequest = await _context.LabRequests
            .Include(l => l.Patient)
            .Include(l => l.TestTypes)
            .FirstOrDefaultAsync(l => l.LabRequestID == vm.LabRequestID);

        if (labRequest == null)
        {
            return NotFound();
        }

        // Concurrency / double-submit guard
        if (labRequest.Status == LabRequestStatuses.Completed)
        {
            TempData["ErrorMessage"] = "This laboratory request has already been completed and handed over.";
            return RedirectToAction("Index", "LabTechnician");
        }

        // Server-side validation of recipient clinician
        var receivingNurse = await (from s in _context.Staffs
                                    join u in _context.Users on s.ID equals u.StaffID
                                    where s.ID == vm.ReceivingNurseStaffID && s.IsActive && (s.Role == "Nurse" || s.Role == "HealthOfficer")
                                       && (u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow)
                                    select new { Staff = s, User = u })
                                    .FirstOrDefaultAsync();

        if (receivingNurse == null)
        {
            ModelState.AddModelError(nameof(vm.ReceivingNurseStaffID), "The selected receiving clinician is invalid, deactivated, or locked out.");
        }

        if (ModelState.IsValid)
        {
            ClinicalEncounter? encounter = null;
            if (labRequest.ClinicalEncounterID.HasValue)
            {
                encounter = await _context.ClinicalEncounters.FindAsync(labRequest.ClinicalEncounterID.Value);
            }

            LabResult? firstLabResult = null;
            foreach (var testTypeId in vm.SelectedTestTypeIds)
            {
                var resultText = vm.Results != null && vm.Results.ContainsKey(testTypeId) ? vm.Results[testTypeId] : string.Empty;
                var remarkText = vm.Remarks != null && vm.Remarks.ContainsKey(testTypeId) ? vm.Remarks[testTypeId] : string.Empty;

                var labResult = new LabResult
                {
                    MedicalRecordID = labRequest.MedicalRecordID,
                    TypeID = testTypeId,
                    Result = resultText,
                    Remark = remarkText,
                    ResultPerformedByID = currentUser!.StaffID!.Value,
                    Status = LabRequestStatuses.Completed
                };

                _context.Add(labResult);
                if (firstLabResult == null) firstLabResult = labResult;
            }
            
            // Mark LabRequest as Completed
            labRequest.Status = LabRequestStatuses.Completed;
            _context.Update(labRequest);

            // Re-route active clinical encounter to receiving nurse for consultation / follow-up
            if (encounter != null && encounter.Status != WorkflowState.Discharged)
            {
                encounter.Status = WorkflowState.WaitingConsultation;
                encounter.AssignedToStaffID = vm.ReceivingNurseStaffID;
            }

            // Persist handover audit record in TaskAssignmentHistory
            var history = new TaskAssignmentHistory
            {
                TaskType = "LabResultHandover",
                TaskID = labRequest.LabRequestID,
                AssignedFromStaffID = currentUser!.StaffID!.Value,
                AssignedToStaffID = vm.ReceivingNurseStaffID,
                AssignedByID = currentUser.Id,
                AssignedAt = DateTime.UtcNow,
                Reason = string.IsNullOrWhiteSpace(vm.HandoverNote) ? "Laboratory test completed; handed over for clinical review" : vm.HandoverNote.Trim()
            };
            _context.TaskAssignmentHistories.Add(history);

            await _context.SaveChangesAsync();

            // Complete any active Laboratory queue tickets for this patient
            var activeLabQueues = await _context.Queues
                .Include(q => q.ServicePoint)
                .Where(q => q.PatientID == labRequest.PatientID 
                         && q.ServicePoint != null && q.ServicePoint.Code == "LAB" 
                         && (q.QueueStatus == "InService" || q.QueueStatus == "Waiting" || q.QueueStatus == "Called"))
                .ToListAsync();

            foreach (var labQ in activeLabQueues)
            {
                labQ.QueueStatus = "Completed";
                var log = await _context.ServiceLogs.FirstOrDefaultAsync(l => l.QueueID == labQ.ID);
                if (log != null && log.ServiceEndedAt == null)
                {
                    log.ServiceEndedAt = DateTime.Now;
                }
            }

            // Close any old stale Triage or Consultation tickets for this patient so there are no queue conflicts
            var staleQueues = await _context.Queues
                .Include(q => q.ServicePoint)
                .Where(q => q.PatientID == labRequest.PatientID 
                         && q.ServicePoint != null && (q.ServicePoint.Code == "TRI" || q.ServicePoint.Code == "CON")
                         && (q.QueueStatus == "InService" || q.QueueStatus == "Waiting" || q.QueueStatus == "Called"))
                .ToListAsync();

            foreach (var sq in staleQueues)
            {
                sq.QueueStatus = "Completed";
            }

            // Automatically enqueue back to Triage & Consultation Service Point for Result Review
            var clinicalSp = await _context.ServicePoints.FirstOrDefaultAsync(s => s.Code == "TRI")
                          ?? await _context.ServicePoints.FirstOrDefaultAsync(s => s.Code == "CON");
            if (clinicalSp != null)
            {
                var todaysQueuesCount = await _context.Queues
                    .Where(q => q.ServicePointID == clinicalSp.ID && q.CreatedAt.Date == DateTime.Today)
                    .CountAsync();

                bool isEmergency = encounter != null && (encounter.Type == EncounterType.Emergency || encounter.Status == WorkflowState.InEmergency);

                var resultReviewQueue = new Queue
                {
                    TokenNumber = $"{clinicalSp.Code}-{todaysQueuesCount + 1:D3}",
                    PatientID = labRequest.PatientID,
                    ServicePointID = clinicalSp.ID,
                    PriorityLevel = isEmergency ? "Emergency" : "Normal",
                    QueueStatus = "Waiting",
                    QueueType = "Result Review",
                    CreatedAt = DateTime.Now
                };

                _context.Queues.Add(resultReviewQueue);
                await _context.SaveChangesAsync();
            }

            // Send direct targeted notification to the designated receiving nurse
            var patientInfo = labRequest.Patient != null ? $"{labRequest.Patient.FirstName} {labRequest.Patient.LastName} (Card No: {labRequest.Patient.CardNumber ?? labRequest.Patient.IDNumber})" : "Unknown Patient";
            var techName = currentUser.FullName ?? "Lab Technician";
            var noteSuffix = !string.IsNullOrWhiteSpace(vm.HandoverNote) ? $" Note: {vm.HandoverNote}." : "";

            await _notificationService.CreateDirectNotificationAsync(
                receivingNurse!.User.Id,
                "Lab Result Handover",
                $"Laboratory results for {patientInfo} have been completed by {techName} and handed over to you for clinical review.{noteSuffix}",
                "LabResult",
                firstLabResult?.ID);

            TempData["SuccessMessage"] = $"Lab results for {patientInfo} entered and handed over successfully to Nurse {receivingNurse.Staff.FirstName} {receivingNurse.Staff.LastName}.";
            return RedirectToAction("Index", "LabTechnician");
        }

        var queryPending = _context.LabRequests
            .Include(l => l.Patient)
            .Include(l => l.TestTypes)
            .Where(l => l.Status == LabRequestStatuses.Pending || l.Status == LabRequestStatuses.InProgress);

        if (currentUser?.StaffID != null)
        {
            queryPending = queryPending.Where(l => l.AssignedToStaffID == currentUser.StaffID.Value || l.LabRequestID == vm.LabRequestID);
        }

        var pendingRequests = await queryPending.ToListAsync();

        ViewBag.LabRequests = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
            pendingRequests.Select(l => new { ID = l.LabRequestID, Name = $"{l.Patient?.FirstName} {l.Patient?.LastName}" }), 
            "ID", "Name", vm.LabRequestID);

        vm.AvailableTestTypes = labRequest?.TestTypes != null && labRequest.TestTypes.Any() 
            ? labRequest.TestTypes.ToList() 
            : await _context.TestTypes.ToListAsync();
        vm.EligibleNurses = await GetEligibleNursesAsync();
        vm.PatientDisplayName = labRequest?.Patient != null ? $"{labRequest.Patient.FirstName} {labRequest.Patient.LastName} ({labRequest.Patient.CardNumber ?? labRequest.Patient.IDNumber})" : "—";

        return View(vm);
    }

    private async Task<List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>> GetEligibleNursesAsync()
    {
        var raw = await (from s in _context.Staffs
                         join u in _context.Users on s.ID equals u.StaffID
                         where s.IsActive && (s.Role == "Nurse" || s.Role == "HealthOfficer")
                            && (u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow)
                         orderby s.Role, s.FirstName, s.LastName
                         select s)
                         .ToListAsync();

        return raw
            .Where(s => !(s.FirstName == "Meron" && s.LastName == "Alemu")
                     && !(s.FirstName == "Tigist" && s.LastName == "Bekele")
                     && !(s.FirstName.ToLower() == "wale" && s.LastName.ToLower() == "solomon"))
            .GroupBy(s => $"{s.FirstName} {s.LastName}")
            .Select(g => g.First())
            .Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = s.ID.ToString(),
                Text = $"{s.FirstName} {s.LastName} ({s.Role} - Consultation / Triage)"
            })
            .ToList();
    }

    // GET: LABRESULTS/Edit/5
    [Authorize(Roles = "LabTechnician,HealthOfficer")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var labresult = await _context.LabResults.FindAsync(id);
        if (labresult == null)
        {
            return NotFound();
        }
        return View(labresult);
    }

    // POST: LABRESULTS/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "LabTechnician,HealthOfficer")]
    public async Task<IActionResult> Edit(int? id, [Bind("ID,MedicalRecordID,TypeID,Result,ResultPerformedByID,Remark,Status")] LabResult labresult)
    {
        if (id != labresult.ID)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(labresult);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!LabResultExists(labresult.ID))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(labresult);
    }

    // GET: LABRESULTS/Delete/5
    [Authorize(Roles = "HealthOfficer")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var labresult = await _context.LabResults
            .Include(m => m.TestType)
            .FirstOrDefaultAsync(m => m.ID == id);
        if (labresult == null)
        {
            return NotFound();
        }

        return View(labresult);
    }

    // POST: LABRESULTS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "HealthOfficer")]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var labresult = await _context.LabResults.FindAsync(id);
        if (labresult != null)
        {
            if (await _context.Prescriptions.AnyAsync(p => p.LabResultID == id))
            {
                TempData["ErrorMessage"] = "Cannot delete lab result because a prescription has been issued for it. Diagnostic history must be preserved.";
                return RedirectToAction(nameof(Index));
            }

            _context.LabResults.Remove(labresult);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Lab result removed.";
        }

        return RedirectToAction(nameof(Index));
    }

    private bool LabResultExists(int? id)
    {
        return _context.LabResults.Any(e => e.ID == id);
    }
}
}
