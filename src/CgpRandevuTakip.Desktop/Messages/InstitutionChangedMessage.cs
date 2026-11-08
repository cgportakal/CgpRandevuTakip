namespace CgpRandevuTakip.Desktop.Messages;

/// <summary>Ayarlarda kurum adı kaydedildiğinde açık ekranları anında günceller.</summary>
public sealed record InstitutionChangedMessage(string InstitutionName);
