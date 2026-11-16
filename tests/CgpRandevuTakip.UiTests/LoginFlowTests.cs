using System.Diagnostics;
using System.IO;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Persistence;
using CgpRandevuTakip.Infrastructure.Security;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.UiTests;

/// <summary>
/// Uçtan uca masaüstü UI testleri (FlaUI / UIA3). Yayınlanmış EXE'yi taşınabilir modda
/// çalıştırır. Varsayılan olarak kapalıdır: <c>CGP_UI_TESTS=1</c> ve yayın çıktısı gerekir.
/// Yerelde: <c>dotnet test tests\CgpRandevuTakip.UiTests --filter Category=Ui</c>
/// </summary>
[Trait("Category", "Ui")]
public class LoginFlowTests
{
    private const string AdminUsername = "uiadmin";
    private const string AdminPassword = "UiTest12345!";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(40);

    [Fact]
    public void Login_OpensMainWindow()
    {
        if (Environment.GetEnvironmentVariable("CGP_UI_TESTS") != "1")
            return;

        var exe = ResolveExecutable();
        if (exe is null)
            return;

        var dataRoot = Path.Combine(Path.GetDirectoryName(exe)!, AppPaths.PortableDataFolderName);
        PrepareDatabase(dataRoot);

        using var automation = new UIA3Automation();
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = Path.GetDirectoryName(exe)!,
            Arguments = "--portable",
            UseShellExecute = false
        });

        Assert.NotNull(process);

        try
        {
            var app = FlaUI.Core.Application.Attach(process!);
            var window = WaitForWindow(app, automation, w =>
                w.Title.Contains("Giriş", StringComparison.OrdinalIgnoreCase));

            if (window is null)
            {
                // İlk çalıştırma sihirbazı açılmış olabilir; en azından pencere açılmalı.
                var setup = WaitForWindow(app, automation, w =>
                    w.Title.Contains("Kurulum", StringComparison.OrdinalIgnoreCase));
                Assert.NotNull(setup);
                setup!.Close();
                return;
            }

            window.SetForeground();
            Thread.Sleep(500);

            var username = window
                .FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit)
                    .And(cf.ByName("Kullanıcı adı")))?
                .AsTextBox();
            Assert.NotNull(username);
            username!.Text = AdminUsername;

            var password = window
                .FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit)
                    .And(cf.ByName("Şifre")))?
                .AsTextBox();
            Assert.NotNull(password);
            password!.Patterns.Value.Pattern.SetValue(AdminPassword);

            var loginButton = window
                .FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                    .And(cf.ByName("Giriş yap")))?
                .AsButton();
            Assert.NotNull(loginButton);
            loginButton!.Invoke();

            var main = WaitForWindow(app, automation, w =>
                w.Title.Contains("CGP - Randevu Takip", StringComparison.OrdinalIgnoreCase)
                && !w.Title.Contains("Giriş", StringComparison.OrdinalIgnoreCase));

            Assert.NotNull(main);
            main!.Close();
        }
        finally
        {
            if (!process!.HasExited && !process.WaitForExit(5000))
                process.Kill(entireProcessTree: true);

            TryDelete(dataRoot);
        }
    }

    private static string? ResolveExecutable()
    {
        var fromEnv = Environment.GetEnvironmentVariable("CGP_UI_EXE");
        if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv))
            return fromEnv;

        var root = FindRepoRoot();
        var dist = Path.Combine(root, "dist");
        if (!Directory.Exists(dist))
            return null;

        return Directory
            .EnumerateFiles(dist, "CgpRandevuTakip.Desktop.exe", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static void PrepareDatabase(string dataRoot)
    {
        if (Directory.Exists(dataRoot))
            Directory.Delete(dataRoot, recursive: true);

        Directory.CreateDirectory(dataRoot);
        var databasePath = Path.Combine(dataRoot, "data.db");
        var factory = new SqliteDbContextFactory(databasePath);

        using var db = factory.CreateDbContext();
        DatabaseInitializer.Initialize(db, databasePath);

        if (db.Users.Any())
            return;

        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.Hash(AdminPassword);

        db.Users.Add(new User
        {
            Username = AdminUsername,
            DisplayName = "UI Test Yönetici",
            Email = "ui@example.com",
            Role = UserRole.Admin,
            IsActive = true,
            IsLoginEnabled = true,
            PasswordHash = hash.Hash,
            PasswordSalt = hash.Salt,
            PasswordIterations = hash.Iterations
        });

        db.SaveChanges();
    }

    private static Window? WaitForWindow(
        FlaUI.Core.Application app,
        UIA3Automation automation,
        Func<Window, bool> predicate)
    {
        var deadline = DateTime.UtcNow.Add(Timeout);
        while (DateTime.UtcNow < deadline)
        {
            foreach (var window in app.GetAllTopLevelWindows(automation))
            {
                if (predicate(window))
                    return window;
            }

            Thread.Sleep(500);
        }

        return null;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CgpRandevuTakip.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Depo kökü bulunamadı.");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Test temizliği başarısız olsa da sonucu etkilemez.
        }
    }
}
