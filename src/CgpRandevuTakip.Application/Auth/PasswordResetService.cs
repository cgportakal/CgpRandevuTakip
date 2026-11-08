using System.Security.Cryptography;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Application.Users;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Auth;

public sealed class PasswordResetService : IPasswordResetService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);
    private const int MaxAttempts = 5;

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IEmailSender _email;
    private readonly IClock _clock;
    private readonly IAuditLogger _audit;

    public PasswordResetService(
        IUserRepository users,
        IPasswordHasher hasher,
        IEmailSender email,
        IClock clock,
        IAuditLogger audit)
    {
        _users = users;
        _hasher = hasher;
        _email = email;
        _clock = clock;
        _audit = audit;
    }

    public async Task<OperationResult> RequestCodeAsync(string email)
    {
        if (!EmailAddressPolicy.TryNormalize(email, out var normalized))
            return OperationResult.Fail("Geçerli bir e-posta adresi girin.");

        var user = await _users.FindByEmailAsync(normalized);

        // Hesap numaralandırmasını önlemek için bilinmeyen adreslerde de aynı yanıt verilir.
        if (user is null || !user.IsActive || !user.IsLoginEnabled)
            return OperationResult.Ok();

        var now = _clock.UtcNow;
        if (user.PasswordResetRequestedAtUtc is { } requestedAt
            && requestedAt > now - ResendCooldown)
        {
            return OperationResult.Fail("Çok sık kod istendi. Lütfen 1 dakika sonra tekrar deneyin.");
        }

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var hash = _hasher.Hash(code);

        user.PasswordResetCodeHash = hash.Hash;
        user.PasswordResetCodeSalt = hash.Salt;
        user.PasswordResetCodeIterations = hash.Iterations;
        user.PasswordResetExpiresAtUtc = now.Add(CodeLifetime);
        user.PasswordResetRequestedAtUtc = now;
        user.PasswordResetAttempts = 0;
        await _users.SaveAsync(user);

        var body = $"""
            CGP - Randevu Takip

            Şifre sıfırlama kodunuz: {code}

            Kod {CodeLifetime.TotalMinutes:0} dakika geçerlidir.
            Bu talebi siz yapmadıysanız yöneticinize başvurun.
            """;

        var sent = await _email.SendAsync(
            normalized,
            "CGP - Randevu Takip şifre sıfırlama kodu",
            body);

        if (!sent.Success)
        {
            user.PasswordResetCodeHash = null;
            user.PasswordResetCodeSalt = null;
            user.PasswordResetCodeIterations = 0;
            user.PasswordResetExpiresAtUtc = null;
            await _users.SaveAsync(user);

            await _audit.LogAsync(AuditAction.PasswordResetFailed, user.Id, nameof(User), user.Id);
            return OperationResult.Fail(
                sent.Error ?? "E-posta gönderilemedi. Yöneticinize başvurun.");
        }

        await _audit.LogAsync(AuditAction.PasswordResetRequested, user.Id, nameof(User), user.Id);
        return OperationResult.Ok();
    }

    public async Task<OperationResult> ResetAsync(
        string email,
        string code,
        string newPassword,
        string confirmPassword)
    {
        if (!EmailAddressPolicy.TryNormalize(email, out var normalized))
            return OperationResult.Fail("Geçerli bir e-posta adresi girin.");

        var user = await _users.FindByEmailAsync(normalized);
        if (user is null || !user.IsActive || !user.IsLoginEnabled)
            return OperationResult.Fail("Kod geçersiz veya süresi dolmuş.");

        var now = _clock.UtcNow;

        if (user.PasswordResetCodeHash is null
            || user.PasswordResetExpiresAtUtc is null
            || user.PasswordResetExpiresAtUtc <= now)
        {
            return OperationResult.Fail("Kod geçersiz veya süresi dolmuş.");
        }

        if (user.PasswordResetAttempts >= MaxAttempts)
            return OperationResult.Fail("Çok fazla hatalı kod denemesi. Yeni kod isteyin.");

        if (string.IsNullOrWhiteSpace(code)
            || !_hasher.Verify(
                code.Trim(),
                user.PasswordResetCodeHash,
                user.PasswordResetCodeSalt ?? string.Empty,
                user.PasswordResetCodeIterations))
        {
            user.PasswordResetAttempts++;
            await _users.SaveAsync(user);
            await _audit.LogAsync(AuditAction.PasswordResetFailed, user.Id, nameof(User), user.Id);
            return OperationResult.Fail("Kod geçersiz veya süresi dolmuş.");
        }

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
        user.PasswordResetCodeHash = null;
        user.PasswordResetCodeSalt = null;
        user.PasswordResetCodeIterations = 0;
        user.PasswordResetExpiresAtUtc = null;
        user.PasswordResetRequestedAtUtc = null;
        user.PasswordResetAttempts = 0;
        user.FailedLoginCount = 0;
        user.LockoutUntilUtc = null;
        user.RememberTokenHash = null;
        user.RememberTokenExpiresAtUtc = null;
        await _users.SaveAsync(user);

        await _audit.LogAsync(AuditAction.PasswordResetCompleted, user.Id, nameof(User), user.Id);
        return OperationResult.Ok();
    }
}
