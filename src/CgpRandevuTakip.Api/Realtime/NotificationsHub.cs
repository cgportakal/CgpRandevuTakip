using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CgpRandevuTakip.Api.Realtime;

/// <summary>
/// Anlık olay kanalı. İstemciler günlük liste, içerik ve hatırlatıcı olaylarını buradan alır.
/// Tek kurum kullanıldığı için tüm kimliği doğrulanmış istemciler "institution" grubundadır.
/// </summary>
[Authorize]
public sealed class NotificationsHub : Hub
{
    public const string InstitutionGroup = "institution";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue("sub");
        var role = Context.User?.FindFirstValue("role");

        await Groups.AddToGroupAsync(Context.ConnectionId, InstitutionGroup);

        if (!string.IsNullOrWhiteSpace(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        if (!string.IsNullOrWhiteSpace(role))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"role:{role}");

        await base.OnConnectedAsync();
    }
}
