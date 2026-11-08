using CgpRandevuTakip.Application.Auth;

namespace CgpRandevuTakip.Application.Auth;

public interface IAuthService
{
    Task<bool> IsFirstRunAsync();
    Task<SetupResult> CreateFirstAdminAsync(SetupRequest request);
    Task<LoginResult> LoginAsync(string username, string password);
    Task<Common.OperationResult> LogoutAsync();
    Task<Common.OperationResult> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        string confirmPassword);
}
