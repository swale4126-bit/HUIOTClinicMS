
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HUIOTClinicMS.Models;
using HUIOTClinicMS.Data;

namespace HUIOTClinicMS.Controllers
{
    public class LabResultsController : Controller
    {
    private readonly ApplicationDbContext _context;
    private readonly HUIOTClinicMS.Services.INotificationService _notificationService;

    public LabResultsController(ApplicationDbContext context, HUIOTClinicMS.Services.INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    // GET: LABRESULTS
    public async Task<IActionResult> Index()    
    {
        var results = await _context.LabResults
            .Include(l => l.MedicalRecord)
                .ThenInclude(m => m.Patient)
            .Include(l => l.TestType)
            .Include(l => l.ResultPerformedBy)
            .ToListAsync();
        return View(results);
    }

    // GET: LABRESULTS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var labresult = await _context.LabResults
            .Include(l => l.MedicalRecord)
                .ThenInclude(m => m.Patient)
            .Include(l => l.TestType)
            .Include(l => l.ResultPerformedBy)
            .FirstOrDefaultAsync(m => m.ID == id);
        if (labresult == null)
        {
            return NotFound();
        }

        return View(labresult);
    }

    // GET: LABRESULTS/Create
    [Authorize(Roles = "LabTechnician")]
    public async Task<IActionResult> Create(int? labRequestId)
    {
        var pendingRequests = await _context.LabRequests
            .Include(l => l.Patient)
            .Include(l => l.TestTypes)
            .Where(l => l.Status == "Pending" || l.Status == "In Progress")
            .OrderByDescending(l => l.RequestDate)
            .ToListAsync();

        ViewBag.LabRequests = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
            pendingRequests.Select(l => new {
                ID = l.LabRequestID,
                Name = $"{l.Patient?.FirstName} {l.Patient?.LastName} - {l.RequestDate:MM/dd} ({l.TestTypes.Count} tests)"
            }), 
            "ID", "Name", labRequestId);

        var vm = new HUIOTClinicMS.ViewModels.LabResultCreateViewModel();
        
        if (labRequestId.HasValue)
        {
            vm.LabRequestID = labRequestId.Value;
            var req = pendingRequests.FirstOrDefault(r => r.LabRequestID == labRequestId.Value);
            if (req != null)
            {
                vm.AvailableTestTypes = req.TestTypes.ToList();
                vm.SelectedTestTypeIds = req.TestTypes.Select(t => t.ID).ToList();
            }
        }
        else
        {
            vm.AvailableTestTypes = await _context.TestTypes.ToListAsync();
        }

        return View(vm);
    }

    // POST: LABRESULTS/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "LabTechnician")]
    public async Task<IActionResult> Create(HUIOTClinicMS.ViewModels.LabResultCreateViewModel vm)
    {
        if (ModelState.IsValid)
        {
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity!.Name);
            var labRequest = await _context.LabRequests
                .Include(l => l.Patient)
                .FirstOrDefaultAsync(l => l.LabRequestID == vm.LabRequestID);
            
            if (labRequest != null && currentUser?.StaffID != null)
            {
                var isPatientInService = await _context.Queues
                    .Include(q => q.ServicePoint)
                    .AnyAsync(q => q.PatientID == labRequest.PatientID && q.QueueStatus == "InService" && q.ServicePoint != null && (q.ServicePoint.Code == "LAB" || q.ServicePoint.Name.Contains("Laboratory")));

                if (!isPatientInService)
                {
                    ModelState.AddModelError(string.Empty, "Sorry, our patient ur queue is not arrived so please wait.");
                    
                    var pendingRequestsReload = await _context.LabRequests
                        .Include(l => l.Patient)
                        .Where(l => l.Status == "Pending" || l.Status == "In Progress")
                        .ToListAsync();
                    ViewBag.LabRequests = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                        pendingRequestsReload.Select(l => new { ID = l.LabRequestID, Name = $"{l.Patient?.FirstName} {l.Patient?.LastName}" }), 
                        "ID", "Name", vm.LabRequestID);
                    vm.AvailableTestTypes = await _context.TestTypes.ToListAsync();

                    return View(vm);
                }

                LabResult firstLabResult = null;
                foreach (var testTypeId in vm.SelectedTestTypeIds)
                {
                    var resultText = vm.Results != null && vm.Results.ContainsKey(testTypeId) ? vm.Results[testTypeId] : string.Empty;
                    var remarkText = vm.Remarks != null && vm.Remarks.ContainsKey(testTypeId) ? vm.Remarks[testTypeId] : string.Empty;

                    var labResult = new LabResult
                    {
                        MedicalRecordID = labRequest.MedicalRecordID,
                        TypeID = testTypeId,
                        Result = resultText,
                        Remark = remarkText,
                        ResultPerformedByID = currentUser.StaffID.Value,
                        Status = "Completed"
                    };

                    _context.Add(labResult);
                    if (firstLabResult == null) firstLabResult = labResult;
                }
                
                // Update LabRequest status
                labRequest.Status = "Completed"; // Or In Progress if not all tests are done, but we'll mark Completed for simplicity
                _context.Update(labRequest);

                await _context.SaveChangesAsync();

                // Automatically enqueue back to Nurse/HealthOfficer for Result Review
                var consultationSp = await _context.ServicePoints.FirstOrDefaultAsync(s => s.Code == "CON" || s.Name.Contains("Consultation"));
                if (consultationSp != null)
                {
                    var todaysConQueuesCount = await _context.Queues
                        .Where(q => q.ServicePointID == consultationSp.ID && q.CreatedAt.Date == DateTime.Today)
                        .CountAsync();

                    var resultReviewQueue = new Queue
                    {
                        TokenNumber = $"CON-{todaysConQueuesCount + 1:D3}",
                        PatientID = labRequest.PatientID,
                        ServicePointID = consultationSp.ID,
                        PriorityLevel = "Normal",
                        QueueStatus = "Waiting",
                        QueueType = "Result Review",
                        CreatedAt = DateTime.Now
                    };

                    _context.Queues.Add(resultReviewQueue);
                    await _context.SaveChangesAsync();
                }

                // Notify requesting Nurse/Clinician
                var requestedByUser = await _context.Users.FirstOrDefaultAsync(u => u.StaffID == labRequest.RequestedByID);
                if (requestedByUser != null && firstLabResult != null)
                {
                    var patientInfo = labRequest.Patient != null ? $"{labRequest.Patient.FirstName} {labRequest.Patient.LastName} (Card No: {labRequest.Patient.CardNumber})" : "Unknown Patient";
                    await _notificationService.CreateNotificationAsync(
                        requestedByUser.Id,
                        "Lab Result Available",
                        $"Laboratory result is available for {patientInfo}.",
                        "LabResult",
                        firstLabResult.ID);
                }

                return RedirectToAction("Index", "LabTechnician");
            }
        }

        var pendingRequests = await _context.LabRequests
            .Include(l => l.Patient)
            .Where(l => l.Status == "Pending" || l.Status == "In Progress")
            .ToListAsync();
        ViewBag.LabRequests = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
            pendingRequests.Select(l => new { ID = l.LabRequestID, Name = $"{l.Patient?.FirstName} {l.Patient?.LastName}" }), 
            "ID", "Name", vm.LabRequestID);
        vm.AvailableTestTypes = await _context.TestTypes.ToListAsync();

        return View(vm);
    }

    // GET: LABRESULTS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var labresult = await _context.LabResults.FindAsync(id);
        if (labresult == null)
        {
            return NotFound();
        }
        return View(labresult);
    }

    // POST: LABRESULTS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("ID,MedicalRecordID,MedicalRecord,TypeID,TestType,Result,ResultPerformedByID,ResultPerformedBy,Remark,Status")] LabResult labresult)
    {
        if (id != labresult.ID)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(labresult);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!LabResultExists(labresult.ID))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(labresult);
    }

    // GET: LABRESULTS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var labresult = await _context.LabResults
            .FirstOrDefaultAsync(m => m.ID == id);
        if (labresult == null)
        {
            return NotFound();
        }

        return View(labresult);
    }

    // POST: LABRESULTS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var labresult = await _context.LabResults.FindAsync(id);
        if (labresult != null)
        {
            _context.LabResults.Remove(labresult);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool LabResultExists(int? id)
    {
        return _context.LabResults.Any(e => e.ID == id);
    }
}
}
