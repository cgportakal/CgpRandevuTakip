using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CgpRandevuTakip.Api.Infrastructure;
using CgpRandevuTakip.Application.Auth;
using Microsoft.IdentityModel.Tokens;

namespace CgpRandevuTakip.Api.Auth;

public sealed record AccessTokenResult(string Token, DateTime ExpiresAtUtc);

/// <summary>
/// Kısa ömürlü JWT erişim belirteci üretir. Anahtar yapılandırmadan
/// (<c>Jwt:Key</c> / <c>CGP_JWT_KEY</c>) veya veri klasöründeki <c>jwt.key</c> dosyasından alınır.
/// </summary>
public sealed class JwtTokenService
{
    public const string Issuer = "cgp-randevu-takip";

    private readonly byte[] _key;
    private readonly int _accessMinutes;

    public JwtTokenService(IConfiguration configuration, ApiEnvironment environment)
    {
        var configured = configuration["Jwt:Key"]
            ?? Environment.GetEnvironmentVariable("CGP_JWT_KEY");

        _key = !string.IsNullOrWhiteSpace(configured)
            ? Encoding.UTF8.GetBytes(configured)
            : LoadOrCreateKey(Path.Combine(environment.RootDirectory, "jwt.key"));

        if (_key.Length < 32)
            throw new InvalidOperationException("JWT anahtarı en az 32 bayt olmalıdır.");

        _accessMinutes = configuration.GetValue("Jwt:AccessTokenMinutes", 30);
    }

    public AccessTokenResult Create(UserSession session)
    {
        var expires = DateTime.UtcNow.AddMinutes(_accessMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, session.UserId),
            new(JwtRegisteredClaimNames.UniqueName, session.Username),
            new("display_name", session.DisplayName),
            new("role", session.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(_key),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Issuer,
            claims: claims,
            notBefore: DateTime.UtcNow.AddSeconds(-5),
            expires: expires,
            signingCredentials: credentials);

        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public TokenValidationParameters CreateValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Issuer,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(_key),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = JwtRegisteredClaimNames.UniqueName,
        RoleClaimType = "role"
    };

    private static byte[] LoadOrCreateKey(string path)
    {
        if (File.Exists(path))
        {
            var existing = File.ReadAllBytes(path);
            if (existing.Length >= 32)
                return existing;
        }

        var key = RandomNumberGenerator.GetBytes(48);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, key);
        return key;
    }
}
