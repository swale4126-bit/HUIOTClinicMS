using Microsoft.AspNetCore.Authorization;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HUIOTClinicMS.Controllers
{
    [Authorize(Roles = "HealthOfficer")]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;

            var vm = new DashboardViewModel
            {
                TotalPatients = await _context.Patients.CountAsync(),

                MedicalRecordsToday = await _context.MedicalRecords
                    .CountAsync(m => m.DateTime.Date == today),

                PendingLabRequests = await _context.LabRequests
                    .CountAsync(l => l.Status == "Pending"),

                ActiveStaff = await _context.Staffs.CountAsync(),

                RecentMedicalRecords = await _context.MedicalRecords
                    .Include(m => m.Patient)
                    .Include(m => m.CreatedBy)
                    .OrderByDescending(m => m.DateTime)
                    .Take(5)
                    .ToListAsync(),

                RecentLabRequests = await _context.LabRequests
                    .Include(l => l.Patient)
                    .Include(l => l.RequestedBy)
                    .OrderByDescending(l => l.RequestDate)
                    .Take(5)
                    .ToListAsync()
            };

            return View(vm);
        }
    }
}