using CgpRandevuTakip.Application.Common;

namespace CgpRandevuTakip.Application.Users;

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> GetAllAsync();
    Task<OperationResult> CreateAsync(UserCreateInput input);
    Task<OperationResult> UpdateAsync(string userId, UserUpdateInput input);
    Task<OperationResult> ResetPasswordAsync(string userId, UserResetPasswordInput input);
}
