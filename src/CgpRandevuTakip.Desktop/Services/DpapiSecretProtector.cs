using System.Security.Cryptography;
using System.Text;
using CgpRandevuTakip.Application.Abstractions;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>
/// Windows DPAPI (CurrentUser) ile sır şifreleme. Şifreli metin Base64 olarak saklanır.
/// Boş değerler değişmeden döner; hata durumunda best-effort olarak düz metin döndürülmez.
/// </summary>
public sealed class DpapiSecretProtector : ISecretProtector
{
    private const DataProtectionScope Scope = DataProtectionScope.CurrentUser;

    public string? Protect(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        var bytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, Scope);
        return Convert.ToBase64String(protectedBytes);
    }

    public string? Unprotect(string? cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;

        try
        {
            var bytes = Convert.FromBase64String(cipherText);
            var plainBytes = ProtectedData.Unprotect(bytes, optionalEntropy: null, Scope);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (FormatException)
        {
            // DPAPI öncesi düz metin kayıtlar için geçiş uyumu.
            return cipherText;
        }
    }
}
