using ClinicDesk.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClinicDesk.Api.Infrastructure;

public class ClinicDeskDbContext : DbContext
{
    public ClinicDeskDbContext(DbContextOptions<ClinicDeskDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Specialty> Specialties => Set<Specialty>();
    public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();
    public DbSet<AppointmentSlot> AppointmentSlots => Set<AppointmentSlot>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Patient>(e =>
        {
            e.HasIndex(p => p.UserId).IsUnique();
            e.HasOne(p => p.User).WithOne(u => u.Patient)
                .HasForeignKey<Patient>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Specialty>(e =>
        {
            e.HasIndex(s => s.Name).IsUnique();
            e.Property(s => s.Name).HasMaxLength(120).IsRequired();
        });

        modelBuilder.Entity<Doctor>(e =>
        {
            e.HasIndex(d => d.UserId).IsUnique();
            e.HasOne(d => d.User).WithOne(u => u.Doctor)
                .HasForeignKey<Doctor>(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.Specialty).WithMany(s => s.Doctors)
                .HasForeignKey(d => d.SpecialtyId)
                .OnDelete(DeleteBehavior.Restrict);
            e.Property(d => d.LicenseNumber).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<DoctorSchedule>(e =>
        {
            e.HasOne(s => s.Doctor).WithMany(d => d.Schedules)
                .HasForeignKey(s => s.DoctorId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.DoctorId, s.DayOfWeek, s.EffectiveFrom });
        });

        modelBuilder.Entity<AppointmentSlot>(e =>
        {
            e.HasOne(s => s.Doctor).WithMany(d => d.Slots)
                .HasForeignKey(s => s.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.DoctorSchedule).WithMany()
                .HasForeignKey(s => s.DoctorScheduleId)
                .OnDelete(DeleteBehavior.SetNull);
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.Version).IsConcurrencyToken();
            // Un doctor no puede tener dos slots que arranquen exactamente al mismo tiempo.
            e.HasIndex(s => new { s.DoctorId, s.StartsAtUtc }).IsUnique();
        });

        modelBuilder.Entity<Appointment>(e =>
        {
            e.HasOne(a => a.Slot).WithMany(s => s.Appointments)
                .HasForeignKey(a => a.SlotId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Patient).WithMany(p => p.Appointments)
                .HasForeignKey(a => a.PatientId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Doctor).WithMany()
                .HasForeignKey(a => a.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.ReasonForVisit).HasMaxLength(500);
            e.Property(a => a.Version).IsConcurrencyToken();
            e.HasIndex(a => new { a.PatientId, a.Status });
            e.HasIndex(a => new { a.DoctorId, a.Status });
        });

        modelBuilder.Entity<WaitlistEntry>(e =>
        {
            e.HasOne(w => w.Slot).WithMany(s => s.WaitlistEntries)
                .HasForeignKey(w => w.SlotId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(w => w.Patient).WithMany()
                .HasForeignKey(w => w.PatientId)
                .OnDelete(DeleteBehavior.Restrict);
            e.Property(w => w.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(w => new { w.SlotId, w.Status, w.CreatedAtUtc });
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        BumpConcurrencyTokens();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        BumpConcurrencyTokens();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void BumpConcurrencyTokens()
    {
        foreach (var entry in ChangeTracker.Entries<Domain.Entities.IConcurrencyAware>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.Version = Guid.NewGuid();
            }
        }
    }
}
