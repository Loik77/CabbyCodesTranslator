using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CabbyCodesTranslator;

public readonly record struct TranslationResult(int Scanned, int Replaced);

public static class Translator
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.Ordinal)
    {
        ["Cabby Codes"] = "Cabby 代码",
        ["Code"] = "代码",
        ["Codes"] = "代码",
        ["Menu"] = "菜单",
        ["Settings"] = "设置",
        ["Save"] = "保存",
        ["Load"] = "读取",
        ["Cancel"] = "取消",
        ["Confirm"] = "确认",
        ["Back"] = "返回",
        ["Close"] = "关闭",
        ["Enabled"] = "启用",
        ["Disabled"] = "禁用"
    };

    private static readonly Regex InternalId = new(@"^(Crossroads|Fungus|Mines|Ruins|Deepnest|Abyss|RestingGrounds|City|Waterways|Cliffs|Grimm|Hive|KingsPass|Dream)\w*[_-]\d+$", RegexOptions.Compiled);

    public static TranslationResult Translate(string input, string output)
    {
        if (!System.IO.File.Exists(input)) throw new System.IO.FileNotFoundException("找不到 DLL", input);
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
            if (f.HasConstant && f.Constant?.Value is string)
                scanned++;
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
        if (Map.TryGetValue(s, out translated)) return true;
        return false;
    }
}
