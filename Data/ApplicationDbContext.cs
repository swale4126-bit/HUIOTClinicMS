using System.Security.Claims;
using HUIOTClinicMS.Models;
using HUIOTClinicMS.Models.Interfaces;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HUIOTClinicMS.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor httpContextAccessor)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            foreach (var entry in ChangeTracker.Entries<IAuditable>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.CreatedBy = userId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedBy = userId;
                }
            }

            return await base.SaveChangesAsync(cancellationToken);
        }

        public DbSet<Department> Departments { get; set; }
        public DbSet<DormBlock> DormBlocks { get; set; }
        public DbSet<TestType> TestTypes { get; set; }
        public DbSet<Staff> Staffs { get; set; }
        public DbSet<Patient> Patients { get; set; }
        public DbSet<EmergencyContact> EmergencyContacts { get; set; }
        public DbSet<MedicalRecord> MedicalRecords { get; set; }
        public DbSet<LabRequest> LabRequests { get; set; }
        public DbSet<LabResult> LabResults { get; set; }
        public DbSet<Prescription> Prescriptions { get; set; }
        public DbSet<Medication> Medications { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ServicePoint> ServicePoints { get; set; }
        public DbSet<Queue> Queues { get; set; }
        public DbSet<ServiceLog> ServiceLogs { get; set; }
        
        public DbSet<ClinicalEncounter> ClinicalEncounters { get; set; }
        public DbSet<TriageAssessment> TriageAssessments { get; set; }
        public DbSet<ReferralRecord> ReferralRecords { get; set; }
        public DbSet<TaskAssignmentHistory> TaskAssignmentHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Fix decimal warning for Medication Amount
            modelBuilder.Entity<Medication>()
                .Property(m => m.Amount)
                .HasColumnType("decimal(18,2)");

            // Fix multiple cascade paths for LabRequest -> Patient
            modelBuilder.Entity<LabRequest>()
                .HasOne(l => l.Patient)
                .WithMany()
                .HasForeignKey(l => l.PatientID)
                .OnDelete(DeleteBehavior.Restrict);
                
            modelBuilder.Entity<LabRequest>()
                .HasOne(l => l.RequestedBy)
                .WithMany()
                .HasForeignKey(l => l.RequestedByID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MedicalRecord>()
                .HasOne(m => m.CreatedByStaff)
                .WithMany()
                .HasForeignKey(m => m.CreatedByID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<LabResult>()
                .HasOne(l => l.ResultPerformedBy)
                .WithMany()
                .HasForeignKey(l => l.ResultPerformedByID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Prescription>()
                .HasOne(p => p.PrescribedBy)
                .WithMany()
                .HasForeignKey(p => p.PrescribedByID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Prescription>()
                .HasOne(p => p.LabResult)
                .WithMany()
                .HasForeignKey(p => p.LabResultID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Medication>()
                .HasOne(m => m.Pharmacist)
                .WithMany()
                .HasForeignKey(m => m.PharmacistID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ClinicalEncounter>()
                .HasOne(e => e.AssignedToStaff)
                .WithMany()
                .HasForeignKey(e => e.AssignedToStaffID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<LabRequest>()
                .HasOne(l => l.AssignedToStaff)
                .WithMany()
                .HasForeignKey(l => l.AssignedToStaffID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Prescription>()
                .HasOne(p => p.AssignedToStaff)
                .WithMany()
                .HasForeignKey(p => p.AssignedToStaffID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TaskAssignmentHistory>()
                .HasOne(t => t.AssignedFromStaff)
                .WithMany()
                .HasForeignKey(t => t.AssignedFromStaffID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TaskAssignmentHistory>()
                .HasOne(t => t.AssignedToStaff)
                .WithMany()
                .HasForeignKey(t => t.AssignedToStaffID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TaskAssignmentHistory>()
                .HasOne(t => t.AssignedByUser)
                .WithMany()
                .HasForeignKey(t => t.AssignedByID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<LabRequest>()
                .HasMany(l => l.TestTypes)
                .WithMany(t => t.LabRequests)
                .UsingEntity<Dictionary<string, object>>(
                    "LabRequestTestTypes",
                    j => j.HasOne<TestType>()
                          .WithMany()
                          .HasForeignKey("TestTypeID")
                          .HasConstraintName("FK_LabRequestTestTypes_TestTypes_TestTypeID"),
                    j => j.HasOne<LabRequest>()
                          .WithMany()
                          .HasForeignKey("LabRequestID")
                          .HasConstraintName("FK_LabRequestTestTypes_LabRequests_LabRequestID"),
                    j =>
                    {
                        j.HasKey("LabRequestID", "TestTypeID");
                        j.ToTable("LabRequestTestTypes");
                    });
        }
    }
}