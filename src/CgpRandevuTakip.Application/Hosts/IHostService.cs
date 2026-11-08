using CgpRandevuTakip.Application.Common;

namespace CgpRandevuTakip.Application.Hosts;

public interface IHostService
{
    Task<IReadOnlyList<HostDto>> GetAllAsync();
    Task<IReadOnlyList<HostDto>> GetSelectableHostsAsync();
    Task<OperationResult> CreateAsync(HostCreateInput input);
    Task<OperationResult> UpdateAsync(string id, HostUpdateInput input);
}
