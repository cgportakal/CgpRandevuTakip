using CgpRandevuTakip.Domain.Common;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Domain.Entities;

public class User : EntityBase
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public int PasswordIterations { get; set; } = 210_000;
    public UserRole Role { get; set; } = UserRole.Host;
    public bool IsActive { get; set; } = true;

    /// <summary>Giriş yapmasına gerek olmayan kaynak kişiler (host) için false olabilir.</summary>
    public bool IsLoginEnabled { get; set; } = true;

    public int FailedLoginCount { get; set; }
    public DateTime? LockoutUntilUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }

    public ICollection<Appointment> HostedAppointments { get; set; } = new List<Appointment>();

    public bool IsLockedOut(DateTime nowUtc)
        => LockoutUntilUtc is not null && LockoutUntilUtc > nowUtc;
}
