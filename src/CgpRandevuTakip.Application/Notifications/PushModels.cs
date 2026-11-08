namespace CgpRandevuTakip.Application.Notifications;

public sealed record PushRequest(
    string Provider,
    string Topic,
    string Token,
    string ChatId,
    string Title,
    string Body);

public interface IPushNotifier
{
    Task<bool> SendAsync(PushRequest request, CancellationToken cancellationToken = default);
}
