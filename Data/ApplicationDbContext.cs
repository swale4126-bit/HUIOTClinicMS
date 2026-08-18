using HUIOTClinicMS.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HUIOTClinicMS.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
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
      


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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