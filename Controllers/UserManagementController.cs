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
        private static readonly string[] StaffRoles = { "HealthOfficer", "Nurse", "LabTechnician", "Pharmacist" };

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
                var isLockedOut = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow;
                
                // Also check linked Staff record if available
                var staff = user.StaffID.HasValue ? await _context.Staffs.FindAsync(user.StaffID.Value) : null;
                var isActive = !isLockedOut && (staff == null || staff.IsActive);

                list.Add(new UserListItemViewModel
                {
                    Id = user.Id,
                    FullName = string.IsNullOrWhiteSpace(user.FullName) ? "(no name)" : user.FullName,
                    Email = user.Email ?? "—",
                    Role = roles.FirstOrDefault() ?? "(no role)",
                    IsActive = isActive
                });
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                list = list
                    .Where(u => u.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                u.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                u.Role.Contains(search, StringComparison.OrdinalIgnoreCase))
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

            // Create linked Staff record, active by default
            var nameParts = vm.FullName.Trim().Split(' ', 2);
            var staff = new Staff
            {
                FirstName = nameParts[0],
                LastName = nameParts.Length > 1 ? nameParts[1] : "",
                Role = vm.Role,
                PhoneNumber = vm.PhoneNumber,
                IsActive = true
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
                StaffID = staff.ID,
                LockoutEnabled = true,
                LockoutEnd = null
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
            var staff = user.StaffID.HasValue ? await _context.Staffs.FindAsync(user.StaffID.Value) : null;
            var isLockedOut = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow;
            var isActive = !isLockedOut && (staff == null || staff.IsActive);

            var vm = new UserEditViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? "—",
                Role = roles.FirstOrDefault() ?? "",
                IsActive = isActive
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

            var currentUserId = _userManager.GetUserId(User);
            if (user.Id == currentUserId)
            {
                ModelState.AddModelError(string.Empty, "You cannot edit or alter the permissions of your own account from this screen.");
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

            // Sync lockout and staff record
            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, vm.IsActive ? null : DateTimeOffset.MaxValue);

            if (user.StaffID.HasValue)
            {
                var staff = await _context.Staffs.FindAsync(user.StaffID.Value);
                if (staff != null)
                {
                    staff.Role = vm.Role;
                    staff.IsActive = vm.IsActive;
                }
            }

            await _context.SaveChangesAsync();
            await _userManager.UpdateAsync(user);

            TempData["SuccessMessage"] = $"Account for {vm.FullName} updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: UserManagement/ToggleStatus/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(string id)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (id == currentUserId)
            {
                TempData["ErrorMessage"] = "You cannot deactivate your own account.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var isCurrentlyLocked = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow;
            bool newActiveStatus = isCurrentlyLocked; // If locked, activating it. If unlocked, deactivating.

            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, newActiveStatus ? null : DateTimeOffset.MaxValue);

            if (user.StaffID.HasValue)
            {
                var staff = await _context.Staffs.FindAsync(user.StaffID.Value);
                if (staff != null)
                {
                    staff.IsActive = newActiveStatus;
                }
            }

            await _context.SaveChangesAsync();
            await _userManager.UpdateAsync(user);

            TempData["SuccessMessage"] = newActiveStatus 
                ? $"Account for {user.FullName} has been reactivated." 
                : $"Account for {user.FullName} has been deactivated. Historical clinical records and audit trails remain intact.";

            return RedirectToAction(nameof(Index));
        }

        // GET: UserManagement/Deactivate/{id}
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

        // POST: UserManagement/Deactivate/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (id == currentUserId)
            {
                TempData["ErrorMessage"] = "You cannot deactivate your own account.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                // Soft deactivation preserves clinical audit trails and foreign keys
                await _userManager.SetLockoutEnabledAsync(user, true);
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

                if (user.StaffID.HasValue)
                {
                    var staff = await _context.Staffs.FindAsync(user.StaffID.Value);
                    if (staff != null)
                    {
                        staff.IsActive = false;
                    }
                }

                await _context.SaveChangesAsync();
                await _userManager.UpdateAsync(user);
                TempData["SuccessMessage"] = $"Account for {user.FullName} deactivated to preserve clinical history.";
            }

            return RedirectToAction(nameof(Index));
        }

        private void PopulateRoleDropdown()
        {
            ViewBag.Roles = new SelectList(StaffRoles);
        }
    }
}
