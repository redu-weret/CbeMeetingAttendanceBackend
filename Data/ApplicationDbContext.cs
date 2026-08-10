using CbeMeetingAttendance.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CbeMeetingAttendance.Data
{
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Employee> Employees => Set<Employee>();
        public DbSet<Attendance> Attendances => Set<Attendance>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Employee entity
            modelBuilder.Entity<Employee>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Unique index on custom EmployeeId
                entity.HasIndex(e => e.EmployeeId)
                      .IsUnique();

                entity.Property(e => e.FullName).IsRequired();
                entity.Property(e => e.Division).IsRequired();
                entity.Property(e => e.JobTitle).IsRequired();
                entity.Property(e => e.Department).IsRequired();
                entity.Property(e => e.PhoneNumber).IsRequired();
            });

            // Configure Attendance entity
            modelBuilder.Entity<Attendance>(entity =>
            {
                entity.HasKey(a => a.Id);

                entity.HasOne(a => a.Employee)
                      .WithMany(e => e.Attendances)
                      .HasForeignKey(a => a.EmployeeId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Composite Unique Index: {EmployeeId, MeetingDate, Session}
                entity.HasIndex(a => new { a.EmployeeId, a.MeetingDate, a.Session })
                      .IsUnique();

                entity.Property(a => a.Session)
                      .HasConversion<string>();
            });

            // Configure User entity (IdentityUser + Employee FK)
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasOne(u => u.Employee)
                      .WithMany()
                      .HasForeignKey(u => u.EmployeeId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(u => u.EmployeeId)
                      .IsUnique();

                // Profile picture: byte[] maps to varbinary(max) by default;
                // cap the relative path length.
                entity.Property(u => u.ProfilePicturePath)
                      .HasMaxLength(255);
            });

        }
    }
}
