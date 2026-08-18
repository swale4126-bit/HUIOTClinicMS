using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HUIOTClinicMS.Models;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.ViewModels;

public class PatientsController : Controller
{
    private readonly ApplicationDbContext _context;

    public PatientsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: PATIENTS
    public async Task<IActionResult> Index(string? search)
    {
        var query = _context.Patients
            .Include(p => p.Department)
            .Include(p => p.DormBlock)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(p => p.IDNumber.ToLower().Contains(term));
        }

        var patients = await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        ViewBag.SearchTerm = search;
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

        var vm = new PatientDetailsViewModel
        {
            Patient = patient,
            EmergencyContact = emergencyContact
        };

        return View(vm);
    }

    // GET: PATIENTS/Create
    [Authorize(Roles = "Nurse,HealthOfficer")]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdownsAsync();
        return View(new PatientCreateViewModel());
    }

    // POST: PATIENTS/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Nurse,HealthOfficer")]
    public async Task<IActionResult> Create(PatientCreateViewModel vm)
    {
        var duplicate = await _context.Patients
            .AnyAsync(p => p.IDNumber == vm.IDNumber);

        if (duplicate)
        {
            ModelState.AddModelError(nameof(vm.IDNumber), "This University ID Number is already registered.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return View(vm);
        }

        var cardNumber = await GenerateCardNumberAsync();

        var patient = new Patient
        {
            IDNumber = vm.IDNumber,
            CardNumber = cardNumber,
            FirstName = vm.FirstName,
            LastName = vm.LastName,
            Gender = vm.Gender,
            DateOfBirth = vm.DateOfBirth,
            PhoneNumber = vm.PhoneNumber,
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
            return View(vm);
        }

        var emergencyContact = new EmergencyContact
        {
            PatientID = patient.ID,
            ContactName = vm.ContactName,
            ContactPhoneNumber = vm.ContactPhoneNumber,
            Relationship = vm.Relationship
        };

        _context.EmergencyContacts.Add(emergencyContact);
        await _context.SaveChangesAsync();

        // Create Queue entry for Consultation
        var consultationSp = await _context.ServicePoints.FirstOrDefaultAsync(s => s.Code == "CON");
        if (consultationSp != null)
        {
            var year = DateTime.Now.Year;
            var month = DateTime.Now.Month;
            var day = DateTime.Now.Day;
            var today = DateTime.Today;

            var queueCountToday = await _context.Queues
                .CountAsync(q => q.ServicePointID == consultationSp.ID && q.CreatedAt >= today);
            
            var tokenSequence = (queueCountToday + 1).ToString("D3");
            var tokenNumber = $"{consultationSp.Code}-{tokenSequence}";

            var queue = new Queue
            {
                PatientID = patient.ID,
                ServicePointID = consultationSp.ID,
                TokenNumber = tokenNumber,
                QueueStatus = "Waiting"
            };
            
            _context.Queues.Add(queue);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Patient registered successfully. Card Number: {cardNumber}. Queue Token: {tokenNumber}";
        }
        else
        {
            TempData["SuccessMessage"] = $"Patient registered successfully. Card Number: {cardNumber}";
        }

        return RedirectToAction(nameof(Details), new { id = patient.ID });
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
        return RedirectToAction(nameof(Details), new { id });
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
