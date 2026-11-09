using System.Security.Cryptography;
using System.Text;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Auth;

/// <summary>
/// Yerel kimlik doğrulama. Parola hiçbir zaman düz metin olarak saklanmaz veya loglanmaz.
/// </summary>
public sealed class AuthService : IAuthService
{
    public const int MaxFailedAttempts = 5;
    public const int LockoutMinutes = 15;
    private const int RememberTokenDays = 30;

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;
    private readonly IAuditLogger _audit;
    private readonly CurrentUser _currentUser;

    public AuthService(
        IUserRepository users,
        IPasswordHasher hasher,
        IClock clock,
        IAuditLogger audit,
        CurrentUser currentUser)
    {
        _users = users;
        _hasher = hasher;
        _clock = clock;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<bool> IsFirstRunAsync()
        => await _users.CountAsync() == 0;

    public async Task<SetupResult> CreateFirstAdminAsync(SetupRequest request)
    {
        if (await _users.CountAsync() > 0)
            return SetupResult.Fail("İlk kurulum daha önce tamamlanmış.");

        var username = UsernamePolicy.Normalize(request.Username);
        var usernameError = UsernamePolicy.Validate(username);
        if (usernameError is not null)
            return SetupResult.Fail(usernameError);

        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length < 3)
            return SetupResult.Fail("Ad soyad en az 3 karakter olmalıdır.");
        if (displayName.Length > 100)
            return SetupResult.Fail("Ad soyad en fazla 100 karakter olabilir.");

        var passwordError = PasswordPolicy.Validate(request.Password);
        if (passwordError is not null)
            return SetupResult.Fail(passwordError);

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            return SetupResult.Fail("Şifreler birbiriyle eşleşmiyor.");

        var hash = _hasher.Hash(request.Password);
        var user = new User
        {
            Username = username,
            DisplayName = displayName,
            PasswordHash = hash.Hash,
            PasswordSalt = hash.Salt,
            PasswordIterations = hash.Iterations,
            Role = UserRole.Admin,
            IsActive = true,
            IsLoginEnabled = true,
            LastLoginAtUtc = _clock.UtcNow
        };

        await _users.AddAsync(user);
        _currentUser.SignIn(ToSession(user));

        await _audit.LogAsync(
            AuditAction.FirstRunCompleted,
            user.Id,
            nameof(User),
            user.Id);

        return SetupResult.Ok(ToSession(user));
    }

    public async Task<LoginResult> LoginAsync(string username, string password)
    {
        var normalized = UsernamePolicy.Normalize(username);
        if (normalized.Length == 0 || string.IsNullOrEmpty(password))
            return LoginResult.Fail("Kullanıcı adı veya şifre hatalı.");

        var user = await _users.FindByUsernameAsync(normalized);
        if (user is null)
        {
            await _audit.LogAsync(AuditAction.LoginFailed);
            return LoginResult.Fail("Kullanıcı adı veya şifre hatalı.");
        }

        if (!user.IsActive || !user.IsLoginEnabled)
        {
            await _audit.LogAsync(AuditAction.LoginFailed, user.Id);
            return LoginResult.Fail("Kullanıcı adı veya şifre hatalı.");
        }

        var now = _clock.UtcNow;

        if (user.IsLockedOut(now))
        {
            var remaining = RemainingLockoutMinutes(user, now);
            await _audit.LogAsync(AuditAction.LoginFailed, user.Id);
            return LoginResult.Fail(
                $"Hesap geçici olarak kilitli. {remaining} dakika sonra tekrar deneyin.",
                remaining);
        }

        if (!_hasher.Verify(password, user.PasswordHash, user.PasswordSalt, user.PasswordIterations))
        {
            user.FailedLoginCount++;

            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.FailedLoginCount = 0;
                user.LockoutUntilUtc = now.AddMinutes(LockoutMinutes);
                await _users.SaveAsync(user);
                await _audit.LogAsync(AuditAction.LoginFailed, user.Id);

                return LoginResult.Fail(
                    $"5 hatalı deneme nedeniyle hesap {LockoutMinutes} dakika kilitlendi.",
                    LockoutMinutes);
            }

            var remainingAttempts = MaxFailedAttempts - user.FailedLoginCount;
            await _users.SaveAsync(user);
            await _audit.LogAsync(AuditAction.LoginFailed, user.Id);

            return LoginResult.Fail(
                $"Kullanıcı adı veya şifre hatalı. Kalan deneme hakkı: {remainingAttempts}.");
        }

        user.FailedLoginCount = 0;
        user.LockoutUntilUtc = null;
        user.LastLoginAtUtc = now;
        await _users.SaveAsync(user);

        var session = ToSession(user);
        _currentUser.SignIn(session);
        await _audit.LogAsync(AuditAction.LoginSucceeded, user.Id);

        return LoginResult.Ok(session);
    }

    public async Task<string?> CreateRememberTokenAsync()
    {
        if (_currentUser.UserId is null)
            return null;

        var user = await _users.GetByIdAsync(_currentUser.UserId);
        if (user is null || !user.IsActive || !user.IsLoginEnabled)
            return null;

        var token = CreateToken();
        user.RememberTokenHash = HashToken(token);
        user.RememberTokenExpiresAtUtc = _clock.UtcNow.AddDays(RememberTokenDays);
        await _users.SaveAsync(user);

        return token;
    }

    public async Task<LoginResult> LoginWithRememberTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return LoginResult.Fail("Oturum bilgisi geçersiz.");

        var hash = HashToken(token.Trim());
        var user = await _users.FindByRememberTokenHashAsync(hash);
        if (user is null)
            return LoginResult.Fail("Oturum bilgisi geçersiz.");

        var now = _clock.UtcNow;
        if (!user.IsActive
            || !user.IsLoginEnabled
            || user.RememberTokenExpiresAtUtc is null
            || user.RememberTokenExpiresAtUtc <= now)
        {
            user.RememberTokenHash = null;
            user.RememberTokenExpiresAtUtc = null;
            await _users.SaveAsync(user);
            return LoginResult.Fail("Oturum süresi doldu. Lütfen giriş yapın.");
        }

        user.FailedLoginCount = 0;
        user.LockoutUntilUtc = null;
        user.LastLoginAtUtc = now;
        user.RememberTokenExpiresAtUtc = now.AddDays(RememberTokenDays);
        await _users.SaveAsync(user);

        var session = ToSession(user);
        _currentUser.SignIn(session);
        await _audit.LogAsync(AuditAction.LoginSucceeded, user.Id);

        return LoginResult.Ok(session);
    }

    public async Task ClearRememberTokenAsync()
    {
        if (_currentUser.UserId is null)
            return;

        var user = await _users.GetByIdAsync(_currentUser.UserId);
        if (user is null)
            return;

        user.RememberTokenHash = null;
        user.RememberTokenExpiresAtUtc = null;
        await _users.SaveAsync(user);
    }

    public async Task<OperationResult> LogoutAsync()
    {
        var userId = _currentUser.UserId;
        // "Beni hatırla" token'ı çıkışta korunur; giriş ekranı kimlik bilgilerini
        // yeniden doldurur. Parola değişimi/sıfırlama token'ı ayrıca geçersiz kılar.
        _currentUser.SignOut();

        if (userId is not null)
            await _audit.LogAsync(AuditAction.Logout, userId);

        return OperationResult.Ok();
    }

    public async Task<OperationResult> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        string confirmPassword)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return OperationResult.Fail("Oturum bulunamadı. Lütfen yeniden giriş yapın.");

        var user = await _users.GetByIdAsync(_currentUser.UserId);
        if (user is null)
            return OperationResult.Fail("Kullanıcı kaydı bulunamadı.");

        if (!_hasher.Verify(currentPassword, user.PasswordHash, user.PasswordSalt, user.PasswordIterations))
            return OperationResult.Fail("Mevcut şifre hatalı.");

        var policyError = PasswordPolicy.Validate(newPassword);
        if (policyError is not null)
            return OperationResult.Fail(policyError);

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            return OperationResult.Fail("Yeni şifreler birbiriyle eşleşmiyor.");

        if (_hasher.Verify(newPassword, user.PasswordHash, user.PasswordSalt, user.PasswordIterations))
            return OperationResult.Fail("Yeni şifre mevcut şifreden farklı olmalıdır.");

        var hash = _hasher.Hash(newPassword);
        user.PasswordHash = hash.Hash;
        user.PasswordSalt = hash.Salt;
        user.PasswordIterations = hash.Iterations;
        user.RememberTokenHash = null;
        user.RememberTokenExpiresAtUtc = null;
        await _users.SaveAsync(user);

        await _audit.LogAsync(AuditAction.PasswordChanged, user.Id, nameof(User), user.Id);

        return OperationResult.Ok();
    }

    private int RemainingLockoutMinutes(User user, DateTime nowUtc)
    {
        if (user.LockoutUntilUtc is null)
            return 0;

        return Math.Max(1, (int)Math.Ceiling((user.LockoutUntilUtc.Value - nowUtc).TotalMinutes));
    }

    private static string CreateToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))
            .ToLowerInvariant();

    private static UserSession ToSession(User user)
        => new(user.Id, user.Username, user.DisplayName, user.Role);
}
