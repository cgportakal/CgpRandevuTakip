using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Users;

public sealed class UserService : IUserService
{
    private const int MinDisplayNameLength = 3;
    private const int MaxDisplayNameLength = 100;

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public UserService(
        IUserRepository users,
        IPasswordHasher hasher,
        IAuditLogger audit,
        IClock clock,
        ICurrentUser currentUser)
    {
        _users = users;
        _hasher = hasher;
        _audit = audit;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<UserDto>> GetAllAsync()
    {
        if (!IsAdmin())
            return Array.Empty<UserDto>();

        var users = await _users.GetAllAsync();
        return users
            .OrderBy(x => x.Role)
            .ThenBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(ToDto)
            .ToList();
    }

    public async Task<OperationResult> CreateAsync(UserCreateInput input)
    {
        if (!IsAdmin())
            return OperationResult.Fail("Kullanıcı ekleme yetkisi yalnızca yöneticidedir.");

        var displayName = NormalizeDisplayName(input.DisplayName);
        var displayNameError = ValidateDisplayName(displayName);
        if (displayNameError is not null)
            return OperationResult.Fail(displayNameError);

        if (!Enum.IsDefined(input.Role))
            return OperationResult.Fail("Geçersiz rol seçildi.");

        if (input.Role != UserRole.Host && !input.IsLoginEnabled)
            return OperationResult.Fail("Yönetici ve sekreter giriş yapabilmelidir.");

        var username = UsernamePolicy.Normalize(input.Username);
        var usernameError = UsernamePolicy.Validate(username);
        if (usernameError is not null)
            return OperationResult.Fail(usernameError);

        if (await _users.FindByUsernameAsync(username) is not null)
            return OperationResult.Fail("Bu kullanıcı adı zaten kullanılıyor.");

        var emailValidation = await ValidateEmailAsync(input.Email, userId: null);
        if (emailValidation.Error is not null)
            return OperationResult.Fail(emailValidation.Error);

        var email = emailValidation.Email;

        string passwordHash;
        string passwordSalt;
        var iterations = 1;

        if (input.IsLoginEnabled)
        {
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
            passwordHash = "!";
            passwordSalt = "!";
        }

        var user = new User
        {
            Username = username,
            DisplayName = displayName,
            Email = email,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            PasswordIterations = iterations,
            Role = input.Role,
            IsActive = true,
            IsLoginEnabled = input.IsLoginEnabled
        };

        await _users.AddAsync(user);
        await _audit.LogAsync(
            AuditAction.UserCreated,
            _currentUser.UserId,
            nameof(User),
            user.Id);

        return OperationResult.Ok();
    }

    public async Task<OperationResult> UpdateAsync(string userId, UserUpdateInput input)
    {
        if (!IsAdmin())
            return OperationResult.Fail("Kullanıcı düzenleme yetkisi yalnızca yöneticidedir.");

        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            return OperationResult.Fail("Kullanıcı bulunamadı.");

        var displayName = NormalizeDisplayName(input.DisplayName);
        var displayNameError = ValidateDisplayName(displayName);
        if (displayNameError is not null)
            return OperationResult.Fail(displayNameError);

        if (!Enum.IsDefined(input.Role))
            return OperationResult.Fail("Geçersiz rol seçildi.");

        if (input.Role != UserRole.Host && !input.IsLoginEnabled)
            return OperationResult.Fail("Yönetici ve sekreter giriş yapabilmelidir.");

        var emailValidation = await ValidateEmailAsync(input.Email, user.Id);
        if (emailValidation.Error is not null)
            return OperationResult.Fail(emailValidation.Error);

        var email = emailValidation.Email;

        if (user.Id == _currentUser.UserId && !input.IsActive)
            return OperationResult.Fail("Kendi hesabınızı pasifleştiremezsiniz.");

        if (input.IsLoginEnabled
            && !user.IsLoginEnabled
            && string.Equals(user.PasswordHash, "!", StringComparison.Ordinal))
        {
            return OperationResult.Fail(
                "Girişi açmadan önce bu kullanıcı için bir şifre belirleyin.");
        }

        var keepsAdminAccess = input.IsActive
                               && input.IsLoginEnabled
                               && input.Role == UserRole.Admin;

        var losesAdminAccess = user.IsActive
                               && user.IsLoginEnabled
                               && user.Role == UserRole.Admin
                               && !keepsAdminAccess;

        if (losesAdminAccess && await _users.CountActiveLoginAdminsAsync() <= 1)
            return OperationResult.Fail("Son aktif yönetici pasifleştirilemez veya rolü değiştirilemez.");

        var deactivating = user.IsActive && !input.IsActive;

        user.DisplayName = displayName;
        user.Email = email;
        user.Role = input.Role;
        user.IsActive = input.IsActive;
        user.IsLoginEnabled = input.IsLoginEnabled;
        if (!input.IsActive || !input.IsLoginEnabled)
        {
            user.RememberTokenHash = null;
            user.RememberTokenExpiresAtUtc = null;
        }
        await _users.SaveAsync(user);

        await _audit.LogAsync(
            deactivating ? AuditAction.UserDeactivated : AuditAction.UserUpdated,
            _currentUser.UserId,
            nameof(User),
            user.Id);

        return OperationResult.Ok();
    }

    public async Task<OperationResult> ResetPasswordAsync(
        string userId,
        UserResetPasswordInput input)
    {
        if (!IsAdmin())
            return OperationResult.Fail("Şifre sıfırlama yetkisi yalnızca yöneticidedir.");

        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            return OperationResult.Fail("Kullanıcı bulunamadı.");

        var passwordError = PasswordPolicy.Validate(input.Password);
        if (passwordError is not null)
            return OperationResult.Fail(passwordError);

        if (!string.Equals(input.Password, input.ConfirmPassword, StringComparison.Ordinal))
            return OperationResult.Fail("Şifreler birbiriyle eşleşmiyor.");

        var hash = _hasher.Hash(input.Password);
        user.PasswordHash = hash.Hash;
        user.PasswordSalt = hash.Salt;
        user.PasswordIterations = hash.Iterations;
        user.FailedLoginCount = 0;
        user.LockoutUntilUtc = null;
        user.RememberTokenHash = null;
        user.RememberTokenExpiresAtUtc = null;
        user.PasswordResetCodeHash = null;
        user.PasswordResetCodeSalt = null;
        user.PasswordResetCodeIterations = 0;
        user.PasswordResetExpiresAtUtc = null;
        await _users.SaveAsync(user);

        await _audit.LogAsync(
            AuditAction.PasswordChanged,
            _currentUser.UserId,
            nameof(User),
            user.Id);

        return OperationResult.Ok();
    }

    private bool IsAdmin() => _currentUser.IsInRole(UserRole.Admin);

    private static string NormalizeDisplayName(string? value)
        => value?.Trim() ?? string.Empty;

    private static string? ValidateDisplayName(string displayName)
    {
        if (displayName.Length < MinDisplayNameLength)
            return $"Ad soyad en az {MinDisplayNameLength} karakter olmalıdır.";

        if (displayName.Length > MaxDisplayNameLength)
            return $"Ad soyad en fazla {MaxDisplayNameLength} karakter olabilir.";

        return null;
    }

    private async Task<(string? Error, string? Email)> ValidateEmailAsync(
        string? email,
        string? userId)
    {
        if (string.IsNullOrWhiteSpace(email))
            return (null, null);

        if (!EmailAddressPolicy.TryNormalize(email, out var value))
            return ("E-posta adresi geçerli değil.", null);

        var existing = await _users.FindByEmailAsync(value);
        if (existing is not null && existing.Id != userId)
            return ("Bu e-posta adresi başka bir kullanıcıda kayıtlı.", null);

        return (null, value);
    }

    private UserDto ToDto(User user)
        => new(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Role,
            user.IsActive,
            user.IsLoginEnabled,
            user.LastLoginAtUtc is null
                ? null
                : _clock.ToLocal(user.LastLoginAtUtc.Value),
            _clock.ToLocal(user.CreatedAtUtc),
            user.Email);
}
