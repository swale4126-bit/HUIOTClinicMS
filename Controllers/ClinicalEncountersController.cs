using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.Models;
using HUIOTClinicMS.Models.Enums;
using HUIOTClinicMS.Services;

namespace HUIOTClinicMS.Controllers
{
    [Authorize]
    public class ClinicalEncountersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ClinicalEncountersController(
            ApplicationDbContext context, 
            INotificationService notificationService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _notificationService = notificationService;
            _userManager = userManager;
        }

        // GET: ClinicalEncounters
        public async Task<IActionResult> Index(string? status, string? search, bool? myEncounters)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var myStaffId = currentUser?.StaffID;

            var query = _context.ClinicalEncounters
                .Include(e => e.Patient)
                .Include(e => e.AssignedToStaff)
                .Include(e => e.TriageAssessment)
                .AsQueryable();

            if (myEncounters == true && myStaffId.HasValue)
            {
                query = query.Where(e => e.AssignedToStaffID == myStaffId.Value);
                ViewBag.MyEncounters = true;
            }

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<WorkflowState>(status, out var parsedStatus))
            {
                query = query.Where(e => e.Status == parsedStatus);
                ViewBag.SelectedStatus = status;
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLower();
                query = query.Where(e => e.Patient != null &&
                    (e.Patient.FirstName.ToLower().Contains(term) ||
                     e.Patient.LastName.ToLower().Contains(term) ||
                     e.Patient.IDNumber.ToLower().Contains(term) ||
                     (e.Patient.CardNumber != null && e.Patient.CardNumber.ToLower().Contains(term))));
                ViewBag.SearchTerm = search;
            }

            var encounters = await query
                .OrderByDescending(e => e.Type == EncounterType.Emergency)
                .ThenByDescending(e => e.StartTime)
                .Take(100)
                .ToListAsync();

            return View(encounters);
        }

        // GET: ClinicalEncounters/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var encounter = await _context.ClinicalEncounters
                .AsNoTracking()
                .AsSplitQuery()
                .Include(e => e.Patient)
                    .ThenInclude(p => p!.Department)
                .Include(e => e.Patient)
                    .ThenInclude(p => p!.DormBlock)
                .Include(e => e.AssignedToStaff)
                .Include(e => e.TriageAssessment)
                .Include(e => e.ReferralRecord)
                .Include(e => e.ClinicalNotes)
                    .ThenInclude(n => n.CreatedByStaff)
                .Include(e => e.LabRequests)
                    .ThenInclude(l => l.TestTypes)
                .Include(e => e.LabRequests)
                    .ThenInclude(l => l.AssignedToStaff)
                .Include(e => e.Prescriptions)
                    .ThenInclude(p => p.Medication)
                        .ThenInclude(m => m!.Pharmacist)
                .Include(e => e.Prescriptions)
                    .ThenInclude(p => p.PrescribedBy)
                .Include(e => e.Prescriptions)
                    .ThenInclude(p => p.AssignedToStaff)
                .FirstOrDefaultAsync(m => m.ID == id);

            if (encounter == null) return NotFound();

            // Load any lab results for medical records belonging to this encounter
            var medicalRecordIds = encounter.ClinicalNotes.Select(n => n.ID).ToList();
            var labRequestRecordIds = encounter.LabRequests.Select(l => l.MedicalRecordID).Where(mid => mid > 0).ToList();
            var allRecordIds = medicalRecordIds.Union(labRequestRecordIds).Distinct().ToList();

            List<LabResult> labResults = new();
            if (allRecordIds.Count > 0)
            {
                labResults = await _context.LabResults
                    .AsNoTracking()
                    .Include(r => r.TestType)
                    .Include(r => r.ResultPerformedBy)
                    .Where(r => allRecordIds.Contains(r.MedicalRecordID))
                    .ToListAsync();
            }

            ViewBag.LabResults = labResults;

            return View(encounter);
        }

        // POST: ClinicalEncounters/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Nurse,HealthOfficer")]
        public async Task<IActionResult> Create(int patientId, EncounterType type = EncounterType.Routine)
        {
            var patient = await _context.Patients.FindAsync(patientId);
            if (patient == null) return NotFound();

            var activeEncounter = await _context.ClinicalEncounters
                .Where(e => e.PatientID == patientId && e.Status != WorkflowState.Discharged && e.Status != WorkflowState.Transferred)
                .OrderByDescending(e => e.StartTime)
                .FirstOrDefaultAsync();

            if (activeEncounter != null)
            {
                if (type == EncounterType.Emergency && activeEncounter.Type != EncounterType.Emergency)
                {
                    activeEncounter.Type = EncounterType.Emergency;
                    if (activeEncounter.Status == WorkflowState.Registered || activeEncounter.Status == WorkflowState.WaitingConsultation)
                    {
                        activeEncounter.Status = WorkflowState.InEmergency;
                    }
                    await _context.SaveChangesAsync();

                    var healthOfficers = await _userManager.GetUsersInRoleAsync("HealthOfficer");
                    if (healthOfficers.Any())
                    {
                        await _notificationService.CreateNotificationForUsersAsync(
                            healthOfficers.Select(h => h.Id),
                            "STAT EMERGENCY ESCALATION",
                            $"Patient {patient.FirstName} {patient.LastName} ({patient.CardNumber ?? patient.IDNumber}) escalated to STAT EMERGENCY.",
                            "Emergency",
                            activeEncounter.ID);
                    }

                    TempData["SuccessMessage"] = $"Existing visit (Encounter #{activeEncounter.ID}) escalated to STAT Emergency.";
                    return RedirectToAction("Create", "TriageAssessments", new { encounterId = activeEncounter.ID });
                }

                TempData["SuccessMessage"] = $"Resumed active visit (Encounter #{activeEncounter.ID}).";
                return RedirectToAction(nameof(Details), new { id = activeEncounter.ID });
            }

            var currentUser = await _userManager.GetUserAsync(User);

            var encounter = new ClinicalEncounter
            {
                PatientID = patientId,
                StartTime = DateTime.UtcNow,
                Type = type,
                Status = type == EncounterType.Emergency ? WorkflowState.InEmergency : WorkflowState.Registered,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = currentUser?.FullName ?? User.Identity?.Name
            };

            _context.ClinicalEncounters.Add(encounter);
            await _context.SaveChangesAsync();

            if (type == EncounterType.Emergency)
            {
                // Immediate notification to HealthOfficers
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

                // Route directly to Consultation / Emergency bay with Emergency priority
                var conSp = await _context.ServicePoints.FirstOrDefaultAsync(s => s.Code == "CON");
                if (conSp != null)
                {
                    var today = DateTime.Today;
                    var queueCountToday = await _context.Queues.CountAsync(q => q.ServicePointID == conSp.ID && q.CreatedAt >= today);
                    var queue = new Queue
                    {
                        PatientID = patient.ID,
                        ServicePointID = conSp.ID,
                        TokenNumber = $"{conSp.Code}-{(queueCountToday + 1):D3}",
                        PriorityLevel = "Emergency",
                        QueueStatus = "Called",
                        QueueType = "Emergency",
                        CreatedAt = DateTime.Now
                    };
                    _context.Queues.Add(queue);
                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = "Emergency encounter initiated. High-priority alerts dispatched.";
                return RedirectToAction("Create", "TriageAssessments", new { encounterId = encounter.ID });
            }
            else
            {
                // Enqueue for Triage / Registration
                var targetSp = await _context.ServicePoints.FirstOrDefaultAsync(s => s.Code == "TRI") 
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
                        PriorityLevel = "Normal",
                        QueueStatus = "Waiting",
                        QueueType = "Routine",
                        CreatedAt = DateTime.Now
                    };
                    _context.Queues.Add(queue);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Routine encounter initiated. Ticket: {tokenNumber}";
                }

                return RedirectToAction(nameof(Details), new { id = encounter.ID });
            }
        }

        // POST: ClinicalEncounters/Discharge/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "HealthOfficer,Nurse,Pharmacist")]
        public async Task<IActionResult> Discharge(int id)
        {
            var encounter = await _context.ClinicalEncounters.FindAsync(id);
            if (encounter == null) return NotFound();

            encounter.Status = WorkflowState.Discharged;
            encounter.EndTime = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Patient encounter concluded and marked as Discharged.";

            return RedirectToAction(nameof(Details), new { id = encounter.ID });
        }
    }
}
