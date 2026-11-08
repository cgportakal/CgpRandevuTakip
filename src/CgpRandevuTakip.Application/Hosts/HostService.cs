using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Hosts;

public sealed class HostService : IHostService
{
    private const int MinDisplayNameLength = 3;
    private const int MaxDisplayNameLength = 100;

    private readonly IUserRepository _users;
    private readonly IAppointmentRepository _appointments;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditLogger _audit;
    private readonly ICurrentUser _currentUser;

    public HostService(
        IUserRepository users,
        IAppointmentRepository appointments,
        IPasswordHasher hasher,
        IAuditLogger audit,
        ICurrentUser currentUser)
    {
        _users = users;
        _appointments = appointments;
        _hasher = hasher;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<HostDto>> GetAllAsync()
    {
        var users = await _users.GetAllAsync();
        var usage = await _appointments.GetHostUsageCountsAsync();

        return users
            .OrderBy(x => x.Role)
            .ThenBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => ToDto(x, usage))
            .ToList();
    }

    public async Task<IReadOnlyList<HostDto>> GetSelectableHostsAsync()
    {
        var users = await _users.GetAllAsync();
        return users
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => ToDto(x, new Dictionary<string, int>()))
            .ToList();
    }

    public async Task<OperationResult> CreateAsync(HostCreateInput input)
    {
        if (!IsAdmin())
            return OperationResult.Fail("Görüşmeci ekleme yetkisi yalnızca yöneticidedir.");

        var displayName = input.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length < MinDisplayNameLength)
            return OperationResult.Fail(
                $"Ad soyad en az {MinDisplayNameLength} karakter olmalıdır.");

        if (displayName.Length > MaxDisplayNameLength)
            return OperationResult.Fail(
                $"Ad soyad en fazla {MaxDisplayNameLength} karakter olabilir.");

        string username;
        string passwordHash;
        string passwordSalt;
        var iterations = 1;

        if (input.LoginEnabled)
        {
            username = UsernamePolicy.Normalize(input.Username);
            var usernameError = UsernamePolicy.Validate(username);
            if (usernameError is not null)
                return OperationResult.Fail(usernameError);

            if (await _users.FindByUsernameAsync(username) is not null)
                return OperationResult.Fail("Bu kullanıcı adı zaten kullanılıyor.");

            var passwordError = PasswordPolicy.Validate(input.Password);
            if (passwordError is not null)
                return OperationResult.Fail(passwordError);

            if (!string.Equals(input.Password, input.ConfirmPassword, StringComparison.Ordinal))
                return OperationResult.Fail("Şifreler birbiriyle eşleşmiyor.");

            var hash = _hasher.Hash(input.Password!);
            passwordHash = hash.Hash;
            passwordSalt = hash.Salt;
            iterations = hash.Iterations;
        }
        else
        {
            username = await GenerateUsernameAsync(displayName);
            passwordHash = "!";
            passwordSalt = "!";
        }

        var user = new User
        {
            Username = username,
            DisplayName = displayName,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            PasswordIterations = iterations,
            Role = UserRole.Host,
            IsActive = true,
            IsLoginEnabled = input.LoginEnabled
        };

        await _users.AddAsync(user);
        await _audit.LogAsync(
            AuditAction.UserCreated,
            _currentUser.UserId,
            nameof(User),
            user.Id);

        return OperationResult.Ok();
    }

    public async Task<OperationResult> UpdateAsync(string id, HostUpdateInput input)
    {
        if (!IsAdmin())
            return OperationResult.Fail("Görüşmeci düzenleme yetkisi yalnızca yöneticidedir.");

        var user = await _users.GetByIdAsync(id);
        if (user is null)
            return OperationResult.Fail("Kullanıcı bulunamadı.");

        var displayName = input.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length < MinDisplayNameLength)
            return OperationResult.Fail(
                $"Ad soyad en az {MinDisplayNameLength} karakter olmalıdır.");

        if (displayName.Length > MaxDisplayNameLength)
            return OperationResult.Fail(
                $"Ad soyad en fazla {MaxDisplayNameLength} karakter olabilir.");

        if (!input.IsActive && user.Id == _currentUser.UserId)
            return OperationResult.Fail("Kendi hesabınızı pasifleştiremezsiniz.");

        if (!input.IsActive
            && user.Role == UserRole.Admin
            && user.IsActive
            && await _users.CountActiveAdminsAsync() <= 1)
        {
            return OperationResult.Fail("Son aktif yönetici pasifleştirilemez.");
        }

        var deactivating = user.IsActive && !input.IsActive;

        user.DisplayName = displayName;
        user.IsActive = input.IsActive;
        await _users.SaveAsync(user);

        await _audit.LogAsync(
            deactivating ? AuditAction.UserDeactivated : AuditAction.UserUpdated,
            _currentUser.UserId,
            nameof(User),
            user.Id);

        return OperationResult.Ok();
    }

    private bool IsAdmin() => _currentUser.IsInRole(UserRole.Admin);

    private async Task<string> GenerateUsernameAsync(string displayName)
    {
        var slug = new string(displayName
            .ToLowerInvariant()
            .Select(ToAsciiLetterOrDigit)
            .Where(c => c != '\0')
            .Take(12)
            .ToArray());

        if (slug.Length == 0)
            slug = "host";

        var candidate = $"host-{slug}";
        var suffix = 2;

        while (await _users.FindByUsernameAsync(candidate) is not null)
        {
            candidate = $"host-{slug}-{suffix++}";
        }

        return candidate.Length <= UsernamePolicy.MaxLength
            ? candidate
            : candidate[..UsernamePolicy.MaxLength];
    }

    private static char ToAsciiLetterOrDigit(char c) => c switch
    {
        'ç' => 'c',
        'ğ' => 'g',
        'ı' => 'i',
        'ö' => 'o',
        'ş' => 's',
        'ü' => 'u',
        _ => char.IsAsciiLetterOrDigit(c) ? c : '\0'
    };

    private static HostDto ToDto(User user, IReadOnlyDictionary<string, int> usage)
        => new(
            user.Id,
            user.DisplayName,
            user.Username,
            user.Role,
            user.IsLoginEnabled,
            user.IsActive,
            usage.TryGetValue(user.Id, out var count) ? count : 0);
}
