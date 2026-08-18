using Microsoft.AspNetCore.Authorization;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HUIOTClinicMS.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public StudentController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.PatientID == null)
            {
                // No linked Patient record yet (e.g. self-registered account
                // not yet connected to a Patient by a Nurse). Show empty state.
                return View(new StudentDashboardViewModel());
            }

            var patientId = currentUser.PatientID.Value;

            var vm = new StudentDashboardViewModel
            {
                Patient = await _context.Patients.FindAsync(patientId),

                MyVisits = await _context.MedicalRecords.CountAsync(m => m.PatientID == patientId),

                PendingLabResults = await _context.LabRequests
                    .CountAsync(l => l.PatientID == patientId && l.Status != "Completed"),

                ActivePrescriptions = await _context.Prescriptions
                    .CountAsync(p => p.PatientID == patientId),

                RecentVisits = await _context.MedicalRecords
                    .Include(m => m.CreatedBy)
                    .Where(m => m.PatientID == patientId)
                    .OrderByDescending(m => m.DateTime)
                    .Take(5)
                    .ToListAsync()
            };

            return View(vm);
        }
    }
}