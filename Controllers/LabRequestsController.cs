using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HUIOTClinicMS.Models;
using HUIOTClinicMS.Models.Enums;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.ViewModels;

public class LabRequestsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly HUIOTClinicMS.Services.INotificationService _notificationService;

    public LabRequestsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, HUIOTClinicMS.Services.INotificationService notificationService)
    {
        _context = context;
        _userManager = userManager;
        _notificationService = notificationService;
    }

    // GET: LABREQUESTS
    public async Task<IActionResult> Index(string? search)
    {
        var query = _context.LabRequests
            .Include(l => l.Patient)
            .Include(l => l.RequestedBy)
            .Include(l => l.AssignedToStaff)
            .Include(l => l.TestTypes)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(l => l.Patient != null &&
                (l.Patient.FirstName.ToLower().Contains(term) || l.Patient.LastName.ToLower().Contains(term)));
        }

        var requests = await query
            .OrderByDescending(l => l.RequestDate)
            .ToListAsync();

        ViewBag.SearchTerm = search;
        return View(requests);
    }

    // GET: LABREQUESTS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var labrequest = await _context.LabRequests
            .Include(l => l.Patient)
            .Include(l => l.RequestedBy)
            .Include(l => l.AssignedToStaff)
            .Include(l => l.MedicalRecord)
            .Include(l => l.TestTypes)
            .FirstOrDefaultAsync(m => m.LabRequestID == id);

        if (labrequest == null) return NotFound();

        return View(labrequest);
    }

    // GET: LABREQUESTS/Create?encounterId=5&medicalRecordId=5
    [Authorize(Roles = "Nurse,HealthOfficer")]
    public async Task<IActionResult> Create(int? encounterId, int? medicalRecordId)
    {
        MedicalRecord? record = null;
        ClinicalEncounter? encounter = null;

        if (encounterId.HasValue)
        {
            encounter = await _context.ClinicalEncounters
                .Include(e => e.Patient)
                .Include(e => e.ClinicalNotes)
                .FirstOrDefaultAsync(e => e.ID == encounterId.Value);

            if (encounter != null)
            {
                record = encounter.ClinicalNotes.OrderByDescending(n => n.CreatedAt).FirstOrDefault();
                if (record == null)
                {
                    var currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser?.StaffID == null)
                    {
                        TempData["ErrorMessage"] = "Your account is not linked to a Staff record. Please contact the Health Officer.";
                        return RedirectToAction("Index", "Home");
                    }

                    record = new MedicalRecord
                    {
                        EncounterID = encounter.ID,
                        PatientID = encounter.PatientID,
                        ChiefComplaint = encounter.Type == EncounterType.Emergency ? "Emergency Diagnostic Evaluation" : "Diagnostic Evaluation",
                        DateTime = DateTime.Now,
                        CreatedByID = currentUser.StaffID.Value,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = currentUser.FullName ?? "Staff"
                    };
                    _context.MedicalRecords.Add(record);
                    await _context.SaveChangesAsync();
                }
            }
        }

        if (record == null && medicalRecordId.HasValue)
        {
            record = await _context.MedicalRecords
                .Include(m => m.Patient)
                .FirstOrDefaultAsync(m => m.ID == medicalRecordId.Value);

            if (record != null && record.EncounterID.HasValue && encounter == null)
            {
                encounter = await _context.ClinicalEncounters.FindAsync(record.EncounterID.Value);
            }
        }

        if (record == null) return NotFound();

        var patient = record.Patient ?? encounter?.Patient;

        // Load active lab technician and health officer staff for the assignment dropdown
        // User requirement: Strictly include only Solomon Girma and Sam in the assignment dropdown
        var activeLabTechsRaw = await _context.Staffs
            .Where(s => (s.Role == "LabTechnician" || s.Role == "HealthOfficer") && s.IsActive)
            .OrderBy(s => s.Role).ThenBy(s => s.FirstName).ThenBy(s => s.LastName)
            .ToListAsync();

        var activeLabTechs = activeLabTechsRaw
            .Where(s => (s.FirstName.Trim().Equals("Solomon", StringComparison.OrdinalIgnoreCase) && s.LastName.Trim().Equals("Girma", StringComparison.OrdinalIgnoreCase))
                     || (s.FirstName.Trim().Equals("Girma", StringComparison.OrdinalIgnoreCase) && s.LastName.Trim().Equals("Solomon", StringComparison.OrdinalIgnoreCase))
                     || s.FirstName.Trim().Equals("Sam", StringComparison.OrdinalIgnoreCase)
                     || s.LastName.Trim().Equals("Sam", StringComparison.OrdinalIgnoreCase))
            .GroupBy(s => s.Role == "HealthOfficer" ? "Solomon Girma" : "Sam")
            .Select(g => g.First())
            .OrderBy(s => s.Role)
            .ThenBy(s => s.FirstName)
            .ToList();

        var vm = new LabRequestCreateViewModel
        {
            EncounterID = encounter?.ID,
            MedicalRecordID = record.ID,
            PatientID = record.PatientID,
            PatientDisplayName = patient != null ? $"{patient.FirstName} {patient.LastName} ({patient.CardNumber ?? patient.IDNumber})" : "—",
            IsStat = encounter?.Type == EncounterType.Emergency,
            AvailableTestTypes = await _context.TestTypes.OrderBy(t => t.TestName).ToListAsync(),
            AvailableLabTechs = activeLabTechs
        };

        return View(vm);
    }

    // POST: LABREQUESTS/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Nurse,HealthOfficer")]
    public async Task<IActionResult> Create(LabRequestCreateViewModel vm)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.StaffID == null)
        {
            ModelState.AddModelError(string.Empty,
                "Your account isn't linked to a Staff record, so this request can't be attributed to you.");
        }

        ClinicalEncounter? encounter = null;
        if (vm.EncounterID.HasValue)
        {
            encounter = await _context.ClinicalEncounters.FindAsync(vm.EncounterID.Value);
        }

        bool isEmergency = vm.IsStat || (encounter != null && (encounter.Type == EncounterType.Emergency || encounter.Status == WorkflowState.InEmergency));

        if (!isEmergency && vm.PatientID != 0)
        {
            var isPatientInService = await _context.Queues
                .Include(q => q.ServicePoint)
                .AnyAsync(q => q.PatientID == vm.PatientID && (q.QueueStatus == "InService" || q.QueueStatus == "Called") && q.ServicePoint != null && q.ServicePoint.Code == "CON");

            var hasActiveEncounter = encounter != null && encounter.Status != WorkflowState.Discharged;

            if (!isPatientInService && !hasActiveEncounter)
            {
                ModelState.AddModelError(string.Empty, "Patient must be called into Consultation queue or have an active registered encounter.");
            }
        }

        if (!ModelState.IsValid)
        {
            vm.AvailableTestTypes = await _context.TestTypes.OrderBy(t => t.TestName).ToListAsync();
            var activeLabTechsRaw = await _context.Staffs
                .Where(s => (s.Role == "LabTechnician" || s.Role == "HealthOfficer") && s.IsActive)
                .OrderBy(s => s.Role).ThenBy(s => s.FirstName).ThenBy(s => s.LastName)
                .ToListAsync();

            vm.AvailableLabTechs = activeLabTechsRaw
                .Where(s => (s.FirstName.Trim().Equals("Solomon", StringComparison.OrdinalIgnoreCase) && s.LastName.Trim().Equals("Girma", StringComparison.OrdinalIgnoreCase))
                         || (s.FirstName.Trim().Equals("Girma", StringComparison.OrdinalIgnoreCase) && s.LastName.Trim().Equals("Solomon", StringComparison.OrdinalIgnoreCase))
                         || s.FirstName.Trim().Equals("Sam", StringComparison.OrdinalIgnoreCase)
                         || s.LastName.Trim().Equals("Sam", StringComparison.OrdinalIgnoreCase))
                .GroupBy(s => s.Role == "HealthOfficer" ? "Solomon Girma" : "Sam")
                .Select(g => g.First())
                .OrderBy(s => s.Role)
                .ThenBy(s => s.FirstName)
                .ToList();
            return View(vm);
        }

        var selectedTests = await _context.TestTypes
            .Where(t => vm.SelectedTestTypeIds.Contains(t.ID))
            .ToListAsync();

        var labRequest = new LabRequest
        {
            PatientID = vm.PatientID,
            ClinicalEncounterID = vm.EncounterID,
            MedicalRecordID = vm.MedicalRecordID,
            RequestedByID = currentUser!.StaffID!.Value,
            RequestDate = DateTime.Now,
            ClinicalNotes = vm.ClinicalNotes,
            Status = LabRequestStatuses.Pending,
            TestTypes = selectedTests,
            AssignedToStaffID = vm.AssignedToStaffID
        };

        _context.LabRequests.Add(labRequest);

        if (encounter != null && encounter.Status != WorkflowState.Discharged)
        {
            encounter.Status = WorkflowState.WaitingLab;
        }

        await _context.SaveChangesAsync();

        // Record assignment history if a lab tech was selected
        if (vm.AssignedToStaffID.HasValue)
        {
            _context.TaskAssignmentHistories.Add(new TaskAssignmentHistory
            {
                TaskType = "LabRequest",
                TaskID = labRequest.LabRequestID,
                AssignedToStaffID = vm.AssignedToStaffID.Value,
                AssignedByID = currentUser.Id,
                AssignedAt = DateTime.UtcNow,
                Reason = "Initial assignment at lab order creation"
            });
            await _context.SaveChangesAsync();
        }

        // Close any active Triage or Consultation queues since patient is now directed to the Laboratory
        var activeNurseQueues = await _context.Queues
            .Include(q => q.ServicePoint)
            .Where(q => q.PatientID == vm.PatientID 
                     && q.ServicePoint != null && (q.ServicePoint.Code == "TRI" || q.ServicePoint.Code == "CON")
                     && (q.QueueStatus == "InService" || q.QueueStatus == "Waiting" || q.QueueStatus == "Called"))
            .ToListAsync();

        foreach (var nq in activeNurseQueues)
        {
            nq.QueueStatus = "Completed";
            var log = await _context.ServiceLogs.FirstOrDefaultAsync(l => l.QueueID == nq.ID);
            if (log != null && log.ServiceEndedAt == null)
            {
                log.ServiceEndedAt = DateTime.Now;
            }
        }

        // Automatically create a Lab Queue ticket
        var labServicePoint = await _context.ServicePoints.FirstOrDefaultAsync(s => s.Code == "LAB" || s.Name.Contains("Laboratory") || s.Name.Contains("Lab"));
        if (labServicePoint != null)
        {
            var todaysLabQueuesCount = await _context.Queues
                .Where(q => q.ServicePointID == labServicePoint.ID && q.CreatedAt.Date == DateTime.Today)
                .CountAsync();

            var newQueue = new Queue
            {
                TokenNumber = $"{labServicePoint.Code}-{todaysLabQueuesCount + 1:D3}",
                PatientID = vm.PatientID,
                ServicePointID = labServicePoint.ID,
                PriorityLevel = isEmergency ? "Emergency" : "Normal",
                QueueStatus = "Waiting",
                QueueType = "Laboratory",
                CreatedAt = DateTime.Now
            };

            _context.Queues.Add(newQueue);
            await _context.SaveChangesAsync();
        }

        // Fetch Patient details for notification
        var patient = await _context.Patients.FindAsync(vm.PatientID);
        string patientInfo = patient != null ? $"{patient.FirstName} {patient.LastName} (Card No: {patient.CardNumber ?? patient.IDNumber})" : "Unknown Patient";
        string notifTitle = isEmergency ? "STAT EMERGENCY: Urgent Lab Order" : "New Lab Request";
        string notifMessage = $"New laboratory request for {patientInfo} by {currentUser.FullName} ({selectedTests.Count} test(s)).";

        if (vm.AssignedToStaffID.HasValue)
        {
            // Notify the specific assigned lab tech
            var assignedUser = await _context.Users.FirstOrDefaultAsync(u => u.StaffID == vm.AssignedToStaffID.Value);
            if (assignedUser != null)
            {
                await _notificationService.CreateNotificationAsync(assignedUser.Id, notifTitle,
                    $"You have been assigned a lab request: {notifMessage}", "LabRequest", labRequest.LabRequestID);
            }
        }
        else
        {
            // No specific assignment — notify all active lab techs
            var labTechs = await _userManager.GetUsersInRoleAsync("LabTechnician");
            var labTechIds = labTechs.Select(t => t.Id).ToList();
            await _notificationService.CreateNotificationForUsersAsync(labTechIds, notifTitle, notifMessage, "LabRequest", labRequest.LabRequestID);
        }

        TempData["SuccessMessage"] = $"Lab request for {patientInfo} submitted successfully.";
        return RedirectToAction(nameof(Confirmation), new { id = labRequest.LabRequestID });
    }

    // GET: LABREQUESTS/Confirmation/5
    [Authorize(Roles = "Nurse,HealthOfficer,LabTechnician")]
    public async Task<IActionResult> Confirmation(int? id)
    {
        if (id == null) return NotFound();

        var labRequest = await _context.LabRequests
            .Include(l => l.Patient)
                .ThenInclude(p => p.Department)
            .Include(l => l.Patient)
                .ThenInclude(p => p.DormBlock)
            .Include(l => l.RequestedBy)
            .Include(l => l.AssignedToStaff)
            .Include(l => l.TestTypes)
            .FirstOrDefaultAsync(l => l.LabRequestID == id);

        if (labRequest == null) return NotFound();

        return View(labRequest);
    }

    // GET: LABREQUESTS/Edit/5
    [Authorize(Roles = "LabTechnician,HealthOfficer")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var labrequest = await _context.LabRequests
            .Include(l => l.Patient)
            .Include(l => l.RequestedBy)
            .Include(l => l.TestTypes)
            .FirstOrDefaultAsync(l => l.LabRequestID == id);

        if (labrequest == null) return NotFound();

        var vm = new LabRequestEditViewModel
        {
            LabRequestID = labrequest.LabRequestID,
            PatientDisplayName = labrequest.Patient != null ? $"{labrequest.Patient.FirstName} {labrequest.Patient.LastName}" : "—",
            RequestedByDisplayName = labrequest.RequestedBy != null ? $"{labrequest.RequestedBy.FirstName} {labrequest.RequestedBy.LastName}" : "—",
            RequestDate = labrequest.RequestDate,
            TestTypeNames = labrequest.TestTypes.Any() ? string.Join(", ", labrequest.TestTypes.Select(t => t.TestName)) : "—",
            ClinicalNotes = labrequest.ClinicalNotes,
            Status = labrequest.Status
        };

        return View(vm);
    }

    // POST: LABREQUESTS/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "LabTechnician,HealthOfficer")]
    public async Task<IActionResult> Edit(int id, LabRequestEditViewModel vm)
    {
        if (id != vm.LabRequestID) return NotFound();

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var labrequest = await _context.LabRequests.FindAsync(id);
        if (labrequest == null) return NotFound();

        labrequest.Status = vm.Status;
        labrequest.ClinicalNotes = vm.ClinicalNotes;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!LabRequestExists(id)) return NotFound();
            throw;
        }

        TempData["SuccessMessage"] = "Lab request updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // GET: LABREQUESTS/Delete/5
    [Authorize(Roles = "HealthOfficer")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var labrequest = await _context.LabRequests
            .Include(l => l.Patient)
            .Include(l => l.TestTypes)
            .FirstOrDefaultAsync(m => m.LabRequestID == id);

        if (labrequest == null) return NotFound();

        return View(labrequest);
    }

    // POST: LABREQUESTS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "HealthOfficer")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var labrequest = await _context.LabRequests.FindAsync(id);
        if (labrequest != null)
        {
            _context.LabRequests.Remove(labrequest);
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Lab request deleted.";
        return RedirectToAction(nameof(Index));
    }

    private bool LabRequestExists(int id)
    {
        return _context.LabRequests.Any(e => e.LabRequestID == id);
    }
}
