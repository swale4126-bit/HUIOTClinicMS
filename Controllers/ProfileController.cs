using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.ViewModels;

namespace HUIOTClinicMS.Controllers
{
    [Authorize]
    public class ProfileController(UserManager<ApplicationUser> userManager, IWebHostEnvironment env) : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager = userManager;
        private readonly IWebHostEnvironment _env = env;

        private static readonly string[] AllowedPhotoExtensions = [".jpg", ".jpeg", ".png", ".webp"];
        private const long MaxPhotoSizeBytes = 2 * 1024 * 1024; // 2 MB

        private string RoleLabel()
        {
            if (User.IsInRole("HealthOfficer")) return "Health Officer";
            if (User.IsInRole("Nurse")) return "Nurse";
            if (User.IsInRole("LabTechnician")) return "Lab Technician";
            if (User.IsInRole("Pharmacist")) return "Pharmacist";
            if (User.IsInRole("Student")) return "Student";
            return "User";
        }

        // GET: PROFILE
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var vm = new ProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email ?? "—",
                PhoneNumber = user.PhoneNumber,
                RoleLabel = RoleLabel(),
                ProfilePhotoUrl = user.ProfilePhotoUrl
            };

            return View(vm);
        }

        // GET: PROFILE/Edit
        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var vm = new ProfileEditViewModel
            {
                FullName = user.FullName,
                Email = user.Email ?? "—",
                PhoneNumber = user.PhoneNumber
            };

            return View(vm);
        }

        // POST: PROFILE/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProfileEditViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            user.FullName = vm.FullName;
            user.PhoneNumber = vm.PhoneNumber;
            await _userManager.UpdateAsync(user);

            TempData["SuccessMessage"] = "Profile updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: PROFILE/UploadPhoto
        [HttpGet]
        public async Task<IActionResult> UploadPhoto()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            ViewBag.CurrentPhotoUrl = user.ProfilePhotoUrl;
            return View();
        }

        // POST: PROFILE/UploadPhoto
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadPhoto(IFormFile? photo)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (photo == null || photo.Length == 0)
            {
                ModelState.AddModelError(string.Empty, "Choose an image to upload.");
                ViewBag.CurrentPhotoUrl = user.ProfilePhotoUrl;
                return View();
            }

            var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
            if (!AllowedPhotoExtensions.Contains(ext))
            {
                ModelState.AddModelError(string.Empty, "Only JPG, PNG, or WEBP images are allowed.");
                ViewBag.CurrentPhotoUrl = user.ProfilePhotoUrl;
                return View();
            }

            if (photo.Length > MaxPhotoSizeBytes)
            {
                ModelState.AddModelError(string.Empty, "Image must be smaller than 2 MB.");
                ViewBag.CurrentPhotoUrl = user.ProfilePhotoUrl;
                return View();
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profile-photos");
            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{user.Id}{ext}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await photo.CopyToAsync(stream);
            }

            user.ProfilePhotoUrl = $"/uploads/profile-photos/{fileName}";
            await _userManager.UpdateAsync(user);

            TempData["SuccessMessage"] = "Profile photo updated.";
            return RedirectToAction(nameof(Index));
        }
    }
}