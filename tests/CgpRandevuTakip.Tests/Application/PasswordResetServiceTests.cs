using System.Text.RegularExpressions;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Repositories;
using CgpRandevuTakip.Infrastructure.Security;
using CgpRandevuTakip.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Tests.Application;

public class PasswordResetServiceTests
{
    private static readonly DateTime NowUtc = new(2026, 11, 8, 9, 0, 0, DateTimeKind.Utc);
    private const string Password = "GizliSifre123";
    private const string Email = "ayse@example.com";

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = new();

        public Task<OperationResult> SendAsync(
            string toAddress,
            string subject,
            string body,
            CancellationToken cancellationToken = default)
        {
            Sent.Add((toAddress, subject, body));
            return Task.FromResult(OperationResult.Ok());
        }
    }

    private sealed class Sut : IDisposable
    {
        public required TestDatabase Db { get; init; }
        public required FixedClock Clock { get; init; }
        public required FakeEmailSender Email { get; init; }
        public required RecordingAuditLogger Audit { get; init; }
        public required PasswordResetService Service { get; init; }
        public required User User { get; init; }

        public void Dispose() => Db.Dispose();
    }

    private static async Task<Sut> CreateAsync()
    {
        var db = new TestDatabase();
        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.Hash(Password);
        var user = new User
        {
            Username = "ayse",
            DisplayName = "Ayşe Yılmaz",
            Email = Email,
            PasswordHash = hash.Hash,
            PasswordSalt = hash.Salt,
            PasswordIterations = hash.Iterations,
            Role = UserRole.Admin,
            IsActive = true,
            IsLoginEnabled = true
        };

        var users = new UserRepository(db.Factory);
        await users.AddAsync(user);

        var clock = new FixedClock(NowUtc);
        var email = new FakeEmailSender();
        var audit = new RecordingAuditLogger();
        var service = new PasswordResetService(users, hasher, email, clock, audit);

        return new Sut
        {
            Db = db,
            Clock = clock,
            Email = email,
            Audit = audit,
            Service = service,
            User = user
        };
    }

    [Fact]
    public async Task RequestCode_ThenReset_ChangesPassword()
    {
        using var sut = await CreateAsync();

        var requested = await sut.Service.RequestCodeAsync(Email);
        Assert.True(requested.Success, requested.Error);
        Assert.Single(sut.Email.Sent);
        Assert.Contains(AuditAction.PasswordResetRequested, sut.Audit.Actions);

        var code = Regex.Match(sut.Email.Sent[0].Body, @"\b\d{6}\b").Value;
        Assert.Equal(6, code.Length);

        var reset = await sut.Service.ResetAsync(Email, code, "YeniSifre123", "YeniSifre123");
        Assert.True(reset.Success, reset.Error);
        Assert.Contains(AuditAction.PasswordResetCompleted, sut.Audit.Actions);

        using var db = sut.Db.CreateContext();
        var user = db.Users.Single(x => x.Username == "ayse");
        Assert.True(new Pbkdf2PasswordHasher().Verify(
            "YeniSifre123", user.PasswordHash, user.PasswordSalt, user.PasswordIterations));
        Assert.Null(user.PasswordResetCodeHash);
    }

    [Fact]
    public async Task RequestCode_UnknownEmail_DoesNotRevealAccount()
    {
        using var sut = await CreateAsync();

        var result = await sut.Service.RequestCodeAsync("yok@example.com");

        Assert.True(result.Success);
        Assert.Empty(sut.Email.Sent);
    }

    [Fact]
    public async Task RequestCode_TooSoon_IsRateLimited()
    {
        using var sut = await CreateAsync();

        var first = await sut.Service.RequestCodeAsync(Email);
        var second = await sut.Service.RequestCodeAsync(Email);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Contains("Çok sık", second.Error);
    }

    [Fact]
    public async Task Reset_WrongCode_IncrementsAttempts()
    {
        using var sut = await CreateAsync();
        await sut.Service.RequestCodeAsync(Email);

        var result = await sut.Service.ResetAsync(Email, "000000", "YeniSifre123", "YeniSifre123");

        Assert.False(result.Success);
        using var db = sut.Db.CreateContext();
        Assert.Equal(1, db.Users.Single(x => x.Username == "ayse").PasswordResetAttempts);
        Assert.Contains(AuditAction.PasswordResetFailed, sut.Audit.Actions);
    }

    [Fact]
    public async Task Reset_ExpiredCode_Fails()
    {
        using var sut = await CreateAsync();
        await sut.Service.RequestCodeAsync(Email);
        var code = Regex.Match(sut.Email.Sent[0].Body, @"\b\d{6}\b").Value;

        using (var db = sut.Db.CreateContext())
        {
            var user = db.Users.Single(x => x.Username == "ayse");
            user.PasswordResetExpiresAtUtc = NowUtc.AddMinutes(-1);
            db.SaveChanges();
        }

        var result = await sut.Service.ResetAsync(Email, code, "YeniSifre123", "YeniSifre123");

        Assert.False(result.Success);
        Assert.Contains("süresi dolmuş", result.Error);
    }
}
