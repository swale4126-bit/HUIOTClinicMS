using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HUIOTClinicMS.Models;
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
                .Include(p => p.LabResult)
                    .ThenInclude(l => l!.TestType)
                .Include(p => p.Medication)
                    .ThenInclude(m => m!.Pharmacist)
                .FirstOrDefaultAsync(m => m.PrescriptionID == id);

            if (prescription == null) return NotFound();

            return View(prescription);
        }

        // GET: PRESCRIPTIONS/Create?labResultId=5
        [Authorize(Roles = "Nurse")]
        public async Task<IActionResult> Create(int? labResultId)
        {
            if (labResultId == null) return NotFound();

            var labResult = await _context.LabResults
                .Include(l => l.MedicalRecord)
                    .ThenInclude(m => m!.Patient)
                .Include(l => l.TestType)
                .FirstOrDefaultAsync(l => l.ID == labResultId);

            if (labResult == null || labResult.MedicalRecord == null) return NotFound();

            if (labResult.Status != "Completed")
            {
                TempData["ErrorMessage"] = "A prescription can only be created once the lab result is completed.";
                return RedirectToAction("Index", "Nurse");
            }

            var patient = labResult.MedicalRecord.Patient;

            var vm = new PrescriptionCreateViewModel
            {
                LabResultID = labResult.ID,
                PatientID = labResult.MedicalRecord.PatientID,
                PatientDisplayName = patient != null ? $"{patient.FirstName} {patient.LastName}" : "—",
                TestTypeName = labResult.TestType?.TestName ?? "—",
                LabResultSummary = labResult.Result ?? "—"
            };

            return View(vm);
        }

        // POST: PRESCRIPTIONS/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Nurse")]
        public async Task<IActionResult> Create(PrescriptionCreateViewModel vm)
        {
            var labResult = await _context.LabResults
                .Include(l => l.MedicalRecord)
                    .ThenInclude(m => m!.Patient)
                .Include(l => l.TestType)
                .FirstOrDefaultAsync(l => l.ID == vm.LabResultID);

            if (labResult == null || labResult.MedicalRecord == null)
            {
                ModelState.AddModelError(string.Empty, "The related lab result could not be found.");
            }
            else if (labResult.Status != "Completed")
            {
                ModelState.AddModelError(string.Empty, "A prescription can only be created once the lab result is completed.");
            }

            if (labResult != null && await _context.Prescriptions.AnyAsync(p => p.LabResultID == labResult.ID))
            {
                ModelState.AddModelError(string.Empty, "A prescription has already been created for this lab result.");
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.StaffID == null)
            {
                ModelState.AddModelError(string.Empty, "Your account isn't linked to a Staff record, so this prescription can't be attributed to you.");
            }

            if (labResult?.MedicalRecord != null)
            {
                var isPatientInService = await _context.Queues
                    .Include(q => q.ServicePoint)
                    .AnyAsync(q => q.PatientID == labResult.MedicalRecord.PatientID && q.QueueStatus == "InService" && q.ServicePoint != null && q.ServicePoint.Code == "CON");

                if (!isPatientInService)
                {
                    ModelState.AddModelError(string.Empty, "Sorry, our patient ur queue is not arrived so please wait.");
                }
            }

            if (!ModelState.IsValid)
            {
                if (labResult?.MedicalRecord?.Patient != null)
                {
                    vm.PatientDisplayName = $"{labResult.MedicalRecord.Patient.FirstName} {labResult.MedicalRecord.Patient.LastName}";
                    vm.TestTypeName = labResult.TestType?.TestName ?? "—";
                    vm.LabResultSummary = labResult.Result ?? "—";
                }
                return View(vm);
            }

            var prescription = new Prescription
            {
                PatientID = labResult!.MedicalRecord!.PatientID,
                LabResultID = labResult.ID,
                RX = vm.RX,
                PrescribedByID = currentUser!.StaffID!.Value,
                TimeDate = DateTime.Now,
                Status = "Pending"
            };

            _context.Prescriptions.Add(prescription);
            await _context.SaveChangesAsync();

            // Automatically create a Pharmacy Queue ticket
            var pharmacyServicePoint = await _context.ServicePoints.FirstOrDefaultAsync(s => s.Name.Contains("Pharmacy"));
            if (pharmacyServicePoint != null && labResult?.MedicalRecord != null)
            {
                var todaysPharmacyQueuesCount = await _context.Queues
                    .Where(q => q.ServicePointID == pharmacyServicePoint.ID && q.CreatedAt.Date == DateTime.Today)
                    .CountAsync();

                var newQueue = new Queue
                {
                    TokenNumber = $"PHA-{todaysPharmacyQueuesCount + 1:D3}",
                    PatientID = labResult.MedicalRecord.PatientID,
                    ServicePointID = pharmacyServicePoint.ID,
                    PriorityLevel = "Normal",
                    QueueStatus = "Waiting",
                    CreatedAt = DateTime.Now
                };

                _context.Queues.Add(newQueue);
                await _context.SaveChangesAsync();
            }

            // Notify Pharmacists
            var pharmacists = await _userManager.GetUsersInRoleAsync("Pharmacist");
            var patientInfo = labResult?.MedicalRecord?.Patient != null ? $"{labResult.MedicalRecord.Patient.FirstName} {labResult.MedicalRecord.Patient.LastName} (Card No: {labResult.MedicalRecord.Patient.CardNumber})" : "Unknown Patient";
            foreach (var pharmacist in pharmacists)
            {
                await _notificationService.CreateNotificationAsync(
                    pharmacist.Id,
                    "New Prescription",
                    $"New prescription submitted for {patientInfo}.",
                    "Prescription",
                    prescription.PrescriptionID);
            }

            TempData["SuccessMessage"] = "Prescription created successfully.";
            return RedirectToAction(nameof(Details), new { id = prescription.PrescriptionID });
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
                _context.Prescriptions.Remove(prescription);
            }

            await _context.SaveChangesAsync();
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
                    ModelState.AddModelError(string.Empty, "Sorry, our patient ur queue is not arrived so please wait.");
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
            prescription.Status = "Dispensed";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Medication dispensed and linked to the prescription.";
            return RedirectToAction(nameof(Details), new { id });
        }

        private bool PrescriptionExists(int id)
        {
            return _context.Prescriptions.Any(e => e.PrescriptionID == id);
        }
    }
}
