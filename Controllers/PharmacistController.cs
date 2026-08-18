using Microsoft.AspNetCore.Authorization;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HUIOTClinicMS.Controllers
{
    [Authorize(Roles = "Pharmacist")]
    public class PharmacistController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PharmacistController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;

            var vm = new PharmacistDashboardViewModel
            {
                PrescriptionsToday = await _context.Prescriptions
                    .CountAsync(p => p.TimeDate.Date == today),

                TotalMedicationsInRegistry = await _context.Medications.CountAsync(),

                TotalPrescriptions = await _context.Prescriptions.CountAsync(),

                RecentPrescriptions = await _context.Prescriptions
                    .Include(p => p.Patient)
                    .Include(p => p.PrescribedBy)
                    .OrderByDescending(p => p.TimeDate)
                    .Take(10)
                    .ToListAsync()
            };

            return View(vm);
        }
    }
}