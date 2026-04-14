using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PaintNetMacroApi.Core;

namespace PaintNetMacroApi.Macros;

public sealed class MacroStore
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
    };

    public string Root { get; }

    public MacroStore()
    {
        Root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PaintNetMacroApi",
            "macros");
        Directory.CreateDirectory(Root);
    }

    public IReadOnlyList<string> List()
    {
        var list = new List<string>();
        foreach (var f in Directory.EnumerateFiles(Root, "*.json"))
            list.Add(Path.GetFileNameWithoutExtension(f));
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    public Macro? Load(string name)
    {
        var path = PathFor(name);
        if (!File.Exists(path)) return null;
        using var fs = File.OpenRead(path);
        return JsonSerializer.Deserialize<Macro>(fs, Opts);
    }

    public void Save(Macro macro)
    {
        var path = PathFor(macro.Name);
        using var fs = File.Create(path);
        JsonSerializer.Serialize(fs, macro, Opts);
    }

    public void SaveRaw(string name, string json)
    {
        // validates + rewrites pretty.
        var parsed = JsonSerializer.Deserialize<Macro>(json, Opts)
            ?? throw new FormatException("invalid macro json");
        if (!string.Equals(parsed.Name, name, StringComparison.Ordinal))
            parsed = parsed with { Name = name };
        Save(parsed);
    }

    public bool Delete(string name)
    {
        var path = PathFor(name);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    private string PathFor(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            if (name.IndexOf(c) >= 0) throw new ArgumentException($"bad char in name: {c}");
        return Path.Combine(Root, name + ".json");
    }
}
