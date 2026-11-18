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
    [Authorize(Roles = "Nurse,HealthOfficer")]
    public class TriageAssessmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public TriageAssessmentsController(
            ApplicationDbContext context, 
            INotificationService notificationService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _notificationService = notificationService;
            _userManager = userManager;
        }

        // GET: TriageAssessments/Create/5
        public async Task<IActionResult> Create(int encounterId)
        {
            var encounter = await _context.ClinicalEncounters
                .Include(e => e.Patient)
                .FirstOrDefaultAsync(e => e.ID == encounterId);

            if (encounter == null) return NotFound();

            var vm = new TriageAssessment
            {
                EncounterID = encounter.ID,
                Encounter = encounter,
                PriorityLevel = encounter.Type == EncounterType.Emergency ? "Emergency" : "Routine"
            };

            return View(vm);
        }

        // POST: TriageAssessments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TriageAssessment triage)
        {
            var encounter = await _context.ClinicalEncounters
                .Include(e => e.Patient)
                .FirstOrDefaultAsync(e => e.ID == triage.EncounterID);
            if (encounter == null) return NotFound();

            if (ModelState.IsValid)
            {
                _context.Add(triage);

                var isEmergency = triage.PriorityLevel == "Emergency" || encounter.Type == EncounterType.Emergency;

                if (isEmergency)
                {
                    encounter.Type = EncounterType.Emergency;
                    encounter.Status = WorkflowState.InEmergency;
                    
                    // Alert all Health Officers with valid user IDs
                    var healthOfficers = await _userManager.GetUsersInRoleAsync("HealthOfficer");
                    if (healthOfficers.Any())
                    {
                        var patientName = encounter.Patient != null 
                            ? $"{encounter.Patient.FirstName} {encounter.Patient.LastName} ({encounter.Patient.CardNumber ?? encounter.Patient.IDNumber})"
                            : $"ID {encounter.PatientID}";

                        await _notificationService.CreateNotificationForUsersAsync(
                            healthOfficers.Select(ho => ho.Id),
                            "EMERGENCY: Immediate Attention Required",
                            $"Emergency patient {patientName} requires immediate clinical stabilization.",
                            "Emergency",
                            encounter.ID);
                    }
                }
                else
                {
                    encounter.Status = WorkflowState.InConsultation;
                }

                await _context.SaveChangesAsync();
                
                // Ensure the combined Triage & Consultation ticket is marked InService
                var today = DateTime.Today;
                var activeClinicalQueues = await _context.Queues
                    .Include(q => q.ServicePoint)
                    .Where(q => q.PatientID == encounter.PatientID 
                             && q.ServicePoint != null && (q.ServicePoint.Code == "TRI" || q.ServicePoint.Code == "CON") 
                             && q.QueueStatus == "Waiting" && q.CreatedAt >= today)
                    .ToListAsync();

                foreach (var q in activeClinicalQueues)
                {
                    q.QueueStatus = isEmergency ? "Called" : "InService";
                }
                if (activeClinicalQueues.Any())
                {
                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = "Vital signs recorded successfully. Combined clinical consultation in progress.";
                return RedirectToAction("Details", "ClinicalEncounters", new { id = triage.EncounterID });
            }

            triage.Encounter = encounter;
            return View(triage);
        }
    }
}
