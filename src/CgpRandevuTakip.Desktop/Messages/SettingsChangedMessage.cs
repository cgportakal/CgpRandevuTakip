namespace CgpRandevuTakip.Desktop.Messages;

/// <summary>Ayarlar kaydedildikten sonra açık pencerelerin anında güncellenmesi için.</summary>
public sealed record SettingsChangedMessage(bool CloseToTray);
