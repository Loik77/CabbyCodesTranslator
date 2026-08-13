using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CabbyCodesTranslator;

public static class TranslationStore
{
    public static Dictionary<string,string> Load(string path)
    {
        if (!File.Exists(path)) return new Dictionary<string,string>();
        return JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(path))
            ?? new Dictionary<string,string>();
    }
}
