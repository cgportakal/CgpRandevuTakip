using CgpRandevuTakip.Infrastructure.Security;
using Xunit;

namespace CgpRandevuTakip.Tests.Infrastructure;

public class Pbkdf2PasswordHasherTests
{
    [Fact]
    public void Hash_ThenVerify_ReturnsTrue()
    {
        var hasher = new Pbkdf2PasswordHasher();

        var result = hasher.Hash("GizliSifre123!");

        Assert.True(hasher.Verify("GizliSifre123!", result.Hash, result.Salt, result.Iterations));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var result = hasher.Hash("GizliSifre123!");

        Assert.False(hasher.Verify("YanlisSifre123!", result.Hash, result.Salt, result.Iterations));
    }

    [Fact]
    public void Hash_SamePassword_ProducesDifferentSaltAndHash()
    {
        var hasher = new Pbkdf2PasswordHasher();

        var first = hasher.Hash("GizliSifre123!");
        var second = hasher.Hash("GizliSifre123!");

        Assert.NotEqual(first.Salt, second.Salt);
        Assert.NotEqual(first.Hash, second.Hash);
    }

    [Fact]
    public void Hash_EmptyPassword_Throws()
    {
        var hasher = new Pbkdf2PasswordHasher();

        Assert.Throws<ArgumentException>(() => hasher.Hash(""));
    }
}
