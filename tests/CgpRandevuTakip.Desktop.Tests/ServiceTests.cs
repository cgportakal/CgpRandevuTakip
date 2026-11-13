using System.IO;
using CgpRandevuTakip.Desktop.Services;
using CgpRandevuTakip.Domain.Common;

namespace CgpRandevuTakip.Desktop.Tests;

public class InstitutionLogoServiceTests : IDisposable
{
    private readonly string _root;
    private readonly InstitutionLogoService _service;

    public InstitutionLogoServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "cgp-logo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _service = new InstitutionLogoService(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Test temizliği başarısız olsa da sonucu etkilemez.
        }
    }

    [Fact]
    public void Import_CopiesFileAndReturnsRelativePath()
    {
        var source = CreateSourceFile(".png", 512);

        var result = _service.Import(source);

        Assert.True(result.Success, result.Error);
        Assert.Equal("branding/institution-logo.png", result.RelativePath);
        Assert.True(File.Exists(Path.Combine(_service.BrandingDirectory, "institution-logo.png")));
        Assert.NotNull(_service.ResolvePath(result.RelativePath));
    }

    [Fact]
    public void Import_ReplacesPreviousLogoExtension()
    {
        var png = CreateSourceFile(".png", 512);
        var jpg = CreateSourceFile(".jpg", 512);

        Assert.True(_service.Import(png).Success);
        var second = _service.Import(jpg);

        Assert.True(second.Success, second.Error);
        Assert.Single(Directory.GetFiles(_service.BrandingDirectory, "institution-logo.*"));
        Assert.EndsWith(".jpg", second.RelativePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Import_RejectsUnsupportedExtension()
    {
        var source = CreateSourceFile(".txt", 100);

        var result = _service.Import(source);

        Assert.False(result.Success);
        Assert.Contains("Desteklenmeyen", result.Error);
    }

    [Fact]
    public void Import_RejectsOversizedFile()
    {
        var source = CreateSourceFile(".png", InstitutionLogoService.MaxFileSizeBytes + 1);

        var result = _service.Import(source);

        Assert.False(result.Success);
        Assert.Contains("çok büyük", result.Error);
    }

    [Fact]
    public void Remove_DeletesLogoFiles()
    {
        Assert.True(_service.Import(CreateSourceFile(".png", 256)).Success);

        _service.Remove();

        Assert.Empty(Directory.GetFiles(_service.BrandingDirectory, "institution-logo.*"));
    }

    [Fact]
    public void ResolvePath_ReturnsNullWhenFileMissing()
        => Assert.Null(_service.ResolvePath("branding/yok.png"));

    private string CreateSourceFile(string extension, long size)
    {
        var directory = Path.Combine(Path.GetTempPath(), "cgp-logo-sources", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"logo{extension}");
        File.WriteAllBytes(path, new byte[size]);
        return path;
    }
}

public class UpdateRollbackServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _appSource;
    private readonly UpdateRollbackService _service;

    public UpdateRollbackServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "cgp-rollback-tests", Guid.NewGuid().ToString("N"));
        _appSource = Path.Combine(_root, "app-source");
        Directory.CreateDirectory(_appSource);
        File.WriteAllText(Path.Combine(_appSource, "app.exe"), "v1");
        File.WriteAllText(Path.Combine(_appSource, "app.dll"), "v1");
        Directory.CreateDirectory(Path.Combine(_appSource, "CGP-Data"));
        File.WriteAllText(Path.Combine(_appSource, "CGP-Data", "data.db"), "veri");

        _service = new UpdateRollbackService(Path.Combine(_root, "data"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Test temizliği başarısız olsa da sonucu etkilemez.
        }
    }

    [Fact]
    public void Prepare_CopiesProgramFilesButNotDataFolder()
    {
        var result = _service.Prepare("9.9.9", portable: false, sourceDirectory: _appSource);

        Assert.True(result);
        Assert.True(File.Exists(Path.Combine(_service.SnapshotAppDirectory, "app.exe")));
        Assert.True(File.Exists(Path.Combine(_service.SnapshotAppDirectory, "app.dll")));
        Assert.False(Directory.Exists(Path.Combine(_service.SnapshotAppDirectory, "CGP-Data")));
        Assert.Equal("veri", File.ReadAllText(Path.Combine(_appSource, "CGP-Data", "data.db")));
    }

    [Fact]
    public void Prepare_WritesSnapshotVersionAndPendingMarker()
    {
        Assert.True(_service.Prepare("9.9.9", portable: true, sourceDirectory: _appSource));

        var snapshot = _service.GetSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal(AppConstants.Version, snapshot!.Version);

        var pending = _service.ReadPending();
        Assert.NotNull(pending);
        Assert.Equal("9.9.9", pending!.TargetVersion);
        Assert.True(pending.Portable);
    }

    [Fact]
    public void CanRollback_TrueForDifferentVersion()
    {
        Assert.True(_service.Prepare("9.9.9", portable: false, sourceDirectory: _appSource));
        File.WriteAllText(Path.Combine(_service.SnapshotRoot, "version.txt"), "0.0.1");

        Assert.True(_service.CanRollback());
    }

    [Fact]
    public void ClearPending_RemovesMarker()
    {
        Assert.True(_service.Prepare("9.9.9", portable: false, sourceDirectory: _appSource));

        _service.ClearPending();

        Assert.Null(_service.ReadPending());
        Assert.NotNull(_service.GetSnapshot());
    }
}
