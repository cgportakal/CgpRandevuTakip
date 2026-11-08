using CgpRandevuTakip.Application.Common;

namespace CgpRandevuTakip.Application.Auth;

public interface IPasswordResetService
{
    Task<OperationResult> RequestCodeAsync(string email);
    Task<OperationResult> ResetAsync(
        string email,
        string code,
        string newPassword,
        string confirmPassword);
}
