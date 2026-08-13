using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CabbyCodesTranslator;

public readonly record struct TranslationResult(int Scanned, int Replaced);

public static class Translator
{
    private static readonly Dictionary<string, string> Map = LoadMap();
    private static readonly Regex InternalId = new(@"^(Crossroads|Fungus\d*|Mines|Ruins|Deepnest|Abyss|RestingGrounds|City|Waterways|Cliffs|Grimm|Hive|KingsPass|Dream)\w*[_-]\d+$", RegexOptions.Compiled);

    private static Dictionary<string, string> LoadMap()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("CabbyCodesTranslator.translations.zh-CN.json");
        if (stream is null) return new Dictionary<string, string>(StringComparer.Ordinal);
        using var reader = new StreamReader(stream);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd())
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public static TranslationResult Translate(string input, string output)
    {
        if (!File.Exists(input)) throw new FileNotFoundException("找不到 DLL", input);
        var module = ModuleDefMD.Load(input);
        int scanned = 0, replaced = 0;
        try
        {
            foreach (var type in module.Types) Walk(type, ref scanned, ref replaced);
            module.Write(output);
        }
        finally { module.Dispose(); }
        return new TranslationResult(scanned, replaced);
    }

    private static void Walk(TypeDef type, ref int scanned, ref int replaced)
    {
        foreach (var f in type.Fields)
        {
            if (f.HasConstant && f.Constant?.Value is string) scanned++;
        }
        foreach (var m in type.Methods)
        {
            if (!m.HasBody) continue;
            foreach (var ins in m.Body.Instructions)
            {
                if (ins.Operand is string s)
                {
                    scanned++;
                    if (TryTranslate(s, out var t)) { ins.Operand = t; replaced++; }
                }
            }
        }
        foreach (var n in type.NestedTypes) Walk(n, ref scanned, ref replaced);
    }

    private static bool TryTranslate(string s, out string translated)
    {
        translated = s;
        if (string.IsNullOrWhiteSpace(s) || InternalId.IsMatch(s)) return false;
        return Map.TryGetValue(s, out translated);
    }
}
