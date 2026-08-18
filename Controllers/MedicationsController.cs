using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HUIOTClinicMS.Data;

namespace HUIOTClinicMS.Controllers
{
    [Authorize(Roles = "Pharmacist,HealthOfficer")]
    public class MedicationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MedicationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: MEDICATIONS
        public async Task<IActionResult> Index(string? search)
        {
            var query = _context.Medications
                .Include(m => m.Prescription)
                    .ThenInclude(p => p!.Patient)
                .Include(m => m.Pharmacist)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLower();
                query = query.Where(m => m.MedicationName.ToLower().Contains(term));
            }

            var medications = await query
                .OrderByDescending(m => m.DispensedDate)
                .ToListAsync();

            ViewBag.SearchTerm = search;
            return View(medications);
        }

        // GET: MEDICATIONS/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var medication = await _context.Medications
                .Include(m => m.Prescription)
                    .ThenInclude(p => p!.Patient)
                .Include(m => m.Pharmacist)
                .FirstOrDefaultAsync(m => m.MedicationID == id);

            if (medication == null) return NotFound();

            return View(medication);
        }
    }
}
