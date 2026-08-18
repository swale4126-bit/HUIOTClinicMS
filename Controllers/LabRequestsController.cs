using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HUIOTClinicMS.Models;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.ViewModels;

public class LabRequestsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly HUIOTClinicMS.Services.INotificationService _notificationService;

    public LabRequestsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, HUIOTClinicMS.Services.INotificationService notificationService)
    {
        _context = context;
        _userManager = userManager;
        _notificationService = notificationService;
    }

    // GET: LABREQUESTS
    public async Task<IActionResult> Index(string? search)
    {
        var query = _context.LabRequests
            .Include(l => l.Patient)
            .Include(l => l.RequestedBy)
            .Include(l => l.TestTypes)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(l => l.Patient != null &&
                (l.Patient.FirstName.ToLower().Contains(term) || l.Patient.LastName.ToLower().Contains(term)));
        }

        var requests = await query
            .OrderByDescending(l => l.RequestDate)
            .ToListAsync();

        ViewBag.SearchTerm = search;
        return View(requests);
    }

    // GET: LABREQUESTS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var labrequest = await _context.LabRequests
            .Include(l => l.Patient)
            .Include(l => l.RequestedBy)
            .Include(l => l.MedicalRecord)
            .Include(l => l.TestTypes)
            .FirstOrDefaultAsync(m => m.LabRequestID == id);

        if (labrequest == null) return NotFound();

        return View(labrequest);
    }

    // GET: LABREQUESTS/Create?medicalRecordId=5
    [Authorize(Roles = "Nurse,HealthOfficer")]
    public async Task<IActionResult> Create(int? medicalRecordId)
    {
        if (medicalRecordId == null) return NotFound();

        var record = await _context.MedicalRecords
            .Include(m => m.Patient)
            .FirstOrDefaultAsync(m => m.ID == medicalRecordId);

        if (record == null) return NotFound();

        var vm = new LabRequestCreateViewModel
        {
            MedicalRecordID = record.ID,
            PatientID = record.PatientID,
            PatientDisplayName = record.Patient != null ? $"{record.Patient.FirstName} {record.Patient.LastName}" : "—",
            AvailableTestTypes = await _context.TestTypes.OrderBy(t => t.TestName).ToListAsync()
        };

        return View(vm);
    }

    // POST: LABREQUESTS/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Nurse,HealthOfficer")]
    public async Task<IActionResult> Create(LabRequestCreateViewModel vm)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.StaffID == null)
        {
            ModelState.AddModelError(string.Empty,
                "Your account isn't linked to a Staff record, so this request can't be attributed to you.");
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
            vm.AvailableTestTypes = await _context.TestTypes.OrderBy(t => t.TestName).ToListAsync();
            return View(vm);
        }

        var selectedTests = await _context.TestTypes
            .Where(t => vm.SelectedTestTypeIds.Contains(t.ID))
            .ToListAsync();

        var labRequest = new LabRequest
        {
            PatientID = vm.PatientID,
            MedicalRecordID = vm.MedicalRecordID,
            RequestedByID = currentUser!.StaffID!.Value,
            RequestDate = DateTime.Now,
            ClinicalNotes = vm.ClinicalNotes,
            Status = "Pending",
            TestTypes = selectedTests
        };

        _context.LabRequests.Add(labRequest);
        await _context.SaveChangesAsync();

        // Automatically create a Lab Queue ticket
        var labServicePoint = await _context.ServicePoints.FirstOrDefaultAsync(s => s.Name.Contains("Laboratory") || s.Name.Contains("Lab"));
        if (labServicePoint != null)
        {
            var todaysLabQueuesCount = await _context.Queues
                .Where(q => q.ServicePointID == labServicePoint.ID && q.CreatedAt.Date == DateTime.Today)
                .CountAsync();

            var newQueue = new Queue
            {
                TokenNumber = $"LAB-{todaysLabQueuesCount + 1:D3}",
                PatientID = vm.PatientID,
                ServicePointID = labServicePoint.ID,
                PriorityLevel = "Normal",
                QueueStatus = "Waiting",
                CreatedAt = DateTime.Now
            };

            _context.Queues.Add(newQueue);
            await _context.SaveChangesAsync();
        }

        // Fetch Patient details for notification
        var patient = await _context.Patients.FindAsync(vm.PatientID);
        string patientInfo = patient != null ? $"{patient.FirstName} {patient.LastName} (Card No: {patient.CardNumber})" : "Unknown Patient";

        // Notify Lab Technicians
        var labTechs = await _userManager.GetUsersInRoleAsync("LabTechnician");
        foreach (var tech in labTechs)
        {
            await _notificationService.CreateNotificationAsync(
                tech.Id,
                "New Lab Request",
                $"New laboratory request created for {patientInfo}.",
                "LabRequest",
                labRequest.LabRequestID);
        }

        TempData["SuccessMessage"] = "Lab request submitted successfully.";
        return RedirectToAction(nameof(Details), new { id = labRequest.LabRequestID });
    }

    // GET: LABREQUESTS/Edit/5
    [Authorize(Roles = "LabTechnician,HealthOfficer")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var labrequest = await _context.LabRequests
            .Include(l => l.Patient)
            .Include(l => l.RequestedBy)
            .Include(l => l.TestTypes)
            .FirstOrDefaultAsync(l => l.LabRequestID == id);

        if (labrequest == null) return NotFound();

        var vm = new LabRequestEditViewModel
        {
            LabRequestID = labrequest.LabRequestID,
            PatientDisplayName = labrequest.Patient != null ? $"{labrequest.Patient.FirstName} {labrequest.Patient.LastName}" : "—",
            RequestedByDisplayName = labrequest.RequestedBy != null ? $"{labrequest.RequestedBy.FirstName} {labrequest.RequestedBy.LastName}" : "—",
            RequestDate = labrequest.RequestDate,
            TestTypeNames = labrequest.TestTypes.Any() ? string.Join(", ", labrequest.TestTypes.Select(t => t.TestName)) : "—",
            ClinicalNotes = labrequest.ClinicalNotes,
            Status = labrequest.Status
        };

        return View(vm);
    }

    // POST: LABREQUESTS/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "LabTechnician,HealthOfficer")]
    public async Task<IActionResult> Edit(int id, LabRequestEditViewModel vm)
    {
        if (id != vm.LabRequestID) return NotFound();

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var labrequest = await _context.LabRequests.FindAsync(id);
        if (labrequest == null) return NotFound();

        labrequest.Status = vm.Status;
        labrequest.ClinicalNotes = vm.ClinicalNotes;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!LabRequestExists(id)) return NotFound();
            throw;
        }

        TempData["SuccessMessage"] = "Lab request updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // GET: LABREQUESTS/Delete/5
    [Authorize(Roles = "HealthOfficer")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var labrequest = await _context.LabRequests
            .Include(l => l.Patient)
            .Include(l => l.TestTypes)
            .FirstOrDefaultAsync(m => m.LabRequestID == id);

        if (labrequest == null) return NotFound();

        return View(labrequest);
    }

    // POST: LABREQUESTS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "HealthOfficer")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var labrequest = await _context.LabRequests.FindAsync(id);
        if (labrequest != null)
        {
            _context.LabRequests.Remove(labrequest);
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Lab request deleted.";
        return RedirectToAction(nameof(Index));
    }

    private bool LabRequestExists(int id)
    {
        return _context.LabRequests.Any(e => e.LabRequestID == id);
    }
}
