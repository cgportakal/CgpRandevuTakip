namespace CgpRandevuTakip.Application.Abstractions;

/// <summary>
/// Sırları (push token, chat id vb.) kalıcı ortamda şifreli saklamak için kullanılır.
/// Boş veya null değerler değişmeden döner.
/// </summary>
public interface ISecretProtector
{
    string? Protect(string? plainText);
    string? Unprotect(string? cipherText);
}
