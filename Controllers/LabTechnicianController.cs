using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.Models;
using HUIOTClinicMS.Models.Enums;
using HUIOTClinicMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HUIOTClinicMS.Controllers
{
    [Authorize(Roles = "LabTechnician")]
    public class LabTechnicianController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public LabTechnicianController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(bool myTasks = false)
        {
            var today = DateTime.Today;
            var currentUser = await _userManager.GetUserAsync(User);
            var myStaffId = currentUser?.StaffID;

            var baseQuery = _context.LabRequests
                .Include(l => l.Patient)
                .Include(l => l.RequestedBy)
                .Include(l => l.AssignedToStaff)
                .Include(l => l.TestTypes)
                .OrderByDescending(l => l.RequestDate);

            var allRequests = await baseQuery.ToListAsync();

            var displayedRequests = (myTasks && myStaffId.HasValue)
                ? allRequests.Where(l => l.AssignedToStaffID == myStaffId.Value).ToList()
                : allRequests;

            var vm = new LabTechDashboardViewModel
            {
                MyTasksOnly = myTasks,
                AssignedToMePendingCount = myStaffId.HasValue 
                    ? allRequests.Count(l => l.Status == LabRequestStatuses.Pending && l.AssignedToStaffID == myStaffId.Value) 
                    : 0,
                PendingRequests = displayedRequests.Count(l => l.Status == LabRequestStatuses.Pending),
                CompletedToday = displayedRequests.Count(l => l.Status == LabRequestStatuses.Completed && l.RequestDate.Date == today),
                TotalResults = await _context.LabResults.CountAsync(),

                PendingLabRequests = displayedRequests.Where(l => l.Status == LabRequestStatuses.Pending).ToList(),
                InProgressLabRequests = displayedRequests.Where(l => l.Status == LabRequestStatuses.InProgress).ToList(),
                CompletedLabRequests = displayedRequests.Where(l => l.Status == LabRequestStatuses.Completed).Take(50).ToList()
            };

            return View(vm);
        }
    }
}