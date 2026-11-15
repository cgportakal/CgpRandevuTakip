using CgpRandevuTakip.Api.Auth;
using CgpRandevuTakip.Api.Contracts;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Api.Endpoints;

public static class AuthEndpoints
{
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/auth").WithTags("Auth");

        group.MapGet("/status", async (IAuthService auth) =>
            Results.Ok(new { isFirstRun = await auth.IsFirstRunAsync() }))
            .AllowAnonymous();

        group.MapPost("/setup", async (
            SetupRequestDto request,
            IAuthService auth,
            JwtTokenService jwt,
            ApiTokenStore tokens,
            IUserRepository users) =>
        {
            if (await auth.IsFirstRunAsync() is false)
                return Results.Conflict(new ErrorResponse("İlk kurulum daha önce tamamlanmış."));

            var result = await auth.CreateFirstAdminAsync(new SetupRequest(
                request.Username ?? string.Empty,
                request.DisplayName ?? string.Empty,
                request.Email ?? string.Empty,
                request.Password ?? string.Empty,
                request.ConfirmPassword ?? string.Empty));

            if (!result.Success || result.Session is null)
                return Results.BadRequest(new ErrorResponse(result.Error ?? "Kurulum tamamlanamadı."));

            var user = await users.GetByIdAsync(result.Session.UserId);
            return Results.Ok(BuildTokens(result.Session, user, jwt, tokens, request.DeviceName));
        })
        .AllowAnonymous();

        group.MapPost("/login", async (
            LoginRequest request,
            IAuthService auth,
            IUserRepository users,
            JwtTokenService jwt,
            ApiTokenStore tokens) =>
        {
            var result = await auth.LoginAsync(
                request.Username ?? string.Empty,
                request.Password ?? string.Empty);

            if (!result.Success || result.Session is null)
            {
                return Results.Json(
                    new ErrorResponse(result.Error ?? "Giriş başarısız.", null),
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var user = await users.GetByIdAsync(result.Session.UserId);
            return Results.Ok(BuildTokens(result.Session, user, jwt, tokens, request.DeviceName));
        })
        .AllowAnonymous();

        group.MapPost("/refresh", async (
            RefreshRequest request,
            ApiTokenStore tokens,
            IUserRepository users,
            JwtTokenService jwt) =>
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                return Results.BadRequest(new ErrorResponse("Yenileme belirteci gerekli."));

            var userId = tokens.Validate(request.RefreshToken);
            if (userId is null)
                return Results.Json(
                    new ErrorResponse("Oturum süresi doldu; yeniden giriş yapın."),
                    statusCode: StatusCodes.Status401Unauthorized);

            var user = await users.GetByIdAsync(userId);
            if (user is null || !user.IsActive || !user.IsLoginEnabled)
                return Results.Json(
                    new ErrorResponse("Hesap artık etkin değil."),
                    statusCode: StatusCodes.Status401Unauthorized);

            var session = new UserSession(user.Id, user.Username, user.DisplayName, user.Role);
            tokens.Revoke(request.RefreshToken);
            return Results.Ok(BuildTokens(session, user, jwt, tokens, deviceName: null));
        })
        .AllowAnonymous();

        group.MapPost("/logout", (RefreshRequest request, ApiTokenStore tokens) =>
        {
            if (!string.IsNullOrWhiteSpace(request.RefreshToken))
                tokens.Revoke(request.RefreshToken);

            return Results.NoContent();
        })
        .RequireAuthorization();

        group.MapGet("/me", (ICurrentUser currentUser) =>
        {
            if (!currentUser.IsAuthenticated)
                return Results.Unauthorized();

            return Results.Ok(new UserDto(
                currentUser.UserId ?? string.Empty,
                currentUser.Username,
                currentUser.DisplayName,
                currentUser.Role?.ToString() ?? UserRole.Host.ToString()));
        })
        .RequireAuthorization();

        return app;
    }

    private static AuthTokensResponse BuildTokens(
        UserSession session,
        User? user,
        JwtTokenService jwt,
        ApiTokenStore tokens,
        string? deviceName)
    {
        var access = jwt.Create(session);
        var refresh = tokens.Create(session.UserId, deviceName, RefreshLifetime);
        var seconds = Math.Max(1, (int)(access.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds);

        return new AuthTokensResponse(
            access.Token,
            refresh,
            seconds,
            new UserDto(
                session.UserId,
                user?.Username ?? session.Username,
                user?.DisplayName ?? session.DisplayName,
                (user?.Role ?? session.Role).ToString()));
    }
}
