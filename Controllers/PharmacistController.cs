using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.Models;
using HUIOTClinicMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HUIOTClinicMS.Controllers
{
    [Authorize(Roles = "Pharmacist")]
    public class PharmacistController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PharmacistController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(bool myTasks = false)
        {
            var today = DateTime.Today;
            var currentUser = await _userManager.GetUserAsync(User);
            var myStaffId = currentUser?.StaffID;

            var baseQuery = _context.Prescriptions
                .Include(p => p.Patient)
                .Include(p => p.PrescribedBy)
                .Include(p => p.AssignedToStaff)
                .OrderByDescending(p => p.TimeDate);

            var allPrescriptions = await baseQuery.ToListAsync();

            var displayedPrescriptions = (myTasks && myStaffId.HasValue)
                ? allPrescriptions.Where(p => p.AssignedToStaffID == myStaffId.Value).ToList()
                : allPrescriptions;

            var vm = new PharmacistDashboardViewModel
            {
                MyTasksOnly = myTasks,
                AssignedToMePendingCount = myStaffId.HasValue
                    ? allPrescriptions.Count(p => p.Status == "Pending" && p.AssignedToStaffID == myStaffId.Value)
                    : 0,
                PrescriptionsToday = displayedPrescriptions.Count(p => p.TimeDate.Date == today),
                TotalMedicationsInRegistry = await _context.Medications.CountAsync(),
                TotalPrescriptions = displayedPrescriptions.Count,
                RecentPrescriptions = displayedPrescriptions.Take(25).ToList()
            };

            return View(vm);
        }
    }
}