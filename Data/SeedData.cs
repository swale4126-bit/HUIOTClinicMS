using HUIOTClinicMS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HUIOTClinicMS.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            await context.Database.MigrateAsync();

            string[] roles = { "HealthOfficer", "Nurse", "LabTechnician", "Pharmacist", "Student" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // One-time cleanup: fold old pre-existing rows into the canonical names below,
            // reassigning any patients that reference them so nothing is orphaned.
            await MergeLegacyDepartmentAsync(context, "Computer Science", "Computer Science (CS)");
            await MergeLegacyDormBlockAsync(context, "Block A", "Block-1");

            string[] departmentNames =
            {
                "Information Systems (IS)",
                "Information Technology (IT)",
                "Computer Science (CS)",
                "Electromechanical Engineering",
                "Biomedical Engineering",
                "Electrical Engineering",
                "Construction Technology and Management (COTM)",
                "Veterinary Medicine",
                "Urban Planning / Urban Engineering",
                "Civil Engineering",
                "Electrical and Computer Engineering"
            };

            var existingDepartments = await context.Departments
                .Select(d => d.DepartmentName)
                .ToListAsync();

            foreach (var name in departmentNames)
            {
                if (!existingDepartments.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    context.Departments.Add(new Department { DepartmentName = name });
                }
            }
            await context.SaveChangesAsync();

            string[] dormBlockNames =
            {
                "Block-1",
                "Block-2",
                "Block-3",
                "Block-4",
                "Block-5",
                "Block-6",
                "Block-7"
            };

            var existingDormBlocks = await context.DormBlocks
                .Select(b => b.BlockName)
                .ToListAsync();

            foreach (var name in dormBlockNames)
            {
                if (!existingDormBlocks.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    context.DormBlocks.Add(new DormBlock { BlockName = name });
                }
            }
            await context.SaveChangesAsync();

            if (!context.TestTypes.Any())
            {
                context.TestTypes.AddRange(
                    new TestType { TestName = "Stool Examination" },
                    new TestType { TestName = "Urine Analysis" },
                    new TestType { TestName = "Blood Film" },
                    new TestType { TestName = "WBC (White Blood Cell Count)" },
                    new TestType { TestName = "Differential Count" },
                    new TestType { TestName = "Neutrophils (%)" },
                    new TestType { TestName = "Lymphocytes (%)" },
                    new TestType { TestName = "Eosinophils (%)" },
                    new TestType { TestName = "Monocytes (%)" },
                    new TestType { TestName = "Basophils (%)" },
                    new TestType { TestName = "RBC / Hemoglobin (Hgb)" }
                );
                await context.SaveChangesAsync();
            }

            if (!context.ServicePoints.Any())
            {
                context.ServicePoints.AddRange(
                    new ServicePoint { Name = "Registration Desk", Code = "REG", RoomNumber = "101" },
                    new ServicePoint { Name = "Consultation Room 1", Code = "CON", RoomNumber = "102" },
                    new ServicePoint { Name = "Laboratory", Code = "LAB", RoomNumber = "103" },
                    new ServicePoint { Name = "Pharmacy", Code = "PHA", RoomNumber = "104" }
                );
                await context.SaveChangesAsync();
            }

            var department = context.Departments.First();
            var dormBlock = context.DormBlocks.First();

            await CreateStaffDemoUser(userManager, context, "healthofficer@hu.edu.et", "Demo@123", "HealthOfficer", "Solomon", "Girma");
            await CreateStaffDemoUser(userManager, context, "nurse@hu.edu.et", "Demo@123", "Nurse", "Tigist", "Worku");
            await CreateStaffDemoUser(userManager, context, "labtech@hu.edu.et", "Demo@123", "LabTechnician", "Yonas", "Abebe");
            await CreateStaffDemoUser(userManager, context, "pharmacist@hu.edu.et", "Demo@123", "Pharmacist", "Hiwot", "Demisse");

            await CreateStudentDemoUser(userManager, context, "student@hu.edu.et", "Demo@123",
                "Amara", "Tadesse", department.ID, dormBlock.ID);
        }

        private static async Task MergeLegacyDepartmentAsync(ApplicationDbContext context, string legacyName, string canonicalName)
        {
            var legacy = await context.Departments.FirstOrDefaultAsync(d => d.DepartmentName == legacyName);
            if (legacy == null) return; // already cleaned up, or never existed under this name

            var canonical = await context.Departments.FirstOrDefaultAsync(d => d.DepartmentName == canonicalName);

            if (canonical == null)
            {
                // No canonical row yet — just rename the legacy one in place.
                legacy.DepartmentName = canonicalName;
                await context.SaveChangesAsync();
                return;
            }

            if (canonical.ID == legacy.ID) return; // same row already

            // Both exist: repoint any patients from the legacy row to the canonical row, then remove the legacy row.
            var affectedPatients = await context.Patients
                .Where(p => p.DepartmentID == legacy.ID)
                .ToListAsync();

            foreach (var patient in affectedPatients)
            {
                patient.DepartmentID = canonical.ID;
            }

            context.Departments.Remove(legacy);
            await context.SaveChangesAsync();
        }

        private static async Task MergeLegacyDormBlockAsync(ApplicationDbContext context, string legacyName, string canonicalName)
        {
            var legacy = await context.DormBlocks.FirstOrDefaultAsync(b => b.BlockName == legacyName);
            if (legacy == null) return;

            var canonical = await context.DormBlocks.FirstOrDefaultAsync(b => b.BlockName == canonicalName);

            if (canonical == null)
            {
                legacy.BlockName = canonicalName;
                await context.SaveChangesAsync();
                return;
            }

            if (canonical.ID == legacy.ID) return;

            var affectedPatients = await context.Patients
                .Where(p => p.DormBlockID == legacy.ID)
                .ToListAsync();

            foreach (var patient in affectedPatients)
            {
                patient.DormBlockID = canonical.ID;
            }

            context.DormBlocks.Remove(legacy);
            await context.SaveChangesAsync();
        }

        private static async Task CreateStaffDemoUser(
            UserManager<ApplicationUser> userManager, ApplicationDbContext context,
            string email, string password, string role, string firstName, string lastName)
        {
            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                if (!await userManager.IsInRoleAsync(existingUser, role))
                {
                    await userManager.AddToRoleAsync(existingUser, role);
                }
                return;
            }

            var staff = new Staff { FirstName = firstName, LastName = lastName, Role = role };
            context.Staffs.Add(staff);
            await context.SaveChangesAsync();

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = $"{firstName} {lastName}",
                StaffID = staff.ID
            };

            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }

        private static async Task CreateStudentDemoUser(
            UserManager<ApplicationUser> userManager, ApplicationDbContext context,
            string email, string password, string firstName, string lastName,
            int departmentId, int dormBlockId)
        {
            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                if (!await userManager.IsInRoleAsync(existingUser, "Student"))
                {
                    await userManager.AddToRoleAsync(existingUser, "Student");
                }
                return;
            }

            var patient = await context.Patients.FirstOrDefaultAsync(p => p.CardNumber == "HU-2024-0042");
            if (patient == null)
            {
                patient = new Patient
                {
                    IDNumber = "HU-2024-0042",
                    FirstName = firstName,
                    LastName = lastName,
                    Gender = "Female",
                    DateOfBirth = new DateTime(2002, 3, 15),
                    DepartmentID = departmentId,
                    DormBlockID = dormBlockId,
                    PhoneNumber = "0912-345-678",
                    CardNumber = "HU-2024-0042"
                };
                context.Patients.Add(patient);
                await context.SaveChangesAsync();
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = $"{firstName} {lastName}",
                PatientID = patient.ID
            };

            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, "Student");
            }
        }
    }
}