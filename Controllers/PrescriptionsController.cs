using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HUIOTClinicMS.Models;
using HUIOTClinicMS.Models.Enums;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.ViewModels;

namespace HUIOTClinicMS.Controllers
{
    [Authorize(Roles = "Nurse,Pharmacist,HealthOfficer")]
    public class PrescriptionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly HUIOTClinicMS.Services.INotificationService _notificationService;

        public PrescriptionsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, HUIOTClinicMS.Services.INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        // GET: PRESCRIPTIONS
        public async Task<IActionResult> Index(string? search)
        {
            var query = _context.Prescriptions
                .Include(p => p.Patient)
                .Include(p => p.PrescribedBy)
                .Include(p => p.AssignedToStaff)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLower();
                query = query.Where(p => p.RX != null && p.RX.ToLower().Contains(term));
            }

            var prescriptions = await query
                .OrderByDescending(p => p.TimeDate)
                .ToListAsync();

            ViewBag.SearchTerm = search;
            return View(prescriptions);
        }

        // GET: PRESCRIPTIONS/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var prescription = await _context.Prescriptions
                .Include(p => p.Patient)
                .Include(p => p.PrescribedBy)
                .Include(p => p.AssignedToStaff)
                .Include(p => p.LabResult)
                    .ThenInclude(l => l!.TestType)
                .Include(p => p.Medication)
                    .ThenInclude(m => m!.Pharmacist)
                .FirstOrDefaultAsync(m => m.PrescriptionID == id);

            if (prescription == null) return NotFound();

            return View(prescription);
        }

        // GET: PRESCRIPTIONS/Create?encounterId=5&patientId=2&labResultId=3
        [Authorize(Roles = "HealthOfficer,Nurse")]
        public async Task<IActionResult> Create(int? encounterId, int? patientId, int? labResultId)
        {
            Patient? patient = null;
            ClinicalEncounter? encounter = null;
            LabResult? labResult = null;
            string? testTypeName = null;
            string? labResultSummary = null;
            string? clinicalNotesSummary = null;

            if (encounterId.HasValue)
            {
                encounter = await _context.ClinicalEncounters
                    .Include(e => e.Patient)
                    .Include(e => e.ClinicalNotes)
                    .Include(e => e.LabRequests)
                    .FirstOrDefaultAsync(e => e.ID == encounterId.Value);

                if (encounter != null)
                {
                    patient = encounter.Patient;
                    var latestNote = encounter.ClinicalNotes.OrderByDescending(n => n.CreatedAt).FirstOrDefault();
                    if (latestNote != null)
                    {
                        clinicalNotesSummary = $"Diagnosis: {latestNote.Diagnosis} | Complaint: {latestNote.ChiefComplaint}";
                    }

                    if (!labResultId.HasValue && encounter.LabRequests.Any())
                    {
                        var completedReq = encounter.LabRequests.Where(r => r.Status == LabRequestStatuses.Completed).OrderByDescending(r => r.RequestDate).FirstOrDefault();
                        if (completedReq != null)
                        {
                            var autoLabResult = await _context.LabResults
                                .Include(l => l.TestType)
                                .Where(l => l.MedicalRecordID == completedReq.MedicalRecordID)
                                .OrderByDescending(l => l.ID)
                                .FirstOrDefaultAsync();

                            if (autoLabResult != null)
                            {
                                labResult = autoLabResult;
                                labResultId = autoLabResult.ID;
                                testTypeName = autoLabResult.TestType?.TestName;
                                labResultSummary = autoLabResult.Result;
                            }
                        }
                    }
                }
            }

            if (labResultId.HasValue)
            {
                labResult = await _context.LabResults
                    .Include(l => l.MedicalRecord)
                        .ThenInclude(m => m!.Patient)
                    .Include(l => l.TestType)
                    .FirstOrDefaultAsync(l => l.ID == labResultId.Value);

                if (labResult != null)
                {
                    patient ??= labResult.MedicalRecord?.Patient;
                    testTypeName = labResult.TestType?.TestName;
                    labResultSummary = labResult.Result;

                    if (encounter == null && labResult.MedicalRecord?.EncounterID != null)
                    {
                        encounter = await _context.ClinicalEncounters.FindAsync(labResult.MedicalRecord.EncounterID);
                    }
                }
            }

            if (patient == null && patientId.HasValue)
            {
                patient = await _context.Patients.FindAsync(patientId.Value);
            }

            if (patient == null)
            {
                TempData["ErrorMessage"] = "A valid patient or clinical encounter must be selected to issue a prescription.";
                return RedirectToAction(nameof(Index));
            }

            var activePharmacists = await _context.Staffs
                .Where(s => s.Role == "Pharmacist" && s.IsActive)
                .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
                .ToListAsync();

            var vm = new PrescriptionCreateViewModel
            {
                EncounterID = encounter?.ID,
                PatientID = patient.ID,
                PatientDisplayName = $"{patient.FirstName} {patient.LastName} ({patient.CardNumber ?? patient.IDNumber})",
                LabResultID = labResult?.ID,
                TestTypeName = testTypeName ?? "—",
                LabResultSummary = labResultSummary ?? "—",
                ClinicalNotesSummary = clinicalNotesSummary,
                AvailablePharmacists = activePharmacists
            };

            return View(vm);
        }

        // POST: PRESCRIPTIONS/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "HealthOfficer,Nurse")]
        public async Task<IActionResult> Create(PrescriptionCreateViewModel vm)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.StaffID == null)
            {
                ModelState.AddModelError(string.Empty,
                    "Your account isn't linked to a Staff record, so this prescription can't be attributed to you.");
            }

            var patient = await _context.Patients.FindAsync(vm.PatientID);
            if (patient == null)
            {
                ModelState.AddModelError(string.Empty, "The specified patient could not be found.");
            }

            ClinicalEncounter? encounter = null;
            if (vm.EncounterID.HasValue)
            {
                encounter = await _context.ClinicalEncounters.FindAsync(vm.EncounterID.Value);
            }

            LabResult? labResult = null;
            if (vm.LabResultID.HasValue)
            {
                labResult = await _context.LabResults
                    .Include(l => l.TestType)
                    .FirstOrDefaultAsync(l => l.ID == vm.LabResultID.Value);

                if (labResult != null && await _context.Prescriptions.AnyAsync(p => p.LabResultID == labResult.ID))
                {
                    ModelState.AddModelError(string.Empty, "A prescription has already been created for this specific lab result.");
                }
            }

            // Queue check: If encounter is Emergency or priority is Emergency, bypass queue check.
            bool isEmergency = encounter != null && (encounter.Type == EncounterType.Emergency || encounter.Status == WorkflowState.InEmergency);
            if (!isEmergency && vm.PatientID != 0)
            {
                var isPatientInService = await _context.Queues
                    .Include(q => q.ServicePoint)
                    .AnyAsync(q => q.PatientID == vm.PatientID && (q.QueueStatus == "InService" || q.QueueStatus == "Called") && q.ServicePoint != null && q.ServicePoint.Code == "CON");

                var hasActiveEncounter = encounter != null && encounter.Status != WorkflowState.Discharged;
                if (!isPatientInService && !hasActiveEncounter)
                {
                    ModelState.AddModelError(string.Empty, "The patient must be called in Consultation before prescribing, or have an active clinical encounter.");
                }
            }

            // Server-side validation of assigned pharmacist
            if (vm.AssignedToStaffID.HasValue)
            {
                var assignedPharmacist = await (from s in _context.Staffs
                                                join u in _context.Users on s.ID equals u.StaffID
                                                where s.ID == vm.AssignedToStaffID.Value
                                                   && s.IsActive && s.Role == "Pharmacist"
                                                   && (u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow)
                                                select s).FirstOrDefaultAsync();
                if (assignedPharmacist == null)
                {
                    ModelState.AddModelError(nameof(vm.AssignedToStaffID), "The selected pharmacist is invalid, deactivated, or locked out.");
                }
            }
            else
            {
                ModelState.AddModelError(nameof(vm.AssignedToStaffID), "Please select a pharmacist to receive this prescription.");
            }

            if (!ModelState.IsValid)
            {
                if (patient != null)
                {
                    vm.PatientDisplayName = $"{patient.FirstName} {patient.LastName} ({patient.CardNumber ?? patient.IDNumber})";
                }
                vm.AvailablePharmacists = await _context.Staffs
                    .Where(s => s.Role == "Pharmacist" && s.IsActive)
                    .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
                    .ToListAsync();
                return View(vm);
            }

            var prescription = new Prescription
            {
                PatientID = vm.PatientID,
                ClinicalEncounterID = vm.EncounterID,
                LabResultID = vm.LabResultID,
                RX = vm.RX,
                PrescribedByID = currentUser!.StaffID!.Value,
                TimeDate = DateTime.Now,
                Status = "Pending",
                AssignedToStaffID = vm.AssignedToStaffID
            };

            _context.Prescriptions.Add(prescription);

            if (encounter != null && encounter.Status != WorkflowState.Discharged)
            {
                encounter.Status = WorkflowState.WaitingPharmacy;
            }

            await _context.SaveChangesAsync();

            // Record assignment history if a pharmacist was specifically selected
            if (vm.AssignedToStaffID.HasValue)
            {
                _context.TaskAssignmentHistories.Add(new TaskAssignmentHistory
                {
                    TaskType = "Prescription",
                    TaskID = prescription.PrescriptionID,
                    AssignedToStaffID = vm.AssignedToStaffID.Value,
                    AssignedByID = currentUser.Id,
                    AssignedAt = DateTime.UtcNow,
                    Reason = "Initial assignment at prescription creation"
                });
                await _context.SaveChangesAsync();
            }

            // Close any active Consultation or Triage queues since patient is now directed to Pharmacy
            var activeConQueues = await _context.Queues
                .Include(q => q.ServicePoint)
                .Where(q => q.PatientID == vm.PatientID 
                         && q.ServicePoint != null && (q.ServicePoint.Code == "CON" || q.ServicePoint.Code == "TRI")
                         && (q.QueueStatus == "InService" || q.QueueStatus == "Waiting" || q.QueueStatus == "Called"))
                .ToListAsync();

            foreach (var cq in activeConQueues)
            {
                cq.QueueStatus = "Completed";
                var log = await _context.ServiceLogs.FirstOrDefaultAsync(l => l.QueueID == cq.ID);
                if (log != null && log.ServiceEndedAt == null)
                {
                    log.ServiceEndedAt = DateTime.Now;
                }
            }

            // Automatically create a Pharmacy Queue ticket if none exists for today
            var pharmacyServicePoint = await _context.ServicePoints.FirstOrDefaultAsync(s => s.Code == "PHA" || s.Name.Contains("Pharmacy"));
            if (pharmacyServicePoint != null)
            {
                var today = DateTime.Today;
                var hasWaitingPharmacyQueue = await _context.Queues
                    .AnyAsync(q => q.PatientID == vm.PatientID && q.ServicePointID == pharmacyServicePoint.ID && q.CreatedAt >= today && q.QueueStatus == "Waiting");

                if (!hasWaitingPharmacyQueue)
                {
                    var todaysPharmacyQueuesCount = await _context.Queues
                        .Where(q => q.ServicePointID == pharmacyServicePoint.ID && q.CreatedAt >= today)
                        .CountAsync();

                    var newQueue = new Queue
                    {
                        TokenNumber = $"{pharmacyServicePoint.Code}-{todaysPharmacyQueuesCount + 1:D3}",
                        PatientID = vm.PatientID,
                        ServicePointID = pharmacyServicePoint.ID,
                        PriorityLevel = isEmergency ? "Emergency" : "Normal",
                        QueueStatus = "Waiting",
                        QueueType = "Pharmacy",
                        CreatedAt = DateTime.Now
                    };

                    _context.Queues.Add(newQueue);
                    await _context.SaveChangesAsync();
                }
            }

            // Notify Pharmacist(s)
            var patientInfo = patient != null ? $"{patient.FirstName} {patient.LastName} (Card No: {patient.CardNumber ?? patient.IDNumber})" : "Unknown Patient";
            string notifTitle = isEmergency ? "STAT EMERGENCY: Prescription Dispensing Required" : "New Prescription";
            string notifMessage = $"New prescription for {patientInfo} by {currentUser.FullName}.";

            if (vm.AssignedToStaffID.HasValue)
            {
                // Notify the specifically assigned pharmacist
                var assignedPharmacist = await _context.Users.FirstOrDefaultAsync(u => u.StaffID == vm.AssignedToStaffID.Value);
                if (assignedPharmacist != null)
                {
                    await _notificationService.CreateNotificationAsync(assignedPharmacist.Id, notifTitle,
                        $"You have been assigned a prescription: {notifMessage}", "Prescription", prescription.PrescriptionID);
                }
            }
            else
            {
                // No specific assignment — notify all active pharmacists
                var pharmacists = await _userManager.GetUsersInRoleAsync("Pharmacist");
                var pharmacistIds = pharmacists.Select(p => p.Id).ToList();
                await _notificationService.CreateNotificationForUsersAsync(pharmacistIds, notifTitle, notifMessage, "Prescription", prescription.PrescriptionID);
            }

            TempData["SuccessMessage"] = $"Prescription for {patientInfo} created and queued for pharmacy dispensing.";
            return RedirectToAction(nameof(Confirmation), new { id = prescription.PrescriptionID });
        }

        // GET: PRESCRIPTIONS/Confirmation/5
        [Authorize(Roles = "Nurse,HealthOfficer,Pharmacist")]
        public async Task<IActionResult> Confirmation(int? id)
        {
            if (id == null) return NotFound();

            var prescription = await _context.Prescriptions
                .Include(p => p.Patient)
                    .ThenInclude(pt => pt.Department)
                .Include(p => p.Patient)
                    .ThenInclude(pt => pt.DormBlock)
                .Include(p => p.PrescribedBy)
                .Include(p => p.AssignedToStaff)
                .Include(p => p.Medication)
                .FirstOrDefaultAsync(p => p.PrescriptionID == id);

            if (prescription == null) return NotFound();

            return View(prescription);
        }

        // GET: PRESCRIPTIONS/Edit/5
        [Authorize(Roles = "Nurse,HealthOfficer")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var prescription = await _context.Prescriptions
                .Include(p => p.Patient)
                .Include(p => p.PrescribedBy)
                .FirstOrDefaultAsync(p => p.PrescriptionID == id);

            if (prescription == null) return NotFound();

            if (prescription.Status == "Dispensed")
            {
                TempData["ErrorMessage"] = "This prescription has already been dispensed and can no longer be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var vm = new PrescriptionEditViewModel
            {
                PrescriptionID = prescription.PrescriptionID,
                PatientDisplayName = prescription.Patient != null ? $"{prescription.Patient.FirstName} {prescription.Patient.LastName}" : "—",
                PrescribedByDisplayName = prescription.PrescribedBy != null ? $"{prescription.PrescribedBy.FirstName} {prescription.PrescribedBy.LastName}" : "—",
                TimeDate = prescription.TimeDate,
                Status = prescription.Status,
                RX = prescription.RX
            };

            return View(vm);
        }

        // POST: PRESCRIPTIONS/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Nurse,HealthOfficer")]
        public async Task<IActionResult> Edit(int id, PrescriptionEditViewModel vm)
        {
            if (id != vm.PrescriptionID) return NotFound();

            var prescription = await _context.Prescriptions.FindAsync(id);
            if (prescription == null) return NotFound();

            if (prescription.Status == "Dispensed")
            {
                TempData["ErrorMessage"] = "This prescription has already been dispensed and can no longer be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            prescription.RX = vm.RX;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PrescriptionExists(id)) return NotFound();
                throw;
            }

            TempData["SuccessMessage"] = "Prescription updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: PRESCRIPTIONS/Delete/5
        [Authorize(Roles = "HealthOfficer")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var prescription = await _context.Prescriptions
                .Include(p => p.Patient)
                .FirstOrDefaultAsync(m => m.PrescriptionID == id);

            if (prescription == null) return NotFound();

            return View(prescription);
        }

        // POST: PRESCRIPTIONS/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "HealthOfficer")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var prescription = await _context.Prescriptions.FindAsync(id);
            if (prescription != null)
            {
                if (prescription.Status == "Dispensed")
                {
                    TempData["ErrorMessage"] = "Cannot cancel or delete a prescription that has already been dispensed.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                prescription.Status = "Cancelled";
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Prescription has been cancelled.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: PRESCRIPTIONS/Dispense/5
        [Authorize(Roles = "Pharmacist")]
        public async Task<IActionResult> Dispense(int? id)
        {
            if (id == null) return NotFound();

            var prescription = await _context.Prescriptions
                .Include(p => p.Patient)
                .Include(p => p.Medication)
                .FirstOrDefaultAsync(p => p.PrescriptionID == id);

            if (prescription == null) return NotFound();

            if (prescription.Status == "Dispensed" || prescription.Medication != null)
            {
                TempData["ErrorMessage"] = "This prescription has already been dispensed.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var vm = new MedicationDispenseViewModel
            {
                PrescriptionID = prescription.PrescriptionID,
                PatientDisplayName = prescription.Patient != null ? $"{prescription.Patient.FirstName} {prescription.Patient.LastName}" : "—",
                RX = prescription.RX
            };

            return View(vm);
        }

        // POST: PRESCRIPTIONS/Dispense/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Pharmacist")]
        public async Task<IActionResult> Dispense(int id, MedicationDispenseViewModel vm)
        {
            if (id != vm.PrescriptionID) return NotFound();

            var prescription = await _context.Prescriptions
                .Include(p => p.Patient)
                .Include(p => p.Medication)
                .FirstOrDefaultAsync(p => p.PrescriptionID == id);

            if (prescription == null) return NotFound();

            if (prescription.Status == "Dispensed" || prescription.Medication != null)
            {
                TempData["ErrorMessage"] = "This prescription has already been dispensed.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.StaffID == null)
            {
                ModelState.AddModelError(string.Empty, "Your account isn't linked to a Staff record, so this dispense action can't be attributed to you.");
            }

            if (prescription.PatientID != 0)
            {
                var isPatientInService = await _context.Queues
                    .Include(q => q.ServicePoint)
                    .AnyAsync(q => q.PatientID == prescription.PatientID && q.QueueStatus == "InService" && q.ServicePoint != null && q.ServicePoint.Code == "PHA");

                if (!isPatientInService)
                {
                    ModelState.AddModelError(string.Empty, "The patient must be called and 'In Service' in the Pharmacy queue before you can dispense.");
                }
            }

            if (!ModelState.IsValid)
            {
                vm.PatientDisplayName = prescription.Patient != null ? $"{prescription.Patient.FirstName} {prescription.Patient.LastName}" : "—";
                vm.RX = prescription.RX;
                return View(vm);
            }

            var medication = new Medication
            {
                PrescriptionID = prescription.PrescriptionID,
                MedicationName = vm.MedicationName,
                MedicationType = vm.MedicationType,
                PrescribedDose = vm.PrescribedDose,
                Amount = vm.Amount,
                DispensedQuantity = vm.DispensedQuantity,
                PharmacistID = currentUser!.StaffID!.Value,
                Remark = vm.Remark,
                DispensedDate = DateTime.Now
            };

            _context.Medications.Add(medication);
            prescription.Status = vm.DispenseStatus == "PartiallyDispensed" ? "PartiallyDispensed" : "Dispensed";

            await _context.SaveChangesAsync();

            // Notify Prescribing Nurse/Clinician
            var prescribedByUser = await _context.Users.FirstOrDefaultAsync(u => u.StaffID == prescription.PrescribedByID);
            if (prescribedByUser != null)
            {
                var patInfo = prescription.Patient != null ? $"{prescription.Patient.FirstName} {prescription.Patient.LastName} (Card No: {prescription.Patient.CardNumber})" : "Unknown Patient";
                string statusLabel = prescription.Status == "PartiallyDispensed" ? "Partially Dispensed" : "Dispensed";
                await _notificationService.CreateDirectNotificationAsync(
                    prescribedByUser.Id,
                    $"Medication {statusLabel}",
                    $"Medication {statusLabel.ToLower()} for {patInfo}: {vm.MedicationName} ({vm.DispensedQuantity?.ToString() ?? "—"} units).",
                    "Medication",
                    medication.MedicationID);
            }

            var patName = prescription.Patient != null ? $"{prescription.Patient.FirstName} {prescription.Patient.LastName}" : "Patient";
            TempData["SuccessMessage"] = $"Medication for {patName} dispensed and linked to the prescription.";
            return RedirectToAction(nameof(Details), new { id });
        }

        private bool PrescriptionExists(int id)
        {
            return _context.Prescriptions.Any(e => e.PrescriptionID == id);
        }
    }
}
