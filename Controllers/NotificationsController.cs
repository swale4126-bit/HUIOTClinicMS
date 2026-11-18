using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.Services;

namespace HUIOTClinicMS.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly INotificationService _notificationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public NotificationsController(INotificationService notificationService, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _notificationService = notificationService;
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var notifications = _context.Notifications
                .Where(n => n.UserID == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .ToList();

            return View(notifications);
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                await _notificationService.MarkAllAsReadAsync(user.Id);
            }
            
            return Redirect(Request.Headers["Referer"].ToString() ?? "/");
        }

        public async Task<IActionResult> ViewRecord(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var notification = await _context.Notifications.FindAsync(id);
            if (notification == null) return RedirectToAction(nameof(Index));

            if (notification.UserID != user.Id)
            {
                return Forbid();
            }

            await _notificationService.MarkAsReadAsync(id);

            if (notification.RelatedRecordID.HasValue)
            {
                switch (notification.NotificationType)
                {
                    case "LabResult":
                        var labResult = await _context.LabResults
                            .Include(r => r.MedicalRecord)
                            .FirstOrDefaultAsync(r => r.ID == notification.RelatedRecordID.Value);
                        if (labResult?.MedicalRecord?.EncounterID.HasValue == true)
                        {
                            return RedirectToAction("Details", "ClinicalEncounters", new { id = labResult.MedicalRecord.EncounterID.Value });
                        }
                        if (labResult != null)
                        {
                            return RedirectToAction("Details", "LabResults", new { id = labResult.ID });
                        }
                        break;

                    case "LabRequest":
                        if (User.IsInRole("LabTechnician"))
                        {
                            return RedirectToAction("Create", "LabResults", new { labRequestId = notification.RelatedRecordID.Value });
                        }
                        var req = await _context.LabRequests.FindAsync(notification.RelatedRecordID.Value);
                        if (req?.ClinicalEncounterID.HasValue == true)
                        {
                            return RedirectToAction("Details", "ClinicalEncounters", new { id = req.ClinicalEncounterID.Value });
                        }
                        return RedirectToAction("Details", "LabRequests", new { id = notification.RelatedRecordID.Value });

                    case "PatientHandover":
                        var enc = await _context.ClinicalEncounters
                            .Where(e => e.PatientID == notification.RelatedRecordID.Value && e.Status != HUIOTClinicMS.Models.Enums.WorkflowState.Discharged && e.Status != HUIOTClinicMS.Models.Enums.WorkflowState.Transferred)
                            .OrderByDescending(e => e.StartTime)
                            .FirstOrDefaultAsync();
                        if (enc != null)
                        {
                            return RedirectToAction("Details", "ClinicalEncounters", new { id = enc.ID });
                        }
                        return RedirectToAction("Details", "Patients", new { id = notification.RelatedRecordID.Value });

                    case "Prescription":
                        var rx = await _context.Prescriptions.FindAsync(notification.RelatedRecordID.Value);
                        if (rx?.ClinicalEncounterID.HasValue == true)
                        {
                            return RedirectToAction("Details", "ClinicalEncounters", new { id = rx.ClinicalEncounterID.Value });
                        }
                        return RedirectToAction("Details", "Prescriptions", new { id = notification.RelatedRecordID.Value });

                    case "Emergency":
                        return RedirectToAction("Details", "ClinicalEncounters", new { id = notification.RelatedRecordID.Value });
                }
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
