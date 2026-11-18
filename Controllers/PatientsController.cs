using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HUIOTClinicMS.Models;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.ViewModels;

using HUIOTClinicMS.Models.Enums;

using Microsoft.AspNetCore.Identity;
using HUIOTClinicMS.Services;

[Authorize]
public class PatientsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notificationService;

    public PatientsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        INotificationService notificationService)
    {
        _context = context;
        _userManager = userManager;
        _notificationService = notificationService;
    }

    // GET: PATIENTS
    public async Task<IActionResult> Index(string? search)
    {
        var query = _context.Patients
            .Include(p => p.Department)
            .Include(p => p.DormBlock)
            .AsQueryable();

        Patient? exactMatch = null;
        PatientDetailsViewModel? patientHistory = null;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var lowerTerm = term.ToLower();

            // Check for exact match on Student ID (IDNumber) or Clinic Card (CardNumber)
            exactMatch = await query.FirstOrDefaultAsync(p => 
                p.IDNumber.ToLower() == lowerTerm || 
                (p.CardNumber != null && p.CardNumber.ToLower() == lowerTerm));

            query = query.Where(p => 
                p.IDNumber.ToLower().Contains(lowerTerm) ||
                (p.CardNumber != null && p.CardNumber.ToLower().Contains(lowerTerm)) ||
                (p.FirstName + " " + p.LastName).ToLower().Contains(lowerTerm));

            ViewBag.SearchTerm = term;
        }

        var patients = await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(100)
            .ToListAsync();

        if (exactMatch == null && patients.Count == 1 && !string.IsNullOrWhiteSpace(search))
        {
            exactMatch = patients[0];
        }

        if (exactMatch != null)
        {
            var pId = exactMatch.ID;

            var encounters = await _context.ClinicalEncounters
                .AsNoTracking()
                .AsSplitQuery()
                .Include(e => e.AssignedToStaff)
                .Include(e => e.TriageAssessment)
                .Include(e => e.ClinicalNotes)
                    .ThenInclude(n => n.CreatedByStaff)
                .Include(e => e.LabRequests)
                    .ThenInclude(l => l.TestTypes)
                .Include(e => e.Prescriptions)
                .Include(e => e.ReferralRecord)
                .Where(e => e.PatientID == pId)
                .OrderByDescending(e => e.StartTime)
                .ToListAsync();

            var medicalRecords = await _context.MedicalRecords
                .Include(m => m.CreatedByStaff)
                .Include(m => m.LabRequests)
                .Where(m => m.PatientID == pId)
                .OrderByDescending(m => m.DateTime)
                .ToListAsync();

            var medRecordIds = medicalRecords.Select(m => m.ID).ToList();

            var labResults = await _context.LabResults
                .Include(lr => lr.TestType)
                .Include(lr => lr.ResultPerformedBy)
                .Where(lr => medRecordIds.Contains(lr.MedicalRecordID))
                .OrderByDescending(lr => lr.ID)
                .ToListAsync();

            var labRequests = await _context.LabRequests
                .Include(l => l.TestTypes)
                .Include(l => l.RequestedBy)
                .Include(l => l.AssignedToStaff)
                .Where(l => l.PatientID == pId)
                .OrderByDescending(l => l.RequestDate)
                .ToListAsync();

            var prescriptions = await _context.Prescriptions
                .Include(p => p.PrescribedBy)
                .Include(p => p.AssignedToStaff)
                .Include(p => p.Medication)
                    .ThenInclude(m => m!.Pharmacist)
                .Where(p => p.PatientID == pId)
                .OrderByDescending(p => p.TimeDate)
                .ToListAsync();

            var emergencyContact = await _context.EmergencyContacts
                .FirstOrDefaultAsync(e => e.PatientID == pId);

            var handoverHistory = await _context.TaskAssignmentHistories
                .Include(h => h.AssignedFromStaff)
                .Include(h => h.AssignedToStaff)
                .Include(h => h.AssignedByUser)
                .Where(h => h.TaskType == "PatientHandover" && h.TaskID == pId)
                .OrderByDescending(h => h.AssignedAt)
                .ToListAsync();

            patientHistory = new PatientDetailsViewModel
            {
                Patient = exactMatch,
                EmergencyContact = emergencyContact,
                Encounters = encounters,
                MedicalRecords = medicalRecords,
                LabRequests = labRequests,
                LabResults = labResults,
                Prescriptions = prescriptions,
                HandoverHistory = handoverHistory
            };
        }

        ViewBag.ExactMatch = exactMatch;
        ViewBag.PatientHistory = patientHistory;
        ViewBag.HasSearched = !string.IsNullOrWhiteSpace(search);

        return View(patients);
    }

    // GET: PATIENTS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var patient = await _context.Patients
            .Include(p => p.Department)
            .Include(p => p.DormBlock)
            .FirstOrDefaultAsync(m => m.ID == id);

        if (patient == null) return NotFound();

        var emergencyContact = await _context.EmergencyContacts
            .FirstOrDefaultAsync(e => e.PatientID == id);

        var encounters = await _context.ClinicalEncounters
            .AsNoTracking()
            .AsSplitQuery()
            .Include(e => e.AssignedToStaff)
            .Include(e => e.TriageAssessment)
            .Include(e => e.ClinicalNotes)
                .ThenInclude(n => n.CreatedByStaff)
            .Include(e => e.LabRequests)
                .ThenInclude(l => l.TestTypes)
            .Include(e => e.Prescriptions)
            .Include(e => e.ReferralRecord)
            .Where(e => e.PatientID == id)
            .OrderByDescending(e => e.StartTime)
            .ToListAsync();

        var medicalRecords = await _context.MedicalRecords
            .Include(m => m.CreatedByStaff)
            .Include(m => m.LabRequests)
            .Where(m => m.PatientID == id)
            .OrderByDescending(m => m.DateTime)
            .ToListAsync();

        var medRecordIds = medicalRecords.Select(m => m.ID).ToList();

        var labResults = await _context.LabResults
            .Include(lr => lr.TestType)
            .Include(lr => lr.ResultPerformedBy)
            .Where(lr => medRecordIds.Contains(lr.MedicalRecordID))
            .OrderByDescending(lr => lr.ID)
            .ToListAsync();

        var labRequests = await _context.LabRequests
            .Include(l => l.TestTypes)
            .Include(l => l.RequestedBy)
            .Include(l => l.AssignedToStaff)
            .Where(l => l.PatientID == id)
            .OrderByDescending(l => l.RequestDate)
            .ToListAsync();

        var prescriptions = await _context.Prescriptions
            .Include(p => p.PrescribedBy)
            .Include(p => p.AssignedToStaff)
            .Include(p => p.Medication)
                .ThenInclude(m => m!.Pharmacist)
            .Where(p => p.PatientID == id)
            .OrderByDescending(p => p.TimeDate)
            .ToListAsync();

        var handoverHistory = await _context.TaskAssignmentHistories
            .Include(h => h.AssignedFromStaff)
            .Include(h => h.AssignedToStaff)
            .Include(h => h.AssignedByUser)
            .Where(h => h.TaskType == "PatientHandover" && h.TaskID == id)
            .OrderByDescending(h => h.AssignedAt)
            .ToListAsync();

        var vm = new PatientDetailsViewModel
        {
            Patient = patient,
            EmergencyContact = emergencyContact,
            Encounters = encounters,
            MedicalRecords = medicalRecords,
            LabRequests = labRequests,
            LabResults = labResults,
            Prescriptions = prescriptions,
            HandoverHistory = handoverHistory
        };

        return View(vm);
    }

    // GET: Patients/CheckIn/5
    [Authorize(Roles = "Nurse,HealthOfficer")]
    public async Task<IActionResult> CheckIn(int? id)
    {
        if (id == null) return NotFound();

        var patient = await _context.Patients
            .Include(p => p.Department)
            .Include(p => p.DormBlock)
            .FirstOrDefaultAsync(p => p.ID == id);

        if (patient == null) return NotFound();

        var activeEncounter = await _context.ClinicalEncounters
            .Include(e => e.TriageAssessment)
            .Where(e => e.PatientID == id.Value && e.Status != WorkflowState.Discharged && e.Status != WorkflowState.Transferred)
            .OrderByDescending(e => e.StartTime)
            .FirstOrDefaultAsync();

        ViewBag.ActiveEncounter = activeEncounter;

        var cliniciansList = await _context.Staffs
            .Where(s => s.IsActive && (s.Role == "HealthOfficer" || s.Role == "Nurse"))
            .OrderBy(s => s.Role).ThenBy(s => s.FirstName)
            .ToListAsync();

        var clinicians = cliniciansList
            .Where(s => !(s.FirstName == "Meron" && s.LastName == "Alemu")
                     && !(s.FirstName == "Tigist" && s.LastName == "Bekele")
                     && !(s.FirstName.ToLower() == "wale" && s.LastName.ToLower() == "solomon"))
            .GroupBy(s => $"{s.FirstName} {s.LastName} ({s.Role})")
            .Select(g => g.First())
            .Select(s => new { s.ID, Name = $"{s.FirstName} {s.LastName} ({s.Role})" })
            .ToList();

        ViewBag.Clinicians = new SelectList(clinicians, "ID", "Name");

        return View(patient);
    }

    // POST: Patients/CheckIn/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Nurse,HealthOfficer")]
    public async Task<IActionResult> CheckIn(int id, EncounterType type = EncounterType.Routine, int? assignedToStaffId = null, bool forceNewVisit = false)
    {
        var patient = await _context.Patients.FindAsync(id);
        if (patient == null) return NotFound();

        var activeEncounter = await _context.ClinicalEncounters
            .Include(e => e.TriageAssessment)
            .Where(e => e.PatientID == id && e.Status != WorkflowState.Discharged && e.Status != WorkflowState.Transferred)
            .OrderByDescending(e => e.StartTime)
            .FirstOrDefaultAsync();

        if (activeEncounter != null && !forceNewVisit)
        {
            var currentUserId = _userManager.GetUserId(User) ?? "Staff";

            if (type == EncounterType.Emergency)
            {
                activeEncounter.Type = EncounterType.Emergency;
                if (activeEncounter.Status == WorkflowState.Registered || activeEncounter.Status == WorkflowState.WaitingConsultation)
                {
                    activeEncounter.Status = WorkflowState.InEmergency;
                }

                if (assignedToStaffId.HasValue && activeEncounter.AssignedToStaffID != assignedToStaffId.Value)
                {
                    var prevStaffId = activeEncounter.AssignedToStaffID;
                    activeEncounter.AssignedToStaffID = assignedToStaffId.Value;

                    _context.TaskAssignmentHistories.Add(new TaskAssignmentHistory
                    {
                        TaskType = "PatientEncounter",
                        TaskID = activeEncounter.ID,
                        AssignedFromStaffID = prevStaffId,
                        AssignedToStaffID = assignedToStaffId.Value,
                        AssignedByID = currentUserId,
                        AssignedAt = DateTime.UtcNow,
                        Reason = "Emergency escalation reassignment"
                    });
                }

                await _context.SaveChangesAsync();

                // Broadcast emergency alert to all Health Officers
                var healthOfficers = await _userManager.GetUsersInRoleAsync("HealthOfficer");
                if (healthOfficers.Any())
                {
                    await _notificationService.CreateNotificationForUsersAsync(
                        healthOfficers.Select(h => h.Id),
                        "STAT EMERGENCY ESCALATION",
                        $"Patient {patient.FirstName} {patient.LastName} ({patient.CardNumber ?? patient.IDNumber}) has been escalated to STAT EMERGENCY.",
                        "Emergency",
                        activeEncounter.ID);
                }

                TempData["SuccessMessage"] = $"Existing visit (Encounter #{activeEncounter.ID}) escalated to STAT Emergency.";

                if (activeEncounter.TriageAssessment == null)
                {
                    return RedirectToAction("Create", "TriageAssessments", new { encounterId = activeEncounter.ID });
                }

                return RedirectToAction("Details", "ClinicalEncounters", new { id = activeEncounter.ID });
            }
            else
            {
                // Routine: if staff reassigned, update and audit
                if (assignedToStaffId.HasValue && activeEncounter.AssignedToStaffID != assignedToStaffId.Value)
                {
                    var prevStaffId = activeEncounter.AssignedToStaffID;
                    activeEncounter.AssignedToStaffID = assignedToStaffId.Value;

                    _context.TaskAssignmentHistories.Add(new TaskAssignmentHistory
                    {
                        TaskType = "PatientEncounter",
                        TaskID = activeEncounter.ID,
                        AssignedFromStaffID = prevStaffId,
                        AssignedToStaffID = assignedToStaffId.Value,
                        AssignedByID = currentUserId,
                        AssignedAt = DateTime.UtcNow,
                        Reason = "Check-in consultation reassignment"
                    });
                    await _context.SaveChangesAsync();

                    var assignedUser = await _context.Users.FirstOrDefaultAsync(u => u.StaffID == assignedToStaffId.Value);
                    if (assignedUser != null)
                    {
                        await _notificationService.CreateNotificationAsync(
                            assignedUser.Id,
                            "Patient Assigned",
                            $"Patient {patient.FirstName} {patient.LastName} ({patient.CardNumber ?? patient.IDNumber}) assigned to you.",
                            "PatientAssignment",
                            activeEncounter.ID);
                    }
                }

                TempData["SuccessMessage"] = $"Resumed active clinical visit (Encounter #{activeEncounter.ID}).";
                return RedirectToAction("Details", "ClinicalEncounters", new { id = activeEncounter.ID });
            }
        }

        var encounter = new ClinicalEncounter
        {
            PatientID = id,
            StartTime = DateTime.UtcNow,
            Type = type,
            Status = type == EncounterType.Emergency ? WorkflowState.InEmergency : WorkflowState.Registered,
            AssignedToStaffID = assignedToStaffId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name
        };

        _context.ClinicalEncounters.Add(encounter);
        await _context.SaveChangesAsync();

        if (assignedToStaffId.HasValue)
        {
            var currentUserId = _userManager.GetUserId(User) ?? "Staff";
            _context.TaskAssignmentHistories.Add(new TaskAssignmentHistory
            {
                TaskType = "PatientEncounter",
                TaskID = encounter.ID,
                AssignedToStaffID = assignedToStaffId.Value,
                AssignedByID = currentUserId,
                AssignedAt = DateTime.UtcNow,
                Reason = "Check-in consultation assignment"
            });
            await _context.SaveChangesAsync();

            var assignedUser = await _context.Users.FirstOrDefaultAsync(u => u.StaffID == assignedToStaffId.Value);
            if (assignedUser != null)
            {
                await _notificationService.CreateNotificationAsync(
                    assignedUser.Id,
                    "New Patient Assigned",
                    $"Patient {patient.FirstName} {patient.LastName} ({patient.CardNumber ?? patient.IDNumber}) has been assigned to you for consultation.",
                    "PatientAssignment",
                    encounter.ID);
            }
        }

        if (type == EncounterType.Emergency)
        {
            // Broadcast emergency alert to all Health Officers
            var healthOfficers = await _userManager.GetUsersInRoleAsync("HealthOfficer");
            if (healthOfficers.Any())
            {
                await _notificationService.CreateNotificationForUsersAsync(
                    healthOfficers.Select(h => h.Id),
                    "STAT EMERGENCY INTAKE",
                    $"Emergency patient {patient.FirstName} {patient.LastName} ({patient.CardNumber ?? patient.IDNumber}) checked in. Immediate medical attention required.",
                    "Emergency",
                    encounter.ID);
            }
        }

        var targetSpCode = type == EncounterType.Emergency ? "CON" : "TRI";
        var targetSp = await _context.ServicePoints.FirstOrDefaultAsync(s => s.Code == targetSpCode)
                    ?? await _context.ServicePoints.FirstOrDefaultAsync(s => s.Code == "CON");

        if (targetSp != null)
        {
            var today = DateTime.Today;
            var queueCountToday = await _context.Queues.CountAsync(q => q.ServicePointID == targetSp.ID && q.CreatedAt >= today);
            var tokenNumber = $"{targetSp.Code}-{(queueCountToday + 1):D3}";

            var queue = new Queue
            {
                PatientID = patient.ID,
                ServicePointID = targetSp.ID,
                TokenNumber = tokenNumber,
                PriorityLevel = type == EncounterType.Emergency ? "Emergency" : "Normal",
                QueueStatus = type == EncounterType.Emergency ? "Called" : "Waiting",
                QueueType = type.ToString(),
                CreatedAt = DateTime.Now
            };
            _context.Queues.Add(queue);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Patient checked in for {type} visit. Token: {tokenNumber}";
        }

        if (type == EncounterType.Emergency)
        {
            return RedirectToAction("Create", "TriageAssessments", new { encounterId = encounter.ID });
        }

        return RedirectToAction("Details", "ClinicalEncounters", new { id = encounter.ID });
    }

    // GET: PATIENTS/Create
    [Authorize(Roles = "Nurse,HealthOfficer")]
    public async Task<IActionResult> Create(string? idNumber)
    {
        await PopulateDropdownsAsync();
        var vm = new PatientCreateViewModel
        {
            EligibleNurses = await GetActiveNurseSelectListAsync()
        };
        if (!string.IsNullOrWhiteSpace(idNumber))
        {
            vm.IDNumber = idNumber.Trim();
        }
        return View(vm);
    }

    // POST: PATIENTS/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Nurse,HealthOfficer")]
    public async Task<IActionResult> Create(PatientCreateViewModel vm)
    {
        var duplicate = await _context.Patients
            .AnyAsync(p => p.IDNumber == vm.IDNumber.Trim());

        if (duplicate)
        {
            ModelState.AddModelError(nameof(vm.IDNumber), "This University ID Number is already registered.");
        }

        var targetNurse = await _context.Staffs
            .FirstOrDefaultAsync(s => s.ID == vm.DestinationNurseStaffID && s.IsActive && (s.Role == "Nurse" || s.Role == "HealthOfficer"));

        if (targetNurse == null)
        {
            ModelState.AddModelError(nameof(vm.DestinationNurseStaffID), "The selected destination clinician is invalid or currently inactive.");
        }

        if (vm.DateOfBirth > DateTime.Today)
        {
            ModelState.AddModelError(nameof(vm.DateOfBirth), "Date of birth cannot be in the future.");
        }
        else if (vm.DateOfBirth > DateTime.Today.AddYears(-15))
        {
            ModelState.AddModelError(nameof(vm.DateOfBirth), "Student must be at least 15 years old for university clinic registration.");
        }
        else if (vm.DateOfBirth < DateTime.Today.AddYears(-100))
        {
            ModelState.AddModelError(nameof(vm.DateOfBirth), "Please provide a valid date of birth.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            vm.EligibleNurses = await GetActiveNurseSelectListAsync();
            return View(vm);
        }

        var cardNumber = await GenerateCardNumberAsync();

        var patient = new Patient
        {
            IDNumber = vm.IDNumber.Trim(),
            CardNumber = cardNumber,
            FirstName = vm.FirstName.Trim(),
            LastName = vm.LastName.Trim(),
            Gender = vm.Gender,
            DateOfBirth = vm.DateOfBirth,
            PhoneNumber = vm.PhoneNumber?.Trim(),
            DepartmentID = vm.DepartmentID,
            DormBlockID = vm.DormBlockID,
            IsActive = true,
            CreatedAt = DateTime.Now
        };

        _context.Patients.Add(patient);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(vm.IDNumber), "This University ID Number is already registered.");
            await PopulateDropdownsAsync();
            vm.EligibleNurses = await GetActiveNurseSelectListAsync();
            return View(vm);
        }

        var emergencyContact = new EmergencyContact
        {
            PatientID = patient.ID,
            ContactName = vm.ContactName.Trim(),
            ContactPhoneNumber = vm.ContactPhoneNumber.Trim(),
            Relationship = vm.Relationship
        };

        _context.EmergencyContacts.Add(emergencyContact);
        await _context.SaveChangesAsync();

        var currentUser = await _userManager.GetUserAsync(User);
        var regStaffId = currentUser?.StaffID;
        var regNurseName = currentUser?.FullName ?? User.Identity?.Name ?? "Registration Nurse";

        // Create the initial Clinical Encounter assigned to selected destination nurse
        var encounter = new ClinicalEncounter
        {
            PatientID = patient.ID,
            StartTime = DateTime.UtcNow,
            Type = vm.IsEmergency ? EncounterType.Emergency : EncounterType.Routine,
            Status = vm.IsEmergency ? WorkflowState.InEmergency : WorkflowState.Registered,
            AssignedToStaffID = vm.DestinationNurseStaffID,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = regNurseName
        };
        _context.ClinicalEncounters.Add(encounter);
        await _context.SaveChangesAsync();

        // Record handover audit entry in TaskAssignmentHistory
        string reasonText = vm.IsEmergency
            ? "[STAT EMERGENCY] " + (string.IsNullOrWhiteSpace(vm.HandoverNote) ? "STAT emergency patient registration & handover for immediate clinical triage & consultation" : vm.HandoverNote.Trim())
            : (string.IsNullOrWhiteSpace(vm.HandoverNote) ? "Patient registration handover for combined triage & consultation" : vm.HandoverNote.Trim());

        var handover = new TaskAssignmentHistory
        {
            TaskType = "PatientHandover",
            TaskID = patient.ID,
            AssignedFromStaffID = regStaffId,
            AssignedToStaffID = vm.DestinationNurseStaffID,
            AssignedByID = currentUser?.Id ?? "System",
            AssignedAt = DateTime.UtcNow,
            Reason = reasonText
        };
        _context.TaskAssignmentHistories.Add(handover);
        await _context.SaveChangesAsync();

        // Create ONE queue ticket for the combined Triage & Consultation service point (TRI)
        var triSp = await _context.ServicePoints.FirstOrDefaultAsync(s => s.Code == "TRI")
                 ?? await _context.ServicePoints.FirstOrDefaultAsync(s => s.Code == "CON");

        string tokenNumber = string.Empty;
        if (triSp != null)
        {
            var today = DateTime.Today;
            var existingQueue = await _context.Queues
                .FirstOrDefaultAsync(q => q.PatientID == patient.ID && q.ServicePointID == triSp.ID && q.CreatedAt >= today && (q.QueueStatus == "Waiting" || q.QueueStatus == "InService"));

            if (existingQueue == null)
            {
                var queueCountToday = await _context.Queues.CountAsync(q => q.ServicePointID == triSp.ID && q.CreatedAt >= today);
                tokenNumber = $"{triSp.Code}-{(queueCountToday + 1):D3}";

                var queue = new Queue
                {
                    PatientID = patient.ID,
                    ServicePointID = triSp.ID,
                    TokenNumber = tokenNumber,
                    PriorityLevel = vm.IsEmergency ? "Emergency" : "Normal",
                    QueueStatus = "Waiting",
                    QueueType = vm.IsEmergency ? "Emergency" : "Triage & Consultation",
                    CreatedAt = DateTime.Now
                };
                _context.Queues.Add(queue);
                await _context.SaveChangesAsync();
            }
            else
            {
                tokenNumber = existingQueue.TokenNumber;
            }
        }

        // Send direct notification to destination nurse
        var destUser = await _context.Users.FirstOrDefaultAsync(u => u.StaffID == vm.DestinationNurseStaffID);
        if (destUser != null)
        {
            string tokenSuffix = !string.IsNullOrEmpty(tokenNumber) ? $" Queue Token: {tokenNumber}." : "";
            string notifTitle = vm.IsEmergency ? "🚨 STAT EMERGENCY: Incoming Patient Handover" : "Incoming Patient Handover";
            string notifMessage = vm.IsEmergency
                ? $"STAT EMERGENCY: Patient {patient.FirstName} {patient.LastName} ({cardNumber}) has been registered and handed over to you by {regNurseName} for immediate emergency care.{tokenSuffix}"
                : $"Patient {patient.FirstName} {patient.LastName} ({cardNumber}) has been registered and handed over to you by {regNurseName} for triage & consultation.{tokenSuffix}";

            await _notificationService.CreateDirectNotificationAsync(
                destUser.Id,
                notifTitle,
                notifMessage,
                "PatientHandover",
                patient.ID);
        }

        if (vm.IsEmergency)
        {
            var healthOfficers = await _userManager.GetUsersInRoleAsync("HealthOfficer");
            if (healthOfficers.Any())
            {
                string tokenSuffix = !string.IsNullOrEmpty(tokenNumber) ? $" Queue Token: {tokenNumber}." : "";
                await _notificationService.CreateNotificationForUsersAsync(
                    healthOfficers.Select(h => h.Id),
                    "STAT EMERGENCY ALERT",
                    $"Emergency patient {patient.FirstName} {patient.LastName} ({cardNumber}) registered and routed to Nurse {targetNurse.FirstName} {targetNurse.LastName} by {regNurseName}.{tokenSuffix}",
                    "Emergency",
                    patient.ID);
            }
        }

        string tokenAlert = !string.IsNullOrEmpty(tokenNumber) ? $" Queue Token: {tokenNumber}." : "";
        TempData["SuccessMessage"] = $"Patient {patient.FirstName} {patient.LastName} registered successfully (Card: {cardNumber}) and routed to Nurse {targetNurse.FirstName} {targetNurse.LastName}.{tokenAlert}";

        return RedirectToAction(nameof(HandoverConfirmation), new { id = handover.ID });
    }

    // GET: PATIENTS/Handover/5
    [Authorize(Roles = "Nurse,HealthOfficer")]
    public async Task<IActionResult> Handover(int? id)
    {
        if (id == null) return NotFound();

        var patient = await _context.Patients
            .Include(p => p.Department)
            .Include(p => p.DormBlock)
            .FirstOrDefaultAsync(p => p.ID == id);

        if (patient == null) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        var regStaff = currentUser?.StaffID.HasValue == true
            ? await _context.Staffs.FindAsync(currentUser.StaffID.Value)
            : null;

        var activeNurses = await GetActiveNurseSelectListAsync();

        var vm = new PatientHandoverViewModel
        {
            PatientID = patient.ID,
            PatientName = $"{patient.FirstName} {patient.LastName}",
            IDNumber = patient.IDNumber,
            CardNumber = patient.CardNumber,
            DepartmentName = patient.Department?.DepartmentName,
            DormBlockName = patient.DormBlock?.BlockName,
            RegistrationNurseStaffID = regStaff?.ID,
            RegistrationNurseName = regStaff != null ? $"{regStaff.FirstName} {regStaff.LastName}" : (currentUser?.FullName ?? "Registration Nurse"),
            EligibleNurses = activeNurses,
            IsExistingPatient = patient.CreatedAt.Date < DateTime.Today
        };

        return View(vm);
    }

    // POST: PATIENTS/Handover
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Nurse,HealthOfficer")]
    public async Task<IActionResult> Handover(PatientHandoverViewModel vm)
    {
        var patient = await _context.Patients
            .Include(p => p.Department)
            .Include(p => p.DormBlock)
            .FirstOrDefaultAsync(p => p.ID == vm.PatientID);

        if (patient == null) return NotFound();

        var targetNurse = await _context.Staffs
            .FirstOrDefaultAsync(s => s.ID == vm.DestinationNurseStaffID && s.IsActive && (s.Role == "Nurse" || s.Role == "HealthOfficer"));

        if (targetNurse == null)
        {
            ModelState.AddModelError(nameof(vm.DestinationNurseStaffID), "The selected destination clinician is invalid or currently inactive.");
        }

        if (!ModelState.IsValid)
        {
            vm.PatientName = $"{patient.FirstName} {patient.LastName}";
            vm.IDNumber = patient.IDNumber;
            vm.CardNumber = patient.CardNumber;
            vm.DepartmentName = patient.Department?.DepartmentName;
            vm.DormBlockName = patient.DormBlock?.BlockName;
            vm.EligibleNurses = await GetActiveNurseSelectListAsync();
            return View(vm);
        }

        var currentUser = await _userManager.GetUserAsync(User);
        var regStaffId = currentUser?.StaffID;
        var regNurseName = currentUser?.FullName ?? User.Identity?.Name ?? "Registration Nurse";

        // Record handover audit entry in TaskAssignmentHistory
        string reasonText = vm.IsEmergency
            ? "[STAT EMERGENCY] " + (string.IsNullOrWhiteSpace(vm.HandoverNote) ? "STAT emergency patient handover for immediate clinical triage" : vm.HandoverNote.Trim())
            : (string.IsNullOrWhiteSpace(vm.HandoverNote) ? "Patient handover for clinical triage/intake" : vm.HandoverNote.Trim());

        var handover = new TaskAssignmentHistory
        {
            TaskType = "PatientHandover",
            TaskID = patient.ID,
            AssignedFromStaffID = regStaffId,
            AssignedToStaffID = vm.DestinationNurseStaffID,
            AssignedByID = currentUser?.Id ?? "System",
            AssignedAt = DateTime.UtcNow,
            Reason = reasonText
        };

        _context.TaskAssignmentHistories.Add(handover);
        await _context.SaveChangesAsync();

        // Ensure active encounter exists and is assigned to destination nurse
        var activeEncounter = await _context.ClinicalEncounters
            .Where(e => e.PatientID == patient.ID && e.Status != WorkflowState.Discharged && e.Status != WorkflowState.Transferred)
            .OrderByDescending(e => e.StartTime)
            .FirstOrDefaultAsync();

        if (activeEncounter == null)
        {
            activeEncounter = new ClinicalEncounter
            {
                PatientID = patient.ID,
                StartTime = DateTime.UtcNow,
                Type = vm.IsEmergency ? EncounterType.Emergency : EncounterType.Routine,
                Status = vm.IsEmergency ? WorkflowState.InEmergency : WorkflowState.Registered,
                AssignedToStaffID = vm.DestinationNurseStaffID,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = regNurseName
            };
            _context.ClinicalEncounters.Add(activeEncounter);
            await _context.SaveChangesAsync();
        }
        else
        {
            activeEncounter.AssignedToStaffID = vm.DestinationNurseStaffID;
            if (vm.IsEmergency)
            {
                activeEncounter.Type = EncounterType.Emergency;
                activeEncounter.Status = WorkflowState.InEmergency;
            }
            await _context.SaveChangesAsync();
        }

        // Add to Triage Queue (TRI) so patient gets to queue immediately
        var triSp = await _context.ServicePoints.FirstOrDefaultAsync(s => s.Code == "TRI")
                 ?? await _context.ServicePoints.FirstOrDefaultAsync(s => s.Code == "CON");

        string tokenNumber = string.Empty;
        if (triSp != null)
        {
            var today = DateTime.Today;
            var existingQueue = await _context.Queues
                .FirstOrDefaultAsync(q => q.PatientID == patient.ID && q.ServicePointID == triSp.ID && q.CreatedAt >= today && q.QueueStatus == "Waiting");

            if (existingQueue == null)
            {
                var queueCountToday = await _context.Queues.CountAsync(q => q.ServicePointID == triSp.ID && q.CreatedAt >= today);
                tokenNumber = $"{triSp.Code}-{(queueCountToday + 1):D3}";

                var queue = new Queue
                {
                    PatientID = patient.ID,
                    ServicePointID = triSp.ID,
                    TokenNumber = tokenNumber,
                    PriorityLevel = vm.IsEmergency ? "Emergency" : "Normal",
                    QueueStatus = "Waiting",
                    QueueType = vm.IsEmergency ? "Emergency" : "Routine",
                    CreatedAt = DateTime.Now
                };
                _context.Queues.Add(queue);
                await _context.SaveChangesAsync();
            }
            else
            {
                tokenNumber = existingQueue.TokenNumber;
            }
        }

        // Send notifications
        var destUser = await _context.Users.FirstOrDefaultAsync(u => u.StaffID == vm.DestinationNurseStaffID);
        if (destUser != null)
        {
            string tokenSuffix = !string.IsNullOrEmpty(tokenNumber) ? $" Queue Token: {tokenNumber}." : "";
            string notifTitle = vm.IsEmergency ? "🚨 STAT EMERGENCY: Incoming Patient Handover" : "Incoming Patient Handover";
            string notifMessage = vm.IsEmergency
                ? $"STAT EMERGENCY: Patient {patient.FirstName} {patient.LastName} ({patient.CardNumber ?? patient.IDNumber}) has been handed over to you by {regNurseName} for immediate emergency care.{tokenSuffix}"
                : $"Patient {patient.FirstName} {patient.LastName} ({patient.CardNumber ?? patient.IDNumber}) has been handed over to you by {regNurseName} for clinical triage.{tokenSuffix}";

            await _notificationService.CreateDirectNotificationAsync(
                destUser.Id,
                notifTitle,
                notifMessage,
                "PatientHandover",
                patient.ID);
        }

        if (vm.IsEmergency)
        {
            // Also broadcast emergency alert to all Health Officers
            var healthOfficers = await _userManager.GetUsersInRoleAsync("HealthOfficer");
            if (healthOfficers.Any())
            {
                string tokenSuffix = !string.IsNullOrEmpty(tokenNumber) ? $" Queue Token: {tokenNumber}." : "";
                await _notificationService.CreateNotificationForUsersAsync(
                    healthOfficers.Select(h => h.Id),
                    "STAT EMERGENCY ALERT",
                    $"Emergency patient {patient.FirstName} {patient.LastName} ({patient.CardNumber ?? patient.IDNumber}) handed over to Nurse {targetNurse.FirstName} {targetNurse.LastName} by {regNurseName}.{tokenSuffix}",
                    "Emergency",
                    patient.ID);
            }
        }

        string tokenAlert = !string.IsNullOrEmpty(tokenNumber) ? $" Triage Queue Token: {tokenNumber}." : "";
        TempData["SuccessMessage"] = $"Patient {patient.FirstName} {patient.LastName} ({patient.CardNumber ?? patient.IDNumber}) handed over successfully to Nurse {targetNurse.FirstName} {targetNurse.LastName}.{tokenAlert}";
        return RedirectToAction(nameof(HandoverConfirmation), new { id = handover.ID });
    }

    // GET: PATIENTS/HandoverConfirmation/5
    [Authorize(Roles = "Nurse,HealthOfficer")]
    public async Task<IActionResult> HandoverConfirmation(int id)
    {
        var handover = await _context.TaskAssignmentHistories
            .Include(h => h.AssignedFromStaff)
            .Include(h => h.AssignedToStaff)
            .FirstOrDefaultAsync(h => h.ID == id && h.TaskType == "PatientHandover");

        if (handover == null)
        {
            return NotFound();
        }

        var patient = await _context.Patients
            .Include(p => p.Department)
            .Include(p => p.DormBlock)
            .FirstOrDefaultAsync(p => p.ID == handover.TaskID);

        if (patient == null)
        {
            return NotFound();
        }

        bool isEmergency = handover.Reason != null && handover.Reason.StartsWith("[STAT EMERGENCY]");

        var today = DateTime.Today;
        var queueItem = await _context.Queues
            .Where(q => q.PatientID == patient.ID && q.CreatedAt >= today && q.QueueStatus != "Completed" && q.QueueStatus != "Cancelled")
            .OrderByDescending(q => q.CreatedAt)
            .FirstOrDefaultAsync();

        string? tokenNumber = queueItem?.TokenNumber;

        var vm = new PatientHandoverConfirmationViewModel
        {
            HandoverID = handover.ID,
            PatientID = patient.ID,
            PatientName = $"{patient.FirstName} {patient.LastName}",
            IDNumber = patient.IDNumber,
            CardNumber = patient.CardNumber,
            DepartmentName = patient.Department?.DepartmentName,
            DormBlockName = patient.DormBlock?.BlockName,
            OriginNurseName = handover.AssignedFromStaff != null 
                ? $"{handover.AssignedFromStaff.FirstName} {handover.AssignedFromStaff.LastName}" 
                : "Registration Desk",
            DestinationNurseName = handover.AssignedToStaff != null 
                ? $"{handover.AssignedToStaff.FirstName} {handover.AssignedToStaff.LastName}" 
                : "Triage Nurse",
            HandoverTime = handover.AssignedAt,
            HandoverNote = handover.Reason,
            IsEmergency = isEmergency,
            TokenNumber = tokenNumber,
            StatusMessage = !string.IsNullOrEmpty(tokenNumber)
                ? $"Active in Triage Queue ({tokenNumber})"
                : (isEmergency ? "STAT Emergency Dispatched" : "Handover Dispatched")
        };

        return View(vm);
    }

    // GET: PATIENTS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var patient = await _context.Patients.FindAsync(id);
        if (patient == null) return NotFound();

        var vm = new PatientEditViewModel
        {
            ID = patient.ID,
            IDNumber = patient.IDNumber,
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            Gender = patient.Gender,
            DateOfBirth = patient.DateOfBirth,
            PhoneNumber = patient.PhoneNumber,
            DepartmentID = patient.DepartmentID,
            DormBlockID = patient.DormBlockID,
            IsActive = patient.IsActive,
            CardNumber = patient.CardNumber ?? "—",
            CreatedAt = patient.CreatedAt
        };

        await PopulateDropdownsAsync();
        return View(vm);
    }

    // POST: PATIENTS/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PatientEditViewModel vm)
    {
        if (id != vm.ID) return NotFound();

        if (vm.DateOfBirth > DateTime.Today)
        {
            ModelState.AddModelError(nameof(vm.DateOfBirth), "Date of birth cannot be in the future.");
        }
        else if (vm.DateOfBirth > DateTime.Today.AddYears(-15))
        {
            ModelState.AddModelError(nameof(vm.DateOfBirth), "Student must be at least 15 years old for university clinic registration.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return View(vm);
        }

        var patient = await _context.Patients.FindAsync(id);
        if (patient == null) return NotFound();

        patient.IDNumber = vm.IDNumber;
        patient.FirstName = vm.FirstName;
        patient.LastName = vm.LastName;
        patient.Gender = vm.Gender;
        patient.DateOfBirth = vm.DateOfBirth;
        patient.PhoneNumber = vm.PhoneNumber;
        patient.DepartmentID = vm.DepartmentID;
        patient.DormBlockID = vm.DormBlockID;
        patient.IsActive = vm.IsActive;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!PatientExists(id)) return NotFound();
            throw;
        }

        TempData["SuccessMessage"] = "Patient record updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    // GET: PATIENTS/Delete/5
    [Authorize(Roles = "HealthOfficer")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var patient = await _context.Patients
            .Include(p => p.Department)
            .Include(p => p.DormBlock)
            .FirstOrDefaultAsync(m => m.ID == id);

        if (patient == null) return NotFound();
        return View(patient);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "HealthOfficer")]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var patient = await _context.Patients.FindAsync(id);
        if (patient != null)
        {
            // Delete dependent records that are created immediately upon registration 
            // to allow deleting a "mistake" registration.
            var emergencyContacts = _context.EmergencyContacts.Where(e => e.PatientID == id);
            _context.EmergencyContacts.RemoveRange(emergencyContacts);

            var queueIds = _context.Queues.Where(q => q.PatientID == id).Select(q => q.ID).ToList();
            var serviceLogs = _context.ServiceLogs.Where(sl => queueIds.Contains(sl.QueueID));
            _context.ServiceLogs.RemoveRange(serviceLogs);

            var queues = _context.Queues.Where(q => q.PatientID == id);
            _context.Queues.RemoveRange(queues);

            _context.Patients.Remove(patient);
        }

        try
        {
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Patient record deleted successfully.";
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "Cannot delete this patient because they have existing medical records or clinical history. Please 'Edit' and deactivate the patient instead.";
        }

        return RedirectToAction(nameof(Index));
    }

    private bool PatientExists(int? id)
    {
        return _context.Patients.Any(e => e.ID == id);
    }

    private async Task PopulateDropdownsAsync()
    {
        ViewBag.Departments = new SelectList(await _context.Departments.ToListAsync(), "ID", "DepartmentName");
        ViewBag.DormBlocks = new SelectList(await _context.DormBlocks.ToListAsync(), "ID", "BlockName");

        var cliniciansList = await _context.Staffs
            .Where(s => s.IsActive && (s.Role == "HealthOfficer" || s.Role == "Nurse"))
            .OrderBy(s => s.Role).ThenBy(s => s.FirstName)
            .ToListAsync();

        var clinicians = cliniciansList
            .Where(s => !(s.FirstName == "Meron" && s.LastName == "Alemu")
                     && !(s.FirstName == "Tigist" && s.LastName == "Bekele")
                     && !(s.FirstName.ToLower() == "wale" && s.LastName.ToLower() == "solomon"))
            .GroupBy(s => $"{s.FirstName} {s.LastName} ({s.Role})")
            .Select(g => g.First())
            .Select(s => new { s.ID, Name = $"{s.FirstName} {s.LastName} ({s.Role})" })
            .ToList();

        ViewBag.Clinicians = new SelectList(clinicians, "ID", "Name");
    }

    private async Task<List<SelectListItem>> GetActiveNurseSelectListAsync()
    {
        var staffList = await _context.Staffs
            .Where(s => s.IsActive && (s.Role == "Nurse" || s.Role == "HealthOfficer"))
            .OrderBy(s => s.Role).ThenBy(s => s.FirstName).ThenBy(s => s.LastName)
            .ToListAsync();

        return staffList
            .Where(s => !(s.FirstName == "Meron" && s.LastName == "Alemu")
                     && !(s.FirstName == "Tigist" && s.LastName == "Bekele")
                     && !(s.FirstName.ToLower() == "wale" && s.LastName.ToLower() == "solomon"))
            .GroupBy(s => $"{s.FirstName} {s.LastName} ({s.Role})")
            .Select(g => g.First())
            .Select(s => new SelectListItem
            {
                Value = s.ID.ToString(),
                Text = $"{s.FirstName} {s.LastName} ({s.Role})"
            })
            .ToList();
    }

    private async Task<string> GenerateCardNumberAsync()
    {
        var year = DateTime.Now.Year;
        var countThisYear = await _context.Patients
            .CountAsync(p => p.CreatedAt.Year == year);

        var sequence = (countThisYear + 1).ToString("D4");
        return $"HU-{year}-{sequence}";
    }
}
