namespace CgpRandevuTakip.Application.Common;

public sealed record OperationResult(bool Success, string? Error)
{
    /// <summary>Sunucu bağlantısı olmadığı için işlem yerel kuyruğa alındı.</summary>
    public bool Queued { get; init; }

    public static OperationResult Ok() => new(true, null);

    public static OperationResult QueuedOk() => new(true, null) { Queued = true };

    public static OperationResult Fail(string error) => new(false, error);
}
