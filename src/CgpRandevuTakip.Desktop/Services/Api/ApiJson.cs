using System.Text.Json;
using System.Text.Json.Serialization;

namespace CgpRandevuTakip.Desktop.Services.Api;

/// <summary>
/// API ile paylaşılan JSON ayarları. Sunucu numaralandırmaları metin olarak
/// gönderdiği için <see cref="JsonStringEnumConverter"/> zorunludur.
/// </summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
