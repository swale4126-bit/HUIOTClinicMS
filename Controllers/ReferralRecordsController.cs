using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.Models;
using HUIOTClinicMS.Models.Enums;

namespace HUIOTClinicMS.Controllers
{
    [Authorize(Roles = "HealthOfficer,Nurse")]
    public class ReferralRecordsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReferralRecordsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: ReferralRecords/Create?encounterId=5
        public async Task<IActionResult> Create(int encounterId)
        {
            var encounter = await _context.ClinicalEncounters
                .Include(e => e.Patient)
                    .ThenInclude(p => p.Department)
                .Include(e => e.Patient)
                    .ThenInclude(p => p.DormBlock)
                .Include(e => e.TriageAssessment)
                .Include(e => e.AssignedToStaff)
                .FirstOrDefaultAsync(e => e.ID == encounterId);

            if (encounter == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            var clinicianName = currentUser?.FullName ?? User.Identity?.Name ?? "Attending Clinician";
            var clinicianRole = User.IsInRole("HealthOfficer") ? "Health Officer" : "Nurse";

            var vm = new ReferralRecord
            {
                EncounterID = encounter.ID,
                Encounter = encounter,
                DestinationFacility = "Hawassa Comprehensive Specialized Hospital (HUCSH)",
                ReasonForTransfer = encounter.Type == EncounterType.Emergency
                    ? "Patient emergency condition exceeds clinic capacity. Requires tertiary specialized diagnostic evaluation, specialist consultation, and intensive care at HUCSH."
                    : "Patient condition exceeds clinic resources. Requires tertiary specialist consultation and workup at HUCSH.",
                TransferredBy = $"{clinicianName} ({clinicianRole})"
            };

            return View(vm);
        }

        // POST: ReferralRecords/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReferralRecord referral)
        {
            var encounter = await _context.ClinicalEncounters
                .Include(e => e.Patient)
                .FirstOrDefaultAsync(e => e.ID == referral.EncounterID);

            if (encounter == null) return NotFound();

            if (ModelState.IsValid)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var clinicianName = currentUser?.FullName ?? User.Identity?.Name ?? "Attending Clinician";
                var clinicianRole = User.IsInRole("HealthOfficer") ? "Health Officer" : "Nurse";

                referral.CreatedAt = DateTime.UtcNow;
                referral.CreatedBy = clinicianName;
                referral.TransferredBy = $"{clinicianName} ({clinicianRole}) [Digitally Signed: {DateTime.Now:yyyy-MM-dd HH:mm}]";

                _context.Add(referral);
                
                // ES 7192:2025 standard mandates updating status on referral
                encounter.Status = WorkflowState.Transferred;
                encounter.EndTime = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Emergency hospital referral created and digitally signed successfully. You may now print the official referral letter.";
                return RedirectToAction(nameof(PrintLetter), new { id = referral.ID });
            }

            referral.Encounter = encounter;
            return View(referral);
        }

        // GET: ReferralRecords/PrintLetter/5
        public async Task<IActionResult> PrintLetter(int id)
        {
            var referral = await _context.ReferralRecords
                .Include(r => r.Encounter)
                    .ThenInclude(e => e!.Patient)
                        .ThenInclude(p => p!.Department)
                .Include(r => r.Encounter)
                    .ThenInclude(e => e!.Patient)
                        .ThenInclude(p => p!.DormBlock)
                .Include(r => r.Encounter)
                    .ThenInclude(e => e!.TriageAssessment)
                .Include(r => r.Encounter)
                    .ThenInclude(e => e!.AssignedToStaff)
                .Include(r => r.Encounter)
                    .ThenInclude(e => e!.ClinicalNotes)
                .Include(r => r.Encounter)
                    .ThenInclude(e => e!.LabRequests)
                        .ThenInclude(l => l.TestTypes)
                .FirstOrDefaultAsync(r => r.ID == id);

            if (referral == null) return NotFound();

            var patientId = referral.Encounter?.PatientID ?? 0;
            var medRecords = await _context.MedicalRecords
                .Where(m => m.PatientID == patientId || m.EncounterID == referral.EncounterID)
                .OrderByDescending(m => m.DateTime)
                .ToListAsync();

            var medRecordIds = medRecords.Select(m => m.ID).ToList();
            var labResults = await _context.LabResults
                .Include(lr => lr.TestType)
                .Where(lr => medRecordIds.Contains(lr.MedicalRecordID))
                .ToListAsync();

            ViewBag.MedicalRecords = medRecords;
            ViewBag.LabResults = labResults;

            return View("ReferralLetter", referral);
        }
    }
}
