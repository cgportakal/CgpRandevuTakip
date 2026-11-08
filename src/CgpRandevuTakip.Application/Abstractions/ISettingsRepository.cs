namespace CgpRandevuTakip.Application.Abstractions;

public interface ISettingsRepository
{
    Task<IReadOnlyDictionary<string, string>> GetAllAsync();
    Task SetManyAsync(IReadOnlyDictionary<string, string> values);
}
