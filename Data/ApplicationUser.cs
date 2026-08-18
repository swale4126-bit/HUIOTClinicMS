using Microsoft.AspNetCore.Identity;

namespace HUIOTClinicMS.Data
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;

        // Set for staff accounts (Health Officer, Nurse, Lab Technician, Pharmacist)
        public int? StaffID { get; set; }

        // Set for Student/Patient accounts
        public int? PatientID { get; set; }

        // Relative path under wwwroot, e.g. "/uploads/profile-photos/abc123.jpg"
        public string? ProfilePhotoUrl { get; set; }
    }
}