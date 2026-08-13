using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CabbyCodesTranslator;

public readonly record struct TranslationResult(int Scanned, int Replaced);

public static class Translator
{
    private static readonly Dictionary<string, string> Map = TranslationCatalog.Load();
    private static readonly Regex InternalId = new(
        @"^(Crossroads|Fungus\d*|Mines|Ruins|Deepnest|Abyss|RestingGrounds|City|Waterways|Cliffs|Grimm|Hive|KingsPass|Dream)\w*[_-]\d+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Placeholder = new(
        @"\{\d+(?:,[^}]*)?(?::[^}]*)?\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RichTextTag = new(
        @"<[^>]+>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> UiSetterNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "set_text", "set_title", "set_label", "set_description", "set_message",
        "set_tooltip", "set_caption", "set_header", "set_content",
        "SetText", "SetTitle", "SetLabel", "SetDescription", "SetMessage",
        "SetTooltip", "SetCaption", "SetHeader", "SetContent",
        "ShowText", "ShowMessage", "DisplayText", "DisplayMessage"
    };

    public static TranslationResult Translate(string input, string output)
    {
        if (!File.Exists(input)) throw new FileNotFoundException("找不到 DLL", input);
        if (string.Equals(Path.GetFullPath(input), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("输出 DLL 不能覆盖原文件，请选择新文件名。");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        var module = ModuleDefMD.Load(input);
        int scanned = 0, replaced = 0;
        try
        {
            foreach (var type in module.Types)
                Walk(type, ref scanned, ref replaced);
            module.Write(output);
        }
        finally
        {
            module.Dispose();
        }
        return new TranslationResult(scanned, replaced);
    }

    private static void Walk(TypeDef type, ref int scanned, ref int replaced)
    {
        foreach (var method in type.Methods)
        {
            if (!method.HasBody) continue;
            var instructions = method.Body.Instructions;
            for (int i = 0; i < instructions.Count; i++)
            {
                if (instructions[i].OpCode.Code != Code.Ldstr || instructions[i].Operand is not string s)
                    continue;

                scanned++;
                if (!IsSafeUiLiteral(s) || !FlowsToUiSink(instructions, i))
                    continue;

                if (TryTranslate(s, out var translated))
                {
                    instructions[i].Operand = translated;
                    replaced++;
                }
            }
        }

        foreach (var nested in type.NestedTypes)
            Walk(nested, ref scanned, ref replaced);
    }

    private static bool FlowsToUiSink(IList<Instruction> instructions, int start)
    {
        const int maxLookAhead = 10;
        for (int j = start + 1; j < instructions.Count && j <= start + maxLookAhead; j++)
        {
            var operand = instructions[j].Operand;
            string? methodName = operand switch
            {
                IMethod m => m.Name,
                MethodSpec ms => ms.Method?.Name,
                _ => null
            };

            if (methodName is null) continue;
            if (UiSetterNames.Contains(methodName)) return true;

            var fullName = operand switch
            {
                IMethod m => m.FullName,
                MethodSpec ms => ms.Method?.FullName ?? string.Empty,
                _ => string.Empty
            };

            if (fullName.Contains("TMPro.TMP_Text", StringComparison.OrdinalIgnoreCase) ||
                fullName.Contains("TextMeshPro", StringComparison.OrdinalIgnoreCase) ||
                fullName.Contains("UnityEngine.UI.Text", StringComparison.OrdinalIgnoreCase))
            {
                return methodName.Contains("text", StringComparison.OrdinalIgnoreCase) ||
                       methodName.Contains("title", StringComparison.OrdinalIgnoreCase) ||
                       methodName.Contains("label", StringComparison.OrdinalIgnoreCase);
            }
        }
        return false;
    }

    private static bool IsSafeUiLiteral(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        if (s.Length > 4000) return false;
        if (InternalId.IsMatch(s)) return false;
        if (LooksLikeTechnicalKey(s)) return false;
        return true;
    }

    private static bool LooksLikeTechnicalKey(string s)
    {
        if (s.Contains("::", StringComparison.Ordinal) || s.Contains("\\", StringComparison.Ordinal)) return true;
        if (s.StartsWith("//", StringComparison.Ordinal) || s.StartsWith("/*", StringComparison.Ordinal)) return true;
        if (s.StartsWith("[", StringComparison.Ordinal) && s.Contains("]:", StringComparison.Ordinal)) return true;
        if (s.Contains("PlayerData_", StringComparison.OrdinalIgnoreCase) ||
            s.Contains("SceneMapData", StringComparison.OrdinalIgnoreCase) ||
            s.Contains("SceneInstances", StringComparison.OrdinalIgnoreCase) ||
            s.Contains("KeyCode.", StringComparison.OrdinalIgnoreCase)) return true;

        if (s.Contains("{0}", StringComparison.Ordinal) || s.Contains("{1}", StringComparison.Ordinal))
            return s.Contains("sceneName|x|y", StringComparison.OrdinalIgnoreCase) ||
                   s.Contains("field", StringComparison.OrdinalIgnoreCase) ||
                   s.Contains("config", StringComparison.OrdinalIgnoreCase) ||
                   s.Contains("debug", StringComparison.OrdinalIgnoreCase) ||
                   s.Contains("diagnostic", StringComparison.OrdinalIgnoreCase);

        if (s.Count(c => c == '_') >= 2 && !s.Contains(' ')) return true;
        if (!s.Contains(' ') && s.Contains('.') && !s.EndsWith(".", StringComparison.Ordinal)) return true;
        return false;
    }

    private static bool TryTranslate(string source, out string translated)
    {
        translated = source;
        if (!Map.TryGetValue(source, out var candidate)) return false;
        if (!FormatTokensMatch(source, candidate)) return false;
        if (source.Count(c => c == '\n') != candidate.Count(c => c == '\n')) return false;
        translated = candidate;
        return true;
    }

    private static bool FormatTokensMatch(string source, string translated)
    {
        var sourceTags = RichTextTag.Matches(source).Select(m => m.Value).ToArray();
        var translatedTags = RichTextTag.Matches(translated).Select(m => m.Value).ToArray();
        if (!sourceTags.SequenceEqual(translatedTags, StringComparer.Ordinal)) return false;

        var sourcePlaceholders = Placeholder.Matches(source).Select(m => m.Value).ToArray();
        var translatedPlaceholders = Placeholder.Matches(translated).Select(m => m.Value).ToArray();
        return sourcePlaceholders.SequenceEqual(translatedPlaceholders, StringComparer.Ordinal);
    }
}
