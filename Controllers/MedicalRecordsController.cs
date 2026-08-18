using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HUIOTClinicMS.Models;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.ViewModels;

public class MedicalRecordsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public MedicalRecordsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: MEDICALRECORDS
    public async Task<IActionResult> Index(string? search)
    {
        var query = _context.MedicalRecords
            .Include(m => m.Patient)
            .Include(m => m.CreatedBy)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(m => m.Diagnosis != null && m.Diagnosis.ToLower().Contains(term));
        }

        var records = await query
            .OrderByDescending(m => m.DateTime)
            .ToListAsync();

        ViewBag.SearchTerm = search;
        return View(records);
    }

    // GET: MEDICALRECORDS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var medicalrecord = await _context.MedicalRecords
            .Include(m => m.Patient)
            .Include(m => m.CreatedBy)
            .Include(m => m.LabRequests)
            .Include(m => m.LabResults)
            .FirstOrDefaultAsync(m => m.ID == id);

        if (medicalrecord == null) return NotFound();

        return View(medicalrecord);
    }

    // GET: MEDICALRECORDS/Create
    [Authorize(Roles = "Nurse,HealthOfficer")]
    public async Task<IActionResult> Create(int? patientId)
    {
        var vm = new MedicalRecordCreateViewModel();

        if (patientId.HasValue)
        {
            var patient = await _context.Patients.FindAsync(patientId.Value);
            if (patient == null) return NotFound();

            vm.PatientID = patient.ID;
            vm.PatientDisplayName = $"{patient.FirstName} {patient.LastName} ({patient.CardNumber})";
        }
        else
        {
            await PopulatePatientDropdownAsync();
        }

        return View(vm);
    }

    // POST: MEDICALRECORDS/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Nurse,HealthOfficer")]
    public async Task<IActionResult> Create(MedicalRecordCreateViewModel vm)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.StaffID == null)
        {
            ModelState.AddModelError(string.Empty,
                "Your account isn't linked to a Staff record, so a medical record can't be attributed to you. Contact a Health Officer to link your account.");
        }

        var isPatientInService = await _context.Queues
            .Include(q => q.ServicePoint)
            .AnyAsync(q => q.PatientID == vm.PatientID && q.QueueStatus == "InService" && q.ServicePoint != null && q.ServicePoint.Code == "CON");

        if (!isPatientInService)
        {
            ModelState.AddModelError(string.Empty, "Sorry, our patient ur queue is not arrived so please wait.");
        }

        if (!ModelState.IsValid)
        {
            if (string.IsNullOrEmpty(vm.PatientDisplayName))
            {
                await PopulatePatientDropdownAsync();
            }
            return View(vm);
        }

        var record = new MedicalRecord
        {
            PatientID = vm.PatientID,
            ChiefComplaint = vm.ChiefComplaint,
            GeneralAppearance = vm.GeneralAppearance,
            Diagnosis = vm.Diagnosis,
            Treatment = vm.Treatment,
            Notes = vm.Notes,
            DateTime = DateTime.Now,
            CreatedByID = currentUser!.StaffID!.Value
        };

        _context.MedicalRecords.Add(record);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Medical record saved successfully.";
        return RedirectToAction(nameof(Details), new { id = record.ID });
    }

    // GET: MEDICALRECORDS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var record = await _context.MedicalRecords
            .Include(m => m.Patient)
            .Include(m => m.CreatedBy)
            .FirstOrDefaultAsync(m => m.ID == id);

        if (record == null) return NotFound();

        var vm = new MedicalRecordEditViewModel
        {
            ID = record.ID,
            PatientDisplayName = record.Patient != null ? $"{record.Patient.FirstName} {record.Patient.LastName} ({record.Patient.CardNumber})" : "—",
            CreatedByDisplayName = record.CreatedBy != null ? $"{record.CreatedBy.FirstName} {record.CreatedBy.LastName}" : "—",
            DateTime = record.DateTime,
            ChiefComplaint = record.ChiefComplaint ?? string.Empty,
            GeneralAppearance = record.GeneralAppearance,
            Diagnosis = record.Diagnosis,
            Treatment = record.Treatment,
            Notes = record.Notes
        };

        return View(vm);
    }

    // POST: MEDICALRECORDS/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, MedicalRecordEditViewModel vm)
    {
        if (id != vm.ID) return NotFound();

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var record = await _context.MedicalRecords.FindAsync(id);
        if (record == null) return NotFound();

        record.ChiefComplaint = vm.ChiefComplaint;
        record.GeneralAppearance = vm.GeneralAppearance;
        record.Diagnosis = vm.Diagnosis;
        record.Treatment = vm.Treatment;
        record.Notes = vm.Notes;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!MedicalRecordExists(id)) return NotFound();
            throw;
        }

        TempData["SuccessMessage"] = "Medical record updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // GET: MEDICALRECORDS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var medicalrecord = await _context.MedicalRecords
            .Include(m => m.Patient)
            .FirstOrDefaultAsync(m => m.ID == id);

        if (medicalrecord == null) return NotFound();
        return View(medicalrecord);
    }

    // POST: MEDICALRECORDS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var medicalrecord = await _context.MedicalRecords.FindAsync(id);
        if (medicalrecord != null)
        {
            _context.MedicalRecords.Remove(medicalrecord);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool MedicalRecordExists(int? id)
    {
        return _context.MedicalRecords.Any(e => e.ID == id);
    }

    private async Task PopulatePatientDropdownAsync()
    {
        var patients = await _context.Queues
            .Include(q => q.Patient)
            .Include(q => q.ServicePoint)
            .Where(q => q.QueueStatus == "InService" && q.ServicePoint != null && q.ServicePoint.Code == "CON")
            .Select(q => q.Patient)
            .Distinct()
            .OrderBy(p => p.FirstName)
            .Select(p => new { p.ID, Display = p.FirstName + " " + p.LastName + " (" + p.CardNumber + ")" })
            .ToListAsync();

        ViewBag.Patients = new SelectList(patients, "ID", "Display");
    }
}
