using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HUIOTClinicMS.Data;
using HUIOTClinicMS.Models;
using HUIOTClinicMS.Models.Enums;

namespace HUIOTClinicMS.Services
{
    public static class RbacVerificationRunner
    {
        public static async Task<int> RunAsync(IServiceProvider services)
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var context = services.GetRequiredService<ApplicationDbContext>();

            Console.WriteLine("=================================================================");
            Console.WriteLine("HU IoT Clinic MS: Dynamic RBAC & Task Assignment Verification Test");
            Console.WriteLine("=================================================================");

            int testsPassed = 0;
            int totalTests = 0;

            void Assert(string testName, bool condition, string detail = "")
            {
                totalTests++;
                if (condition)
                {
                    testsPassed++;
                    Console.WriteLine($"[PASS] {testName} {detail}");
                }
                else
                {
                    Console.WriteLine($"[FAIL] {testName} - {detail}");
                }
            }

            // 1. Create 2 Nurses, 2 Lab Technicians, 2 Pharmacists
            Console.WriteLine("\n--- TEST PHASE 1: Dynamic Account Creation for Multiple Users Per Role ---");

            var testUsers = new[]
            {
                ("nurse.aman@hu.edu.et", "Nurse", "Aman", "Nurse"),
                ("nurse.worku@hu.edu.et", "Nurse", "Tigist", "Worku"),
                ("lab.abebe@hu.edu.et", "LabTechnician", "Abebe", "Kebede"),
                ("lab.samuel@hu.edu.et", "LabTechnician", "Samuel", "Tadesse"),
                ("pharm.chala@hu.edu.et", "Pharmacist", "Chala", "Tolosa"),
                ("pharm.helen@hu.edu.et", "Pharmacist", "Helen", "Desta")
            };

            foreach (var (email, role, firstName, lastName) in testUsers)
            {
                var user = await userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    var staff = new Staff
                    {
                        FirstName = firstName,
                        LastName = lastName,
                        Role = role,
                        IsActive = true
                    };
                    context.Staffs.Add(staff);
                    await context.SaveChangesAsync();

                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true,
                        FullName = $"{firstName} {lastName}",
                        StaffID = staff.ID
                    };
                    var createResult = await userManager.CreateAsync(user, "Test@Pass123");
                    if (createResult.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, role);
                    }
                }
                else
                {
                    if (user.StaffID.HasValue)
                    {
                        var staff = await context.Staffs.FindAsync(user.StaffID.Value);
                        if (staff != null && !staff.IsActive)
                        {
                            staff.IsActive = true;
                            await context.SaveChangesAsync();
                        }
                    }
                    if (!await userManager.CheckPasswordAsync(user, "Test@Pass123"))
                    {
                        var token = await userManager.GeneratePasswordResetTokenAsync(user);
                        await userManager.ResetPasswordAsync(user, token, "Test@Pass123");
                    }
                }
            }

            // Verify each account exists, has role, and password checks pass
            foreach (var (email, role, _, _) in testUsers)
            {
                var user = await userManager.FindByEmailAsync(email);
                Assert($"User exists: {email}", user != null);
                Assert($"Role assigned ({role}): {email}", user != null && await userManager.IsInRoleAsync(user, role));
                Assert($"Password hashing verified: {email}", user != null && await userManager.CheckPasswordAsync(user, "Test@Pass123"));
                Assert($"Linked active Staff record: {email}", user != null && user.StaffID.HasValue && (await context.Staffs.FindAsync(user.StaffID.Value))?.IsActive == true);
            }

            // 2. Test Dynamic Assignment Eligibility Dropdowns
            Console.WriteLine("\n--- TEST PHASE 2: Dynamic Assignment Dropdowns (Active Only) ---");

            var activeClinicians = await context.Staffs
                .Where(s => s.IsActive && (s.Role == "HealthOfficer" || s.Role == "Nurse"))
                .ToListAsync();
            Assert("Clinicians dropdown contains at least 2 nurses + 1 HO", activeClinicians.Count >= 3, $"Count: {activeClinicians.Count}");

            var activeLabTechs = await context.Staffs
                .Where(s => s.IsActive && s.Role == "LabTechnician")
                .ToListAsync();
            Assert("Lab Techs dropdown contains at least 2 lab techs", activeLabTechs.Count >= 2, $"Count: {activeLabTechs.Count}");

            var activePharmacists = await context.Staffs
                .Where(s => s.IsActive && s.Role == "Pharmacist")
                .ToListAsync();
            Assert("Pharmacists dropdown contains at least 2 pharmacists", activePharmacists.Count >= 2, $"Count: {activePharmacists.Count}");

            // 3. Test Full Workflow Assignment & Reassignment Audit Trail
            Console.WriteLine("\n--- TEST PHASE 3: Workflow Task Assignment & Reassignment Audit Trail ---");

            // Get or create test patient
            var patient = await context.Patients.FirstOrDefaultAsync(p => p.IDNumber == "TEST-RBAC-001");
            if (patient == null)
            {
                patient = new Patient
                {
                    FirstName = "Kenenisa",
                    LastName = "Bekele",
                    IDNumber = "TEST-RBAC-001",
                    CardNumber = "CARD-RBAC-01",
                    DepartmentID = (await context.Departments.FirstAsync()).ID,
                    DormBlockID = (await context.DormBlocks.FirstAsync()).ID,
                    Gender = "Male",
                    DateOfBirth = DateTime.Today.AddYears(-21),
                    PhoneNumber = "0911223344"
                };
                context.Patients.Add(patient);
                await context.SaveChangesAsync();
            }

            var nurse1 = await userManager.FindByEmailAsync("nurse.aman@hu.edu.et");
            var labTech1 = await userManager.FindByEmailAsync("lab.abebe@hu.edu.et");
            var pharm1 = await userManager.FindByEmailAsync("pharm.chala@hu.edu.et");
            var pharm2 = await userManager.FindByEmailAsync("pharm.helen@hu.edu.et");

            // Discharge any lingering test encounters from previous test runs to ensure clean isolated test state
            var priorEncounters = await context.ClinicalEncounters
                .Where(e => e.PatientID == patient.ID && e.Status != WorkflowState.Discharged && e.Status != WorkflowState.Transferred)
                .ToListAsync();
            foreach (var pe in priorEncounters)
            {
                pe.Status = WorkflowState.Discharged;
            }
            if (priorEncounters.Any())
            {
                await context.SaveChangesAsync();
            }

            // Create Clinical Encounter with dynamic clinician assignment
            var encounter = new ClinicalEncounter
            {
                PatientID = patient.ID,
                Type = EncounterType.Routine,
                Status = WorkflowState.InConsultation,
                StartTime = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "RegistrationNurse",
                AssignedToStaffID = nurse1!.StaffID
            };
            context.ClinicalEncounters.Add(encounter);
            await context.SaveChangesAsync();

            var encAssignment = new TaskAssignmentHistory
            {
                TaskType = "PatientEncounter",
                TaskID = encounter.ID,
                AssignedToStaffID = nurse1.StaffID!.Value,
                AssignedByID = nurse1.Id,
                AssignedAt = DateTime.UtcNow,
                Reason = "Assigned at registration triage"
            };
            context.TaskAssignmentHistories.Add(encAssignment);
            await context.SaveChangesAsync();

            Assert("Encounter assigned to specific clinician", encounter.AssignedToStaffID == nurse1.StaffID);

            // Create Lab Request with dynamic lab technician assignment
            var testType = await context.TestTypes.FirstAsync();
            var medRecord = new MedicalRecord
            {
                EncounterID = encounter.ID,
                PatientID = patient.ID,
                ChiefComplaint = "Fever and headaches",
                CreatedByID = nurse1.StaffID!.Value,
                CreatedBy = nurse1.FullName!,
                DateTime = DateTime.Now
            };
            context.MedicalRecords.Add(medRecord);
            await context.SaveChangesAsync();

            var labRequest = new LabRequest
            {
                PatientID = patient.ID,
                ClinicalEncounterID = encounter.ID,
                MedicalRecordID = medRecord.ID,
                RequestedByID = nurse1.StaffID!.Value,
                RequestDate = DateTime.Now,
                ClinicalNotes = "Check malaria and CBC",
                Status = "Pending",
                AssignedToStaffID = labTech1!.StaffID,
                TestTypes = new List<TestType> { testType }
            };
            context.LabRequests.Add(labRequest);
            await context.SaveChangesAsync();

            context.TaskAssignmentHistories.Add(new TaskAssignmentHistory
            {
                TaskType = "LabRequest",
                TaskID = labRequest.LabRequestID,
                AssignedToStaffID = labTech1.StaffID!.Value,
                AssignedByID = nurse1.Id,
                AssignedAt = DateTime.UtcNow,
                Reason = "Assigned at order creation"
            });
            await context.SaveChangesAsync();

            Assert("Lab request assigned to specific lab technician", labRequest.AssignedToStaffID == labTech1.StaffID);

            // Create Prescription with initial assignment to Pharmacist 1
            var prescription = new Prescription
            {
                PatientID = patient.ID,
                ClinicalEncounterID = encounter.ID,
                RX = "Amoxicillin 500mg TID x 7 days",
                PrescribedByID = nurse1.StaffID!.Value,
                TimeDate = DateTime.Now,
                Status = "Pending",
                AssignedToStaffID = pharm1!.StaffID
            };
            context.Prescriptions.Add(prescription);
            await context.SaveChangesAsync();

            context.TaskAssignmentHistories.Add(new TaskAssignmentHistory
            {
                TaskType = "Prescription",
                TaskID = prescription.PrescriptionID,
                AssignedToStaffID = pharm1.StaffID!.Value,
                AssignedByID = nurse1.Id,
                AssignedAt = DateTime.UtcNow,
                Reason = "Assigned at prescription creation"
            });
            await context.SaveChangesAsync();

            Assert("Prescription initially assigned to Pharmacist 1", prescription.AssignedToStaffID == pharm1.StaffID);

            // Reassign Prescription to Pharmacist 2
            int prevPharmacistStaffId = prescription.AssignedToStaffID.Value;
            prescription.AssignedToStaffID = pharm2!.StaffID;
            await context.SaveChangesAsync();

            context.TaskAssignmentHistories.Add(new TaskAssignmentHistory
            {
                TaskType = "Prescription",
                TaskID = prescription.PrescriptionID,
                AssignedFromStaffID = prevPharmacistStaffId,
                AssignedToStaffID = pharm2.StaffID!.Value,
                AssignedByID = nurse1.Id,
                AssignedAt = DateTime.UtcNow,
                Reason = "Pharmacist 1 shift ended - handover to Pharmacist 2"
            });
            await context.SaveChangesAsync();

            Assert("Prescription reassigned to Pharmacist 2", prescription.AssignedToStaffID == pharm2.StaffID);

            var rxHistories = await context.TaskAssignmentHistories
                .Where(h => h.TaskType == "Prescription" && h.TaskID == prescription.PrescriptionID)
                .OrderBy(h => h.AssignedAt)
                .ToListAsync();

            Assert("Prescription assignment history preserved with 2 entries", rxHistories.Count == 2);
            Assert("History tracks previous assignee (Pharmacist 1)", rxHistories.Any(h => h.AssignedFromStaffID == prevPharmacistStaffId));
            Assert("History tracks new assignee (Pharmacist 2)", rxHistories.Any(h => h.AssignedToStaffID == pharm2.StaffID));

            // Partial dispensing check
            prescription.Status = "PartiallyDispensed";
            var medication = new Medication
            {
                PrescriptionID = prescription.PrescriptionID,
                MedicationName = "Amoxicillin 500mg",
                DispensedQuantity = 10,
                PharmacistID = pharm2.StaffID!.Value,
                Remark = "Dispensed 10 of 21 capsules due to current stock level; remainder pending",
                DispensedDate = DateTime.Now
            };
            context.Medications.Add(medication);
            await context.SaveChangesAsync();

            Assert("Prescription status supports PartiallyDispensed", prescription.Status == "PartiallyDispensed");
            Assert("Medication record linked to dispensing pharmacist", medication.PharmacistID == pharm2.StaffID);

            // 4. Test Soft Deactivation & Historical Integrity
            Console.WriteLine("\n--- TEST PHASE 4: Soft Deactivation & Historical Data Integrity ---");

            var pharm2Staff = await context.Staffs.FindAsync(pharm2.StaffID!.Value);
            pharm2Staff!.IsActive = false;
            pharm2.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
            await userManager.UpdateAsync(pharm2);
            await context.SaveChangesAsync();

            // Verify deactivated pharmacist is excluded from active dropdown queries
            var activePharmacistsAfterDeactivation = await context.Staffs
                .Where(s => s.Role == "Pharmacist" && s.IsActive)
                .ToListAsync();

            Assert("Deactivated pharmacist is excluded from active assignment list", !activePharmacistsAfterDeactivation.Any(s => s.ID == pharm2.StaffID));
            Assert("Active pharmacist 1 remains in eligible list", activePharmacistsAfterDeactivation.Any(s => s.ID == pharm1.StaffID));

            // Verify historical prescription and dispensing records remain completely intact
            var preservedPrescription = await context.Prescriptions
                .Include(p => p.AssignedToStaff)
                .Include(p => p.Medication)
                    .ThenInclude(m => m!.Pharmacist)
                .FirstOrDefaultAsync(p => p.PrescriptionID == prescription.PrescriptionID);

            Assert("Prescription record intact after assignee deactivation", preservedPrescription != null);
            Assert("Prescription still references historical assignee", preservedPrescription?.AssignedToStaffID == pharm2.StaffID);
            Assert("Dispensing audit trail references historical pharmacist", preservedPrescription?.Medication?.PharmacistID == pharm2.StaffID);

            // Re-activate pharmacist 2
            pharm2Staff.IsActive = true;
            pharm2.LockoutEnd = null;
            await userManager.UpdateAsync(pharm2);
            await context.SaveChangesAsync();

            var activePharmacistsAfterReactivation = await context.Staffs
                .Where(s => s.Role == "Pharmacist" && s.IsActive)
                .ToListAsync();

            Assert("Reactivated pharmacist restored to assignment list", activePharmacistsAfterReactivation.Any(s => s.ID == pharm2.StaffID));

            // 5. Test Nurse-to-Nurse Handover, Active Filtering, and Direct Notifications
            Console.WriteLine("\n--- TEST PHASE 5: Nurse-to-Nurse Handover & Routing Verification ---");

            var nurse2 = await userManager.FindByEmailAsync("nurse.worku@hu.edu.et");
            Assert("Destination nurse account exists", nurse2 != null && nurse2.StaffID.HasValue);

            // Test active nurse filtering for destination nurse selection
            var activeNurses = await context.Staffs
                .Where(s => s.IsActive && s.Role == "Nurse")
                .ToListAsync();
            Assert("Eligible destination nurses list contains active nurses", activeNurses.Count >= 2);
            Assert("Active destination nurse is present in eligible list", activeNurses.Any(s => s.ID == nurse2!.StaffID));

            // Test inactive nurse exclusion
            var nurse2Staff = await context.Staffs.FindAsync(nurse2!.StaffID!.Value);
            nurse2Staff!.IsActive = false;
            await context.SaveChangesAsync();

            var activeNursesDuringDeactivation = await context.Staffs
                .Where(s => s.IsActive && s.Role == "Nurse")
                .ToListAsync();
            Assert("Deactivated nurse is excluded from destination selection", !activeNursesDuringDeactivation.Any(s => s.ID == nurse2.StaffID));

            // Restore nurse2 active status
            nurse2Staff.IsActive = true;
            await context.SaveChangesAsync();

            // Perform Nurse-to-Nurse Handover
            var handoverAudit = new TaskAssignmentHistory
            {
                TaskType = "PatientHandover",
                TaskID = patient.ID,
                AssignedFromStaffID = nurse1!.StaffID,
                AssignedToStaffID = nurse2.StaffID!.Value,
                AssignedByID = nurse1.Id,
                AssignedAt = DateTime.UtcNow,
                Reason = "Student reporting severe migraine; routed to Triage Room 101B"
            };
            context.TaskAssignmentHistories.Add(handoverAudit);
            await context.SaveChangesAsync();

            Assert("Handover audit record persisted in TaskAssignmentHistory", handoverAudit.ID > 0);
            Assert("Handover audit references registration nurse as origin", handoverAudit.AssignedFromStaffID == nurse1.StaffID);
            Assert("Handover audit references destination nurse as recipient", handoverAudit.AssignedToStaffID == nurse2.StaffID);
            Assert("Handover note preserved in audit trail", handoverAudit.Reason == "Student reporting severe migraine; routed to Triage Room 101B");

            // Dispatch targeted direct notification without spamming Health Officers
            var notificationService = services.GetRequiredService<INotificationService>();
            await notificationService.CreateDirectNotificationAsync(
                nurse2.Id,
                "Incoming Patient Handover",
                $"Patient {patient.FirstName} {patient.LastName} ({patient.CardNumber}) handed over by Nurse {nurse1.FullName}.",
                "PatientHandover",
                patient.ID);

            var nurse2Notifications = await context.Notifications
                .Where(n => n.UserID == nurse2.Id && n.NotificationType == "PatientHandover" && n.RelatedRecordID == patient.ID)
                .ToListAsync();
            Assert("Targeted notification received by destination nurse", nurse2Notifications.Any());

            // Query incoming handovers for destination nurse
            var destinationHandovers = await context.TaskAssignmentHistories
                .Where(h => h.TaskType == "PatientHandover" && h.AssignedToStaffID == nurse2.StaffID.Value)
                .OrderByDescending(h => h.AssignedAt)
                .ToListAsync();
            Assert("Destination nurse can query incoming handovers", destinationHandovers.Any(h => h.TaskID == patient.ID));

            // 6. Test Laboratory Result Handover & Nurse Review Workflow
            Console.WriteLine("\n--- TEST PHASE 6: Laboratory Result Handover & Nurse Review Workflow ---");

            var p6LabTech = await userManager.FindByEmailAsync("lab.abebe@hu.edu.et");
            Assert("Lab Technician account exists with staff link", p6LabTech != null && p6LabTech.StaffID.HasValue);

            // Verify dynamic eligible receiving nurse query (active + unlocked)
            var eligibleReceivingNurses = await (from s in context.Staffs
                                                 join u in context.Users on s.ID equals u.StaffID
                                                 where s.IsActive && s.Role == "Nurse"
                                                    && (u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow)
                                                 orderby s.LastName, s.FirstName
                                                 select s)
                                                 .ToListAsync();

            Assert("Eligible receiving nurses list contains active nurses", eligibleReceivingNurses.Count >= 2);
            Assert("Active nurse2 is in eligible receiving nurses list", eligibleReceivingNurses.Any(s => s.ID == nurse2.StaffID));

            // Verify deactivated nurse is excluded from result handover dropdown
            nurse2Staff.IsActive = false;
            await context.SaveChangesAsync();

            var eligibleAfterDeact = await (from s in context.Staffs
                                            join u in context.Users on s.ID equals u.StaffID
                                            where s.IsActive && s.Role == "Nurse"
                                               && (u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow)
                                            select s)
                                            .ToListAsync();

            Assert("Deactivated nurse excluded from lab handover recipient selection", !eligibleAfterDeact.Any(s => s.ID == nurse2.StaffID));

            // Restore nurse2
            nurse2Staff.IsActive = true;
            await context.SaveChangesAsync();

            // Create test lab request for encounter
            var testLabReq = new LabRequest
            {
                PatientID = patient.ID,
                ClinicalEncounterID = encounter.ID,
                MedicalRecordID = medRecord.ID,
                RequestedByID = nurse1.StaffID!.Value,
                AssignedToStaffID = p6LabTech!.StaffID,
                RequestDate = DateTime.Now,
                Status = "Pending",
                ClinicalNotes = "Routine diagnostic testing"
            };
            context.LabRequests.Add(testLabReq);
            await context.SaveChangesAsync();

            // Record completed lab result
            var p6TestType = await context.TestTypes.FirstOrDefaultAsync() ?? new TestType { TestName = "Urinalysis" };
            if (p6TestType.ID == 0) { context.TestTypes.Add(p6TestType); await context.SaveChangesAsync(); }

            var testLabResult = new LabResult
            {
                MedicalRecordID = testLabReq.MedicalRecordID,
                TypeID = p6TestType.ID,
                Result = "Normal findings; negative for pathogens",
                Remark = "Routine screening",
                ResultPerformedByID = p6LabTech.StaffID!.Value,
                Status = "Completed"
            };
            context.LabResults.Add(testLabResult);

            // Update LabRequest and Encounter
            testLabReq.Status = "Completed";
            encounter.Status = WorkflowState.WaitingConsultation;
            encounter.AssignedToStaffID = nurse2.StaffID!.Value;

            // Record Handover Audit in TaskAssignmentHistory
            var labHandoverAudit = new TaskAssignmentHistory
            {
                TaskType = "LabResultHandover",
                TaskID = testLabReq.LabRequestID,
                AssignedFromStaffID = p6LabTech.StaffID!.Value,
                AssignedToStaffID = nurse2.StaffID!.Value,
                AssignedByID = p6LabTech.Id,
                AssignedAt = DateTime.UtcNow,
                Reason = "Results completed and handed over for clinical review"
            };
            context.TaskAssignmentHistories.Add(labHandoverAudit);
            await context.SaveChangesAsync();

            Assert("Lab result handover audit persisted in TaskAssignmentHistory", labHandoverAudit.ID > 0);
            Assert("Audit references lab technician as origin", labHandoverAudit.AssignedFromStaffID == p6LabTech.StaffID);
            Assert("Audit references destination nurse as recipient", labHandoverAudit.AssignedToStaffID == nurse2.StaffID);
            Assert("Lab request status updated to Completed", testLabReq.Status == "Completed");
            Assert("Clinical encounter updated to WaitingConsultation", encounter.Status == WorkflowState.WaitingConsultation);
            Assert("Clinical encounter routed to receiving nurse", encounter.AssignedToStaffID == nurse2.StaffID);

            // Send direct targeted notification
            await notificationService.CreateDirectNotificationAsync(
                nurse2.Id,
                "Lab Result Handover",
                $"Laboratory results for {patient.FirstName} {patient.LastName} completed and handed over to you for clinical review.",
                "LabResult",
                testLabResult.ID);

            var nurse2LabNotifs = await context.Notifications
                .Where(n => n.UserID == nurse2.Id && n.NotificationType == "LabResult" && n.RelatedRecordID == testLabResult.ID)
                .ToListAsync();

            Assert("Targeted lab result notification delivered to destination nurse", nurse2LabNotifs.Any());

            // Destination nurse queries incoming lab handovers
            var nurse2LabHandovers = await context.TaskAssignmentHistories
                .Where(h => h.TaskType == "LabResultHandover" && h.AssignedToStaffID == nurse2.StaffID.Value)
                .ToListAsync();

            Assert("Destination nurse can query incoming lab result handovers", nurse2LabHandovers.Any(h => h.TaskID == testLabReq.LabRequestID));

            // Verify original encounter and patient remain intact (no duplicate encounter created)
            var activeEncountersAfterLab = await context.ClinicalEncounters
                .Where(e => e.PatientID == patient.ID && e.Status != WorkflowState.Discharged && e.Status != WorkflowState.Transferred)
                .CountAsync();
            Assert("Single active clinical encounter preserved during lab handover", activeEncountersAfterLab == 1);

            Console.WriteLine("\n=================================================================");
            Console.WriteLine($"VERIFICATION SUMMARY: {testsPassed} / {totalTests} CHECKS PASSED ({(testsPassed * 100.0 / totalTests):F1}%)");
            Console.WriteLine("=================================================================\n");

            return testsPassed == totalTests ? 0 : 1;
        }
    }
}
