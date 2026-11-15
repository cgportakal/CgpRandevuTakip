using System.Security.Claims;
using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Api.Auth;

/// <summary>
/// JWT doğrulandıktan sonra istek kapsamındaki <see cref="CurrentUser"/> nesnesini doldurur.
/// Uygulama servisleri (randevu, kategori, ayar) bu nesneden kullanıcı kimliğini okur.
/// </summary>
public sealed class CurrentUserMiddleware
{
    private readonly RequestDelegate _next;

    public CurrentUserMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, CurrentUser currentUser)
    {
        var identity = context.User.Identity;
        if (identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirstValue("sub");
            var username = context.User.FindFirstValue("unique_name") ?? string.Empty;
            var displayName = context.User.FindFirstValue("display_name") ?? username;

            if (!string.IsNullOrWhiteSpace(userId)
                && Enum.TryParse<UserRole>(context.User.FindFirstValue("role"), out var role))
            {
                currentUser.SignIn(new UserSession(userId, username, displayName, role));
            }
        }

        await _next(context);
    }
}
