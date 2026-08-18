using Microsoft.AspNetCore.Authorization;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HUIOTClinicMS.Controllers
{
    [Authorize(Roles = "LabTechnician")]
    public class LabTechnicianController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LabTechnicianController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;

            var allRequests = await _context.LabRequests
                .Include(l => l.Patient)
                .Include(l => l.RequestedBy)
                .Include(l => l.TestTypes)
                .OrderByDescending(l => l.RequestDate)
                .ToListAsync();

            var vm = new LabTechDashboardViewModel
            {
                PendingRequests = allRequests.Count(l => l.Status == "Pending"),

                // Approximation: no date field exists on LabResult yet, so we use
                // LabRequest.RequestDate + Status as a stand-in for "completed today".
                CompletedToday = allRequests.Count(l => l.Status == "Completed" && l.RequestDate.Date == today),

                TotalResults = await _context.LabResults.CountAsync(),

                PendingLabRequests = allRequests.Where(l => l.Status == "Pending").ToList(),
                InProgressLabRequests = allRequests.Where(l => l.Status == "In Progress").ToList(),
                CompletedLabRequests = allRequests.Where(l => l.Status == "Completed").Take(50).ToList() // Limit to 50 for performance
            };

            return View(vm);
        }
    }
}