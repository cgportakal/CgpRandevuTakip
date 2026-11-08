using CgpRandevuTakip.Application.Common;

namespace CgpRandevuTakip.Application.Abstractions;

public interface IEmailSender
{
    Task<OperationResult> SendAsync(
        string toAddress,
        string subject,
        string body,
        CancellationToken cancellationToken = default);
}
