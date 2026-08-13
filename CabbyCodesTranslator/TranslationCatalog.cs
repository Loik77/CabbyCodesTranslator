using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace CabbyCodesTranslator;

public static class TranslationCatalog
{
    private const string ResourceName = "CabbyCodesTranslator.translations.zh-CN.json";

    public static Dictionary<string, string> Load()
    {
        var external = Path.Combine(AppContext.BaseDirectory, "translations", "zh-CN.json");
        if (File.Exists(external))
        {
            try
            {
                var json = File.ReadAllText(external);
                var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (map is not null) return new Dictionary<string, string>(map, StringComparer.Ordinal);
            }
            catch
            {
                // Fall back to the embedded catalog when the external file is invalid.
            }
        }

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null) return new Dictionary<string, string>(StringComparer.Ordinal);
        using var reader = new StreamReader(stream);
        var embedded = reader.ReadToEnd();
        return JsonSerializer.Deserialize<Dictionary<string, string>>(embedded)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
