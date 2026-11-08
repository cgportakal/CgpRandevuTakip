using System.Net.Mail;

namespace CgpRandevuTakip.Application.Users;

public static class EmailAddressPolicy
{
    public static bool TryNormalize(string? value, out string email)
    {
        email = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            var address = new MailAddress(value.Trim()).Address;
            if (address.Length > 200)
                return false;

            email = address.ToLowerInvariant();
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
