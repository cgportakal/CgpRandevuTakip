using CgpRandevuTakip.Application.Settings;

namespace CgpRandevuTakip.Tests.Application;

public class SetupValidatorTests
{
    private const string ValidInstitutionName = "BEK Okulları";
    private const string ValidEmail = "info@bek.example";
    private const string ValidPhone = "0532 123 45 67";
    private const string ValidAddress = "Atatürk Mahallesi Okul Caddesi No: 1, Ankara";

    [Fact]
    public void ValidateInstitution_AcceptsCompleteProfile()
        => Assert.Null(SetupValidator.ValidateInstitution(
            ValidInstitutionName,
            ValidEmail,
            ValidPhone,
            ValidAddress));

    [Theory]
    [InlineData("", ValidEmail, ValidPhone, ValidAddress)]
    [InlineData(ValidInstitutionName, "", ValidPhone, ValidAddress)]
    [InlineData(ValidInstitutionName, ValidEmail, "", ValidAddress)]
    [InlineData(ValidInstitutionName, ValidEmail, ValidPhone, "")]
    public void ValidateInstitution_RequiresEveryField(
        string name,
        string email,
        string phone,
        string address)
        => Assert.NotNull(SetupValidator.ValidateInstitution(name, email, phone, address));

    [Theory]
    [InlineData("12345")]
    [InlineData("telefon")]
    [InlineData("0000000000")]
    public void ValidateInstitution_RejectsInvalidPhone(string phone)
        => Assert.NotNull(SetupValidator.ValidateInstitution(
            ValidInstitutionName,
            ValidEmail,
            phone,
            ValidAddress));

    [Fact]
    public void ValidateInstitution_RejectsShortAddress()
        => Assert.NotNull(SetupValidator.ValidateInstitution(
            ValidInstitutionName,
            ValidEmail,
            ValidPhone,
            "Kısa"));

    [Fact]
    public void ValidateAdmin_AcceptsCompleteProfile()
        => Assert.Null(SetupValidator.ValidateAdmin(
            "Ayşe Yılmaz",
            "ayse.yilmaz",
            "ayse@example.com",
            "GizliSifre123",
            "GizliSifre123"));

    [Fact]
    public void ValidateAdmin_RequiresEmail()
        => Assert.NotNull(SetupValidator.ValidateAdmin(
            "Ayşe Yılmaz",
            "ayse.yilmaz",
            "",
            "GizliSifre123",
            "GizliSifre123"));

    [Fact]
    public void ValidateAdmin_RejectsMismatchedPasswords()
        => Assert.NotNull(SetupValidator.ValidateAdmin(
            "Ayşe Yılmaz",
            "ayse.yilmaz",
            "ayse@example.com",
            "GizliSifre123",
            "BaskaSifre123"));

    [Fact]
    public void ValidateNewPassword_RequiresCurrentPassword()
        => Assert.NotNull(SetupValidator.ValidateNewPassword(
            "",
            "YeniSifre123",
            "YeniSifre123"));

    [Fact]
    public void ValidatePreferences_RejectsReversedHours()
        => Assert.NotNull(SetupValidator.ValidatePreferences(
            30,
            "18:00",
            "09:00",
            "08:30"));

    [Fact]
    public void ValidatePreferences_RejectsInvalidTime()
        => Assert.NotNull(SetupValidator.ValidatePreferences(
            30,
            "25:00",
            "20:45",
            "08:30"));

    [Fact]
    public void ValidatePreferences_AcceptsDefaults()
        => Assert.Null(SetupValidator.ValidatePreferences(30, "06:00", "20:45", "08:30"));

    [Fact]
    public void ValidateSmtp_AllowsSkipping()
        => Assert.Null(SetupValidator.ValidateSmtp("", "587", "", "", ""));

    [Fact]
    public void ValidateSmtp_RequiresFromAddressWhenHostIsSet()
        => Assert.NotNull(SetupValidator.ValidateSmtp("smtp.gmail.com", "587", "", "", ""));

    [Fact]
    public void ValidateSmtp_RequiresPasswordWhenUsernameIsSet()
        => Assert.NotNull(SetupValidator.ValidateSmtp(
            "smtp.gmail.com",
            "587",
            ValidEmail,
            "info@bek.example",
            ""));

    [Fact]
    public void ValidateSmtp_RejectsInvalidPort()
        => Assert.NotNull(SetupValidator.ValidateSmtp(
            "smtp.gmail.com",
            "70000",
            ValidEmail,
            "",
            ""));

    [Fact]
    public void ValidatePush_AllowsSkipping()
        => Assert.Null(SetupValidator.ValidatePush(false, "ntfy", "", "", ""));

    [Fact]
    public void ValidatePush_RequiresNtfyTopic()
        => Assert.NotNull(SetupValidator.ValidatePush(true, "ntfy", "", "", ""));

    [Fact]
    public void ValidatePush_RequiresNumericTelegramChatId()
        => Assert.NotNull(SetupValidator.ValidatePush(true, "telegram", "", "token", "abc"));

    [Fact]
    public void ValidatePush_AcceptsTelegramConfiguration()
        => Assert.Null(SetupValidator.ValidatePush(true, "telegram", "", "token", "123456"));

    [Fact]
    public void ValidateBackup_RequiresPasswordWhenEncryptionIsOn()
        => Assert.NotNull(SetupValidator.ValidateBackup(
            @"C:\Yedekler",
            encryptionEnabled: true,
            password: "",
            confirmPassword: "",
            driveEnabled: false,
            driveFolder: ""));

    [Fact]
    public void ValidateBackup_RequiresMatchingPasswords()
        => Assert.NotNull(SetupValidator.ValidateBackup(
            @"C:\Yedekler",
            encryptionEnabled: true,
            password: "GizliSifre123",
            confirmPassword: "BaskaSifre123",
            driveEnabled: false,
            driveFolder: ""));

    [Fact]
    public void ValidateBackup_RequiresEncryptionForDrive()
        => Assert.NotNull(SetupValidator.ValidateBackup(
            @"C:\Yedekler",
            encryptionEnabled: false,
            password: "",
            confirmPassword: "",
            driveEnabled: true,
            driveFolder: @"C:\Drive\CGP Yedekler"));

    [Fact]
    public void ValidateBackup_AcceptsDefaultConfiguration()
        => Assert.Null(SetupValidator.ValidateBackup(
            @"C:\Yedekler",
            encryptionEnabled: false,
            password: "",
            confirmPassword: "",
            driveEnabled: false,
            driveFolder: ""));
}
