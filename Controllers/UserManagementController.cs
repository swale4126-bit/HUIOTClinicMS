using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.Models;
using HUIOTClinicMS.ViewModels;

namespace HUIOTClinicMS.Controllers
{
    [Authorize(Roles = "HealthOfficer")]
    public class UserManagementController : Controller
    {
        private static readonly string[] StaffRoles = { "Nurse", "LabTechnician", "Pharmacist" };

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public UserManagementController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // GET: UserManagement
        public async Task<IActionResult> Index(string? search)
        {
            var users = _userManager.Users.ToList();
            var list = new List<UserListItemViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                list.Add(new UserListItemViewModel
                {
                    Id = user.Id,
                    FullName = string.IsNullOrWhiteSpace(user.FullName) ? "(no name)" : user.FullName,
                    Email = user.Email ?? "—",
                    Role = roles.FirstOrDefault() ?? "(no role)",
                    IsActive = user.LockoutEnd == null || user.LockoutEnd < DateTimeOffset.UtcNow
                });
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                list = list
                    .Where(u => u.FullName.Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            ViewBag.SearchTerm = search;
            return View(list.OrderBy(u => u.FullName).ToList());
        }

        // GET: UserManagement/Create
        public IActionResult Create()
        {
            PopulateRoleDropdown();
            return View(new UserCreateViewModel());
        }

        // POST: UserManagement/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                PopulateRoleDropdown();
                return View(vm);
            }

            var existing = await _userManager.FindByEmailAsync(vm.Email);
            if (existing != null)
            {
                ModelState.AddModelError(nameof(vm.Email), "An account with this email already exists.");
                PopulateRoleDropdown();
                return View(vm);
            }

            // Create linked Staff record, matching the pattern used for seeded accounts.
            var nameParts = vm.FullName.Trim().Split(' ', 2);
            var staff = new Staff
            {
                FirstName = nameParts[0],
                LastName = nameParts.Length > 1 ? nameParts[1] : "",
                Role = vm.Role,
                PhoneNumber = vm.PhoneNumber
            };
            _context.Staffs.Add(staff);
            await _context.SaveChangesAsync();

            var user = new ApplicationUser
            {
                UserName = vm.Email,
                Email = vm.Email,
                EmailConfirmed = true,
                FullName = vm.FullName,
                PhoneNumber = vm.PhoneNumber,
                StaffID = staff.ID
            };

            var result = await _userManager.CreateAsync(user, vm.Password);
            if (!result.Succeeded)
            {
                _context.Staffs.Remove(staff);
                await _context.SaveChangesAsync();

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                PopulateRoleDropdown();
                return View(vm);
            }

            await _userManager.AddToRoleAsync(user, vm.Role);

            TempData["SuccessMessage"] = $"Account created for {vm.FullName} ({vm.Role}).";
            return RedirectToAction(nameof(Index));
        }

        // GET: UserManagement/Edit/{id}
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            var vm = new UserEditViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? "—",
                Role = roles.FirstOrDefault() ?? "",
                IsActive = user.LockoutEnd == null || user.LockoutEnd < DateTimeOffset.UtcNow
            };

            PopulateRoleDropdown();
            return View(vm);
        }

        // POST: UserManagement/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, UserEditViewModel vm)
        {
            if (id != vm.Id) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (user.Id == _userManager.GetUserId(User))
            {
                ModelState.AddModelError(string.Empty, "You cannot edit your own account from this screen.");
            }

            if (!ModelState.IsValid)
            {
                PopulateRoleDropdown();
                return View(vm);
            }

            user.FullName = vm.FullName;

            var currentRoles = await _userManager.GetRolesAsync(user);
            if (!currentRoles.Contains(vm.Role))
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, vm.Role);
            }

            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, vm.IsActive ? null : DateTimeOffset.MaxValue);

            await _userManager.UpdateAsync(user);

            TempData["SuccessMessage"] = $"Account for {vm.FullName} updated.";
            return RedirectToAction(nameof(Index));
        }

        // GET: UserManagement/Delete/{id}
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            var vm = new UserListItemViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? "—",
                Role = roles.FirstOrDefault() ?? "(no role)"
            };

            return View(vm);
        }

        // POST: UserManagement/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            if (id == _userManager.GetUserId(User))
            {
                TempData["ErrorMessage"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                await _userManager.DeleteAsync(user);
            }

            TempData["SuccessMessage"] = "Account deleted.";
            return RedirectToAction(nameof(Index));
        }

        private void PopulateRoleDropdown()
        {
            ViewBag.Roles = new SelectList(StaffRoles);
        }
    }
}
