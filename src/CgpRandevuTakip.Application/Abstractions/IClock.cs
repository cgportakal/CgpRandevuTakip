namespace CgpRandevuTakip.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
    DateTime ToLocal(DateTime utc);
    DateTime ToUtc(DateTime local);
}
