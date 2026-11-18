using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.Models;
using HUIOTClinicMS.Models.Enums;
using HUIOTClinicMS.Services;
using HUIOTClinicMS.ViewModels;

namespace HUIOTClinicMS.Controllers
{
    [Authorize(Roles = "HealthOfficer,Nurse,LabTechnician,Pharmacist")]
    public class TaskAssignmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;

        public TaskAssignmentController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        // GET: TaskAssignment/Reassign?taskType=PatientEncounter&taskId=5
        public async Task<IActionResult> Reassign(string taskType, int taskId)
        {
            if (string.IsNullOrWhiteSpace(taskType) || taskId <= 0)
            {
                return BadRequest();
            }

            var vm = new ReassignTaskViewModel
            {
                TaskType = taskType,
                TaskID = taskId
            };

            var loaded = await PopulateTaskDetailsAsync(vm);
            if (!loaded)
            {
                return NotFound();
            }

            return View(vm);
        }

        // POST: TaskAssignment/Reassign
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reassign(ReassignTaskViewModel vm)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var targetStaff = await _context.Staffs.FirstOrDefaultAsync(s => s.ID == vm.NewAssignedStaffID && s.IsActive);
            if (targetStaff == null)
            {
                ModelState.AddModelError(nameof(vm.NewAssignedStaffID), "The selected staff member is invalid or currently deactivated.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateTaskDetailsAsync(vm);
                return View(vm);
            }

            int? previousStaffId = null;
            int? redirectEncounterId = null;
            string notificationTitle = "Task Reassignment";
            string notificationMessage = string.Empty;

            switch (vm.TaskType)
            {
                case "PatientEncounter":
                    var encounter = await _context.ClinicalEncounters
                        .Include(e => e.Patient)
                        .FirstOrDefaultAsync(e => e.ID == vm.TaskID);
                    if (encounter == null) return NotFound();

                    previousStaffId = encounter.AssignedToStaffID;
                    encounter.AssignedToStaffID = vm.NewAssignedStaffID;
                    redirectEncounterId = encounter.ID;

                    notificationTitle = "Patient Encounter Reassigned to You";
                    notificationMessage = $"Patient {encounter.Patient?.FirstName} {encounter.Patient?.LastName} (Encounter #{encounter.ID}) has been assigned to you by {currentUser.FullName}. Reason: {vm.Reason ?? "Clinical re-routing"}";
                    break;

                case "LabRequest":
                    var labRequest = await _context.LabRequests
                        .Include(l => l.Patient)
                        .Include(l => l.TestTypes)
                        .FirstOrDefaultAsync(l => l.LabRequestID == vm.TaskID);
                    if (labRequest == null) return NotFound();

                    previousStaffId = labRequest.AssignedToStaffID;
                    labRequest.AssignedToStaffID = vm.NewAssignedStaffID;
                    redirectEncounterId = labRequest.ClinicalEncounterID;

                    notificationTitle = "Lab Request Assigned to You";
                    notificationMessage = $"Lab Request #{labRequest.LabRequestID} for {labRequest.Patient?.FirstName} {labRequest.Patient?.LastName} has been assigned to you by {currentUser.FullName}. Reason: {vm.Reason ?? "Workload balancing"}";
                    break;

                case "Prescription":
                    var prescription = await _context.Prescriptions
                        .Include(p => p.Patient)
                        .FirstOrDefaultAsync(p => p.PrescriptionID == vm.TaskID);
                    if (prescription == null) return NotFound();

                    previousStaffId = prescription.AssignedToStaffID;
                    prescription.AssignedToStaffID = vm.NewAssignedStaffID;
                    redirectEncounterId = prescription.ClinicalEncounterID;

                    notificationTitle = "Prescription Assigned to You";
                    notificationMessage = $"Prescription #{prescription.PrescriptionID} for {prescription.Patient?.FirstName} {prescription.Patient?.LastName} has been assigned to you by {currentUser.FullName}. Reason: {vm.Reason ?? "Workload balancing"}";
                    break;

                case "LabResultHandover":
                    var labReqHandover = await _context.LabRequests
                        .Include(l => l.Patient)
                        .Include(l => l.TestTypes)
                        .FirstOrDefaultAsync(l => l.LabRequestID == vm.TaskID);
                    if (labReqHandover == null) return NotFound();

                    var latestHandover = await _context.TaskAssignmentHistories
                        .Where(h => h.TaskType == "LabResultHandover" && h.TaskID == vm.TaskID)
                        .OrderByDescending(h => h.AssignedAt)
                        .FirstOrDefaultAsync();

                    previousStaffId = latestHandover?.AssignedToStaffID;
                    redirectEncounterId = labReqHandover.ClinicalEncounterID;

                    if (labReqHandover.ClinicalEncounterID.HasValue)
                    {
                        var enc = await _context.ClinicalEncounters.FindAsync(labReqHandover.ClinicalEncounterID.Value);
                        if (enc != null && enc.Status != WorkflowState.Discharged)
                        {
                            enc.AssignedToStaffID = vm.NewAssignedStaffID;
                        }
                    }

                    notificationTitle = "Lab Result Review Assigned to You";
                    notificationMessage = $"Lab Result Review for {labReqHandover.Patient?.FirstName} {labReqHandover.Patient?.LastName} has been assigned to you by {currentUser.FullName}. Reason: {vm.Reason ?? "Clinical workload re-routing"}";
                    break;

                default:
                    return BadRequest("Unsupported task type.");
            }

            // Record assignment audit entry
            var history = new TaskAssignmentHistory
            {
                TaskType = vm.TaskType,
                TaskID = vm.TaskID,
                AssignedFromStaffID = previousStaffId,
                AssignedToStaffID = vm.NewAssignedStaffID,
                AssignedByID = currentUser.Id,
                AssignedAt = DateTime.UtcNow,
                Reason = vm.Reason
            };

            _context.TaskAssignmentHistories.Add(history);
            await _context.SaveChangesAsync();

            // Notify newly assigned user
            var assignedUser = await _context.Users.FirstOrDefaultAsync(u => u.StaffID == vm.NewAssignedStaffID);
            if (assignedUser != null)
            {
                await _notificationService.CreateNotificationAsync(
                    assignedUser.Id,
                    notificationTitle,
                    notificationMessage,
                    vm.TaskType,
                    vm.TaskID);
            }

            TempData["SuccessMessage"] = $"Task successfully assigned to {targetStaff!.FirstName} {targetStaff.LastName}.";

            if (redirectEncounterId.HasValue)
            {
                return RedirectToAction("Details", "ClinicalEncounters", new { id = redirectEncounterId.Value });
            }

            return vm.TaskType switch
            {
                "LabRequest" => RedirectToAction("Details", "LabRequests", new { id = vm.TaskID }),
                "Prescription" => RedirectToAction("Details", "Prescriptions", new { id = vm.TaskID }),
                _ => RedirectToAction("Index", "ClinicalEncounters")
            };
        }

        private async Task<bool> PopulateTaskDetailsAsync(ReassignTaskViewModel vm)
        {
            switch (vm.TaskType)
            {
                case "PatientEncounter":
                    var encounter = await _context.ClinicalEncounters
                        .Include(e => e.Patient)
                        .Include(e => e.AssignedToStaff)
                        .FirstOrDefaultAsync(e => e.ID == vm.TaskID);
                    if (encounter == null) return false;

                    vm.TaskTitle = $"Clinical Encounter #{encounter.ID} ({encounter.Type})";
                    vm.PatientName = encounter.Patient != null ? $"{encounter.Patient.FirstName} {encounter.Patient.LastName} ({encounter.Patient.CardNumber ?? encounter.Patient.IDNumber})" : "—";
                    vm.CurrentAssignedStaffID = encounter.AssignedToStaffID;
                    vm.CurrentAssignedStaffName = encounter.AssignedToStaff != null
                        ? $"{encounter.AssignedToStaff.FirstName} {encounter.AssignedToStaff.LastName} ({encounter.AssignedToStaff.Role})"
                        : "Unassigned (General Queue)";

                    vm.EligibleStaff = await _context.Staffs
                        .Where(s => s.IsActive && (s.Role == "HealthOfficer" || s.Role == "Nurse"))
                        .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
                        .ToListAsync();
                    break;

                case "LabRequest":
                    var labReq = await _context.LabRequests
                        .Include(l => l.Patient)
                        .Include(l => l.AssignedToStaff)
                        .Include(l => l.TestTypes)
                        .FirstOrDefaultAsync(l => l.LabRequestID == vm.TaskID);
                    if (labReq == null) return false;

                    var testNames = labReq.TestTypes.Any() ? string.Join(", ", labReq.TestTypes.Select(t => t.TestName)) : "General Tests";
                    vm.TaskTitle = $"Lab Request #{labReq.LabRequestID} ({testNames})";
                    vm.PatientName = labReq.Patient != null ? $"{labReq.Patient.FirstName} {labReq.Patient.LastName} ({labReq.Patient.CardNumber ?? labReq.Patient.IDNumber})" : "—";
                    vm.CurrentAssignedStaffID = labReq.AssignedToStaffID;
                    vm.CurrentAssignedStaffName = labReq.AssignedToStaff != null
                        ? $"{labReq.AssignedToStaff.FirstName} {labReq.AssignedToStaff.LastName} ({labReq.AssignedToStaff.Role})"
                        : "Unassigned (General Lab Queue)";

                    vm.EligibleStaff = await _context.Staffs
                        .Where(s => s.IsActive && s.Role == "LabTechnician")
                        .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
                        .ToListAsync();
                    break;

                case "Prescription":
                    var rx = await _context.Prescriptions
                        .Include(p => p.Patient)
                        .Include(p => p.AssignedToStaff)
                        .FirstOrDefaultAsync(p => p.PrescriptionID == vm.TaskID);
                    if (rx == null) return false;

                    vm.TaskTitle = $"Prescription #{rx.PrescriptionID} ({rx.Status})";
                    vm.PatientName = rx.Patient != null ? $"{rx.Patient.FirstName} {rx.Patient.LastName} ({rx.Patient.CardNumber ?? rx.Patient.IDNumber})" : "—";
                    vm.CurrentAssignedStaffID = rx.AssignedToStaffID;
                    vm.CurrentAssignedStaffName = rx.AssignedToStaff != null
                        ? $"{rx.AssignedToStaff.FirstName} {rx.AssignedToStaff.LastName} ({rx.AssignedToStaff.Role})"
                        : "Unassigned (General Pharmacy Queue)";

                    vm.EligibleStaff = await _context.Staffs
                        .Where(s => s.IsActive && s.Role == "Pharmacist")
                        .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
                        .ToListAsync();
                    break;

                case "LabResultHandover":
                    var lreq = await _context.LabRequests
                        .Include(l => l.Patient)
                        .Include(l => l.TestTypes)
                        .FirstOrDefaultAsync(l => l.LabRequestID == vm.TaskID);
                    if (lreq == null) return false;

                    var lastHandover = await _context.TaskAssignmentHistories
                        .Include(h => h.AssignedToStaff)
                        .Where(h => h.TaskType == "LabResultHandover" && h.TaskID == vm.TaskID)
                        .OrderByDescending(h => h.AssignedAt)
                        .FirstOrDefaultAsync();

                    var testList = lreq.TestTypes.Any() ? string.Join(", ", lreq.TestTypes.Select(t => t.TestName)) : "Lab Results";
                    vm.TaskTitle = $"Lab Result Review #{lreq.LabRequestID} ({testList})";
                    vm.PatientName = lreq.Patient != null ? $"{lreq.Patient.FirstName} {lreq.Patient.LastName} ({lreq.Patient.CardNumber ?? lreq.Patient.IDNumber})" : "—";
                    vm.CurrentAssignedStaffID = lastHandover?.AssignedToStaffID;
                    vm.CurrentAssignedStaffName = lastHandover?.AssignedToStaff != null
                        ? $"{lastHandover.AssignedToStaff.FirstName} {lastHandover.AssignedToStaff.LastName} ({lastHandover.AssignedToStaff.Role})"
                        : "Unassigned";

                    vm.EligibleStaff = await _context.Staffs
                        .Where(s => s.IsActive && (s.Role == "Nurse" || s.Role == "HealthOfficer"))
                        .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
                        .ToListAsync();
                    break;

                default:
                    return false;
            }

            vm.History = await _context.TaskAssignmentHistories
                .Include(h => h.AssignedFromStaff)
                .Include(h => h.AssignedToStaff)
                .Include(h => h.AssignedByUser)
                .Where(h => h.TaskType == vm.TaskType && h.TaskID == vm.TaskID)
                .OrderByDescending(h => h.AssignedAt)
                .ToListAsync();

            return true;
        }
    }
}
