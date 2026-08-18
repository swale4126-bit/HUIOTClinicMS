using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.ViewModels;

namespace HUIOTClinicMS.Controllers
{
    [Authorize]
    public class SettingsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public SettingsController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var staff = user.StaffID != null ? await _context.Staffs.FindAsync(user.StaffID.Value) : null;

            var vm = new SettingsViewModel
            {
                FullName = user.FullName,
                Email = user.Email ?? "—",
                PhoneNumber = staff?.PhoneNumber
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var staff = user.StaffID != null ? await _context.Staffs.FindAsync(user.StaffID.Value) : null;

            var vm = new SettingsViewModel
            {
                FullName = user.FullName,
                Email = user.Email ?? "—",
                PhoneNumber = staff?.PhoneNumber
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SettingsViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            user.FullName = vm.FullName;
            await _userManager.UpdateAsync(user);

            if (user.StaffID != null)
            {
                var staff = await _context.Staffs.FindAsync(user.StaffID.Value);
                if (staff != null)
                {
                    staff.PhoneNumber = vm.PhoneNumber;
                    await _context.SaveChangesAsync();
                }
            }

            TempData["SuccessMessage"] = "Settings updated successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}