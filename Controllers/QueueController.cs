using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.Models;
using HUIOTClinicMS.ViewModels;

namespace HUIOTClinicMS.Controllers
{
    [Authorize(Roles = "HealthOfficer,Nurse,LabTechnician,Pharmacist")]
    public class QueueController : Controller
    {
        private readonly ApplicationDbContext _context;

        public QueueController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? spId, string? queueType)
        {
            var servicePoints = await _context.ServicePoints.Where(s => s.IsActive).ToListAsync();
            
            if (!servicePoints.Any())
            {
                return View(new QueueDashboardViewModel()); // No service points configured yet
            }

            if (User.IsInRole("Nurse"))
            {
                servicePoints = servicePoints.Where(s => s.Code == "REG" || s.Code == "CON").ToList();
            }
            else if (User.IsInRole("LabTechnician"))
            {
                servicePoints = servicePoints.Where(s => s.Code == "LAB").ToList();
            }
            else if (User.IsInRole("Pharmacist"))
            {
                servicePoints = servicePoints.Where(s => s.Code == "PHA").ToList();
            }

            int? actualSpId = spId;
            
            // For Health Officer, if spId is 0, we show all departments. If it's null, we also default to 0 for Health Officers.
            if (User.IsInRole("HealthOfficer"))
            {
                if (spId == null) actualSpId = 0;
            }
            else
            {
                if (spId == null) actualSpId = servicePoints.First().ID;
            }

            if (actualSpId == null && !string.IsNullOrEmpty(queueType))
            {
                var matchingSp = servicePoints.FirstOrDefault(s => s.Name.Contains(queueType, StringComparison.OrdinalIgnoreCase));
                if (matchingSp != null)
                {
                    actualSpId = matchingSp.ID;
                }
            }

            var today = DateTime.Today;
            
            var query = _context.Queues
                .Include(q => q.Patient)
                .Where(q => q.CreatedAt >= today);

            if (actualSpId > 0)
            {
                query = query.Where(q => q.ServicePointID == actualSpId);
            }

            var queues = await query.OrderBy(q => q.CreatedAt).ToListAsync();

            var currentServing = actualSpId > 0 ? queues.FirstOrDefault(q => q.QueueStatus == "Called" || q.QueueStatus == "InService") : null;
            var waiting = actualSpId > 0 ? queues.Where(q => q.QueueStatus == "Waiting").ToList() : queues.Where(q => q.QueueStatus == "Waiting" || q.QueueStatus == "Called" || q.QueueStatus == "InService").ToList();

            var vm = new QueueDashboardViewModel
            {
                ServicePoints = servicePoints,
                SelectedServicePointID = actualSpId ?? 0,
                SelectedServicePoint = actualSpId > 0 ? servicePoints.FirstOrDefault(s => s.ID == actualSpId) : null,
                CurrentServingQueue = currentServing,
                WaitingQueues = waiting,
                WaitingCount = queues.Count(q => q.QueueStatus == "Waiting"),
                CalledCount = queues.Count(q => q.QueueStatus == "Called"),
                InServiceCount = queues.Count(q => q.QueueStatus == "InService"),
                CompletedCount = queues.Count(q => q.QueueStatus == "Completed")
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> CallNext(int servicePointId)
        {
            var today = DateTime.Today;

            // Check if already serving someone
            var currentServing = await _context.Queues
                .Where(q => q.ServicePointID == servicePointId && q.CreatedAt >= today && (q.QueueStatus == "Called" || q.QueueStatus == "InService"))
                .FirstOrDefaultAsync();

            if (currentServing != null)
            {
                TempData["ErrorMessage"] = "You must complete or cancel the current patient before calling the next one.";
                return RedirectToAction(nameof(Index), new { spId = servicePointId });
            }

            // Find next waiting
            var nextQueue = await _context.Queues
                .Where(q => q.ServicePointID == servicePointId && q.CreatedAt >= today && q.QueueStatus == "Waiting")
                .OrderBy(q => q.CreatedAt)
                .FirstOrDefaultAsync();

            if (nextQueue != null)
            {
                // Atomic update to prevent race conditions
                int rowsAffected = await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE Queues SET QueueStatus = 'Called' WHERE ID = {0} AND QueueStatus = 'Waiting'", nextQueue.ID);

                if (rowsAffected > 0)
                {
                    var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity!.Name);
                    var log = new ServiceLog
                    {
                        QueueID = nextQueue.ID,
                        CalledAt = DateTime.Now,
                        ServedByID = currentUser?.Id
                    };

                    _context.ServiceLogs.Add(log);
                    await _context.SaveChangesAsync();
                    
                    TempData["SuccessMessage"] = $"Called Token {nextQueue.TokenNumber}";
                }
                else
                {
                    TempData["ErrorMessage"] = "The patient was already called by another staff member.";
                }
            }

            return RedirectToAction(nameof(Index), new { spId = servicePointId });
        }

        [HttpPost]
        public async Task<IActionResult> StartService(int queueId, int servicePointId)
        {
            var queue = await _context.Queues.FindAsync(queueId);
            if (queue != null && queue.QueueStatus == "Called")
            {
                queue.QueueStatus = "InService";
                
                var log = await _context.ServiceLogs.FirstOrDefaultAsync(l => l.QueueID == queueId);
                if (log != null)
                {
                    log.ServiceStartedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index), new { spId = servicePointId });
        }

        [HttpPost]
        public async Task<IActionResult> CompleteService(int queueId, int servicePointId)
        {
            var queue = await _context.Queues.FindAsync(queueId);
            if (queue != null && (queue.QueueStatus == "Called" || queue.QueueStatus == "InService"))
            {
                queue.QueueStatus = "Completed";

                var log = await _context.ServiceLogs.FirstOrDefaultAsync(l => l.QueueID == queueId);
                if (log != null)
                {
                    log.ServiceEndedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index), new { spId = servicePointId });
        }
        
        [HttpPost]
        public async Task<IActionResult> CancelService(int queueId, int servicePointId)
        {
            var queue = await _context.Queues.FindAsync(queueId);
            if (queue != null && (queue.QueueStatus == "Called" || queue.QueueStatus == "InService"))
            {
                queue.QueueStatus = "Cancelled";

                var log = await _context.ServiceLogs.FirstOrDefaultAsync(l => l.QueueID == queueId);
                if (log != null)
                {
                    log.ServiceEndedAt = DateTime.Now; // Log when it was cancelled
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index), new { spId = servicePointId });
        }
    }
}
