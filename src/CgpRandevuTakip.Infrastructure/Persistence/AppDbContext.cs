using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<ReminderDispatch> ReminderDispatches => Set<ReminderDispatch>();
    public DbSet<AppointmentOutcome> AppointmentOutcomes => Set<AppointmentOutcome>();
    public DbSet<Recipient> Recipients => Set<Recipient>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<WhatsAppShareLog> WhatsAppShareLogs => Set<WhatsAppShareLog>();
    public DbSet<BackupRecord> BackupRecords => Set<BackupRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Username).HasMaxLength(32).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PasswordHash).IsRequired();
            entity.Property(x => x.PasswordSalt).IsRequired();
            entity.Property(x => x.Role).HasConversion<int>();
            entity.HasIndex(x => x.Username).IsUnique();
        });

        modelBuilder.Entity<Person>(entity =>
        {
            entity.ToTable("Persons");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PhoneE164).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.Organization).HasMaxLength(200);
            entity.HasIndex(x => x.PhoneE164);
            entity.HasIndex(x => new { x.LastName, x.FirstName });
            entity.Ignore(x => x.FullName);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(50).UseCollation("NOCASE").IsRequired();
            entity.Property(x => x.ColorHex).HasMaxLength(9).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.ToTable("Appointments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Description).HasMaxLength(4000);
            entity.Property(x => x.StudentFirstName).HasMaxLength(100);
            entity.Property(x => x.StudentLastName).HasMaxLength(100);
            entity.Property(x => x.Method).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.ClosedByUserId).HasMaxLength(100);

            entity.HasIndex(x => x.StartsAtUtc);
            entity.HasIndex(x => x.PersonId);
            entity.HasIndex(x => new { x.HostUserId, x.StartsAtUtc });
            entity.HasIndex(x => x.CategoryId);

            entity.HasOne(x => x.Person)
                  .WithMany(x => x.Appointments)
                  .HasForeignKey(x => x.PersonId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.HostUser)
                  .WithMany(x => x.HostedAppointments)
                  .HasForeignKey(x => x.HostUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Category)
                  .WithMany(x => x.Appointments)
                  .HasForeignKey(x => x.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Outcome)
                  .WithOne(x => x.Appointment)
                  .HasForeignKey<AppointmentOutcome>(x => x.AppointmentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Reminders)
                  .WithOne(x => x.Appointment)
                  .HasForeignKey(x => x.AppointmentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Ignore(x => x.EndsAtUtc);
            entity.Ignore(x => x.StudentFullName);
        });

        modelBuilder.Entity<ReminderDispatch>(entity =>
        {
            entity.ToTable("ReminderDispatches");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.State).HasConversion<int>();
            entity.Property(x => x.LastAction).HasConversion<int>();
            entity.Property(x => x.DedupKey).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.DedupKey).IsUnique();
            entity.HasIndex(x => new { x.State, x.DueAtUtc });
            entity.HasIndex(x => x.AppointmentId);
        });

        modelBuilder.Entity<AppointmentOutcome>(entity =>
        {
            entity.ToTable("AppointmentOutcomes");
            entity.HasKey(x => x.AppointmentId);
            entity.Property(x => x.Attendance).HasConversion<int>();
            entity.Property(x => x.Summary).HasMaxLength(4000);
            entity.Property(x => x.PostNotes).HasMaxLength(4000);
        });

        modelBuilder.Entity<Recipient>(entity =>
        {
            entity.ToTable("Recipients");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PhoneE164).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Tag).HasMaxLength(50);
            entity.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<Setting>(entity =>
        {
            entity.ToTable("Settings");
            entity.HasKey(x => x.Key);
            entity.Property(x => x.Key).HasMaxLength(100);
            entity.Property(x => x.Value).HasMaxLength(4000);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasConversion<int>();
            entity.Property(x => x.EntityType).HasMaxLength(100);
            entity.Property(x => x.EntityId).HasMaxLength(100);
            entity.HasIndex(x => x.CreatedAtUtc);
        });

        modelBuilder.Entity<WhatsAppShareLog>(entity =>
        {
            entity.ToTable("WhatsAppShareLogs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Method).HasConversion<int>();
            entity.Property(x => x.Result).HasConversion<int>();
            entity.Property(x => x.RecipientIdsJson).HasMaxLength(2000);
            entity.Property(x => x.ImagePath).HasMaxLength(500);
            entity.HasIndex(x => x.ShareDate);
            entity.HasIndex(x => x.AppointmentId);
        });

        modelBuilder.Entity<BackupRecord>(entity =>
        {
            entity.ToTable("BackupRecords");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Path).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Provider).HasConversion<int>();
            entity.HasIndex(x => x.CreatedAtUtc);
        });
    }
}
