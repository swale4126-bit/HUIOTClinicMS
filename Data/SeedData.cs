using HUIOTClinicMS.Models;
using HUIOTClinicMS.Models.Enums;
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

            string[] roles = { "HealthOfficer", "Nurse", "LabTechnician", "Pharmacist" };
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
                    new ServicePoint { Name = "Triage & Consultation", Code = "TRI", RoomNumber = "101B" },
                    new ServicePoint { Name = "Consultation Room 1", Code = "CON", RoomNumber = "102" },
                    new ServicePoint { Name = "Laboratory", Code = "LAB", RoomNumber = "103" },
                    new ServicePoint { Name = "Pharmacy", Code = "PHA", RoomNumber = "104" }
                );
                await context.SaveChangesAsync();
            }
            else
            {
                var triSp = await context.ServicePoints.FirstOrDefaultAsync(s => s.Code == "TRI");
                if (triSp == null)
                {
                    context.ServicePoints.Add(new ServicePoint { Name = "Triage & Consultation", Code = "TRI", RoomNumber = "101B" });
                    await context.SaveChangesAsync();
                }
                else if (triSp.Name != "Triage & Consultation")
                {
                    triSp.Name = "Triage & Consultation";
                    await context.SaveChangesAsync();
                }

                var conSp = await context.ServicePoints.FirstOrDefaultAsync(s => s.Code == "CON");
                if (conSp != null && conSp.IsActive)
                {
                    conSp.IsActive = false;
                    await context.SaveChangesAsync();
                }
            }

            var department = context.Departments.First();
            var dormBlock = context.DormBlocks.First();

            // Perform staff deduplication and deactivate requested nurses (Meron Alemu, Tigist Bekele, wale solomon)
            await CleanDuplicateStaffAndNursesAsync(context, userManager);

            // Only bootstrap an initial Health Officer account if no Health Officer exists in the database
            var existingHealthOfficers = await userManager.GetUsersInRoleAsync("HealthOfficer");
            if (!existingHealthOfficers.Any())
            {
                await CreateStaffBootstrapUser(userManager, context, "healthofficer@hu.edu.et", "HealthOfficer", "Solomon", "Girma");
            }

            await BackfillLegacyEncountersAsync(context);
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

        private static async Task CreateStaffBootstrapUser(
            UserManager<ApplicationUser> userManager, ApplicationDbContext context,
            string email, string role, string firstName, string lastName)
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

            var staff = new Staff { FirstName = firstName, LastName = lastName, Role = role, IsActive = true };
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

            var result = await userManager.CreateAsync(user, "Admin@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }

        private static async Task BackfillLegacyEncountersAsync(ApplicationDbContext context)
        {
            var unlinkedRecords = await context.MedicalRecords
                .Where(mr => mr.EncounterID == null)
                .ToListAsync();

            if (!unlinkedRecords.Any()) return;

            foreach (var mr in unlinkedRecords)
            {
                var recordTime = mr.DateTime != default ? mr.DateTime : (mr.CreatedAt != default ? mr.CreatedAt : DateTime.UtcNow);
                var encounter = new ClinicalEncounter
                {
                    PatientID = mr.PatientID,
                    Type = EncounterType.Routine,
                    Status = WorkflowState.Discharged,
                    StartTime = recordTime,
                    EndTime = recordTime.AddMinutes(30),
                    CreatedAt = recordTime,
                    CreatedBy = mr.CreatedBy ?? "LegacyMigrationSystem"
                };

                context.ClinicalEncounters.Add(encounter);
                await context.SaveChangesAsync();

                mr.EncounterID = encounter.ID;

                var labRequests = await context.LabRequests
                    .Where(lr => lr.MedicalRecordID == mr.ID && lr.ClinicalEncounterID == null)
                    .ToListAsync();
                foreach (var lr in labRequests)
                {
                    lr.ClinicalEncounterID = encounter.ID;
                }

                var minTime = recordTime.AddHours(-24);
                var maxTime = recordTime.AddHours(24);
                var prescriptions = await context.Prescriptions
                    .Where(p => p.PatientID == mr.PatientID && p.ClinicalEncounterID == null && p.TimeDate >= minTime && p.TimeDate <= maxTime)
                    .ToListAsync();
                foreach (var p in prescriptions)
                {
                    p.ClinicalEncounterID = encounter.ID;
                }
            }

            await context.SaveChangesAsync();
        }

        private static async Task CleanDuplicateStaffAndNursesAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            // 1. Remove/deactivate nurses requested by user: Meron Alemu, Tigist Bekele, wale solomon
            var nursesToRemove = await context.Staffs
                .Where(s => (s.FirstName == "Meron" && s.LastName == "Alemu")
                         || (s.FirstName == "Tigist" && s.LastName == "Bekele")
                         || (s.FirstName.ToLower() == "wale" && s.LastName.ToLower() == "solomon"))
                .ToListAsync();

            foreach (var n in nursesToRemove)
            {
                n.IsActive = false;
                var users = await userManager.Users
                    .Where(u => u.StaffID == n.ID || u.Email == "nurse.meron@hu.edu.et" || u.Email == "nurse.tigist@hu.edu.et")
                    .ToListAsync();
                foreach (var u in users)
                {
                    await userManager.SetLockoutEnabledAsync(u, true);
                    await userManager.SetLockoutEndDateAsync(u, DateTimeOffset.MaxValue);
                }
            }
            await context.SaveChangesAsync();

            // 2. Inverted "Girma Solomon" normalization -> ensure FirstName = Solomon, LastName = Girma
            var invertedGirmaSolomons = await context.Staffs
                .Where(s => s.FirstName == "Girma" && s.LastName == "Solomon")
                .ToListAsync();
            foreach (var inv in invertedGirmaSolomons)
            {
                inv.FirstName = "Solomon";
                inv.LastName = "Girma";
            }
            if (invertedGirmaSolomons.Any())
            {
                await context.SaveChangesAsync();
            }

            var invertedUsers = await userManager.Users
                .Where(u => u.FullName == "Girma Solomon")
                .ToListAsync();
            foreach (var u in invertedUsers)
            {
                u.FullName = "Solomon Girma";
                await userManager.UpdateAsync(u);
            }

            // 3. Deduplicate Solomon Girma (HealthOfficer) - keep the primary one, deactivate/repoint duplicate
            var solomons = await context.Staffs
                .Where(s => s.FirstName == "Solomon" && s.LastName == "Girma")
                .ToListAsync();

            if (solomons.Count > 1)
            {
                var primarySolomon = solomons.FirstOrDefault(s => context.Users.Any(u => u.StaffID == s.ID && (u.Email == "healthofficer@hu.edu.et" || u.Email == "admin@hu.edu.et")))
                                     ?? solomons.First();

                var duplicates = solomons.Where(s => s.ID != primarySolomon.ID).ToList();
                foreach (var dup in duplicates)
                {
                    var encounters = await context.ClinicalEncounters.Where(e => e.AssignedToStaffID == dup.ID).ToListAsync();
                    foreach (var e in encounters) e.AssignedToStaffID = primarySolomon.ID;

                    var medRecords = await context.MedicalRecords.Where(m => m.CreatedByID == dup.ID).ToListAsync();
                    foreach (var m in medRecords) m.CreatedByID = primarySolomon.ID;

                    var labReqs = await context.LabRequests.Where(l => l.AssignedToStaffID == dup.ID).ToListAsync();
                    foreach (var l in labReqs) l.AssignedToStaffID = primarySolomon.ID;

                    var labReqsReq = await context.LabRequests.Where(l => l.RequestedByID == dup.ID).ToListAsync();
                    foreach (var l in labReqsReq) l.RequestedByID = primarySolomon.ID;

                    var rxs = await context.Prescriptions.Where(p => p.PrescribedByID == dup.ID).ToListAsync();
                    foreach (var p in rxs) p.PrescribedByID = primarySolomon.ID;

                    var rxsAssigned = await context.Prescriptions.Where(p => p.AssignedToStaffID == dup.ID).ToListAsync();
                    foreach (var p in rxsAssigned) p.AssignedToStaffID = primarySolomon.ID;

                    var tasksTo = await context.TaskAssignmentHistories.Where(t => t.AssignedToStaffID == dup.ID).ToListAsync();
                    foreach (var t in tasksTo) t.AssignedToStaffID = primarySolomon.ID;

                    var tasksFrom = await context.TaskAssignmentHistories.Where(t => t.AssignedFromStaffID == dup.ID).ToListAsync();
                    foreach (var t in tasksFrom) t.AssignedFromStaffID = primarySolomon.ID;

                    dup.IsActive = false;

                    var dupUsers = await context.Users.Where(u => u.StaffID == dup.ID).ToListAsync();
                    foreach (var u in dupUsers)
                    {
                        if (u.Email != "healthofficer@hu.edu.et" && u.Email != "admin@hu.edu.et")
                        {
                            await userManager.SetLockoutEnabledAsync(u, true);
                            await userManager.SetLockoutEndDateAsync(u, DateTimeOffset.MaxValue);
                        }
                        else
                        {
                            u.StaffID = primarySolomon.ID;
                        }
                    }
                }
                primarySolomon.IsActive = true;
                await context.SaveChangesAsync();
            }

            // 4. Deduplicate Tigist Worku (Nurse)
            var tigistWorkus = await context.Staffs
                .Where(s => s.FirstName == "Tigist" && s.LastName == "Worku")
                .ToListAsync();

            if (tigistWorkus.Count > 1)
            {
                var primaryTigist = tigistWorkus.FirstOrDefault(s => context.Users.Any(u => u.StaffID == s.ID && u.Email == "nurse.worku@hu.edu.et"))
                                    ?? tigistWorkus.First();

                var duplicates = tigistWorkus.Where(s => s.ID != primaryTigist.ID).ToList();
                foreach (var dup in duplicates)
                {
                    var encounters = await context.ClinicalEncounters.Where(e => e.AssignedToStaffID == dup.ID).ToListAsync();
                    foreach (var e in encounters) e.AssignedToStaffID = primaryTigist.ID;

                    var medRecords = await context.MedicalRecords.Where(m => m.CreatedByID == dup.ID).ToListAsync();
                    foreach (var m in medRecords) m.CreatedByID = primaryTigist.ID;

                    var labReqs = await context.LabRequests.Where(l => l.AssignedToStaffID == dup.ID).ToListAsync();
                    foreach (var l in labReqs) l.AssignedToStaffID = primaryTigist.ID;

                    var labReqsReq = await context.LabRequests.Where(l => l.RequestedByID == dup.ID).ToListAsync();
                    foreach (var l in labReqsReq) l.RequestedByID = primaryTigist.ID;

                    var rxs = await context.Prescriptions.Where(p => p.PrescribedByID == dup.ID).ToListAsync();
                    foreach (var p in rxs) p.PrescribedByID = primaryTigist.ID;

                    var rxsAssigned = await context.Prescriptions.Where(p => p.AssignedToStaffID == dup.ID).ToListAsync();
                    foreach (var p in rxsAssigned) p.AssignedToStaffID = primaryTigist.ID;

                    var tasksTo = await context.TaskAssignmentHistories.Where(t => t.AssignedToStaffID == dup.ID).ToListAsync();
                    foreach (var t in tasksTo) t.AssignedToStaffID = primaryTigist.ID;

                    var tasksFrom = await context.TaskAssignmentHistories.Where(t => t.AssignedFromStaffID == dup.ID).ToListAsync();
                    foreach (var t in tasksFrom) t.AssignedFromStaffID = primaryTigist.ID;

                    dup.IsActive = false;

                    var dupUsers = await context.Users.Where(u => u.StaffID == dup.ID).ToListAsync();
                    foreach (var u in dupUsers)
                    {
                        if (u.Email != "nurse.worku@hu.edu.et")
                        {
                            await userManager.SetLockoutEnabledAsync(u, true);
                            await userManager.SetLockoutEndDateAsync(u, DateTimeOffset.MaxValue);
                        }
                        else
                        {
                            u.StaffID = primaryTigist.ID;
                        }
                    }
                }
                primaryTigist.IsActive = true;
                await context.SaveChangesAsync();
            }

            // 5. Ensure Sam exists as active Lab Technician and link user
            var samStaff = await context.Staffs.FirstOrDefaultAsync(s => s.FirstName.Trim().ToLower() == "sam" || s.LastName.Trim().ToLower() == "sam");
            if (samStaff == null)
            {
                samStaff = new Staff
                {
                    FirstName = "Sam",
                    LastName = "",
                    Role = "LabTechnician",
                    IsActive = true
                };
                context.Staffs.Add(samStaff);
                await context.SaveChangesAsync();
            }
            else
            {
                samStaff.FirstName = "Sam";
                samStaff.LastName = "";
                samStaff.Role = "LabTechnician";
                samStaff.IsActive = true;
                await context.SaveChangesAsync();
            }

            // Link any Sam user account (e.g. Sam4126@gmail.com, sam@hu.edu.et) to Sam staff
            var samUsers = await userManager.Users
                .Where(u => u.Email.Contains("sam4126") || u.UserName.Contains("sam4126") || u.Email == "sam@hu.edu.et")
                .ToListAsync();
            foreach (var su in samUsers)
            {
                su.StaffID = samStaff.ID;
                su.FullName = "Sam";
                await userManager.UpdateAsync(su);
                if (!await userManager.IsInRoleAsync(su, "LabTechnician"))
                {
                    await userManager.AddToRoleAsync(su, "LabTechnician");
                }
            }
        }
    }
}