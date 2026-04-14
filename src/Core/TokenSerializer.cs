using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using PaintDotNet;
using PaintDotNet.Effects;

namespace PaintNetMacroApi.Core;

// Serializes an EffectConfigToken using reflection on public instance properties.
// Paint.NET tokens are hand-written per effect; there is no common interface for
// their fields, so we probe well-known types (ColorBgra, Pair<,>, primitives, enums).
public static class TokenSerializer
{
    public static JsonObject Serialize(EffectConfigToken? token)
    {
        var obj = new JsonObject();
        if (token is null) return obj;

        foreach (var p in token.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
            object? val;
            try { val = p.GetValue(token); }
            catch { continue; }
            obj[p.Name] = ToNode(val);
        }
        return obj;
    }

    public static EffectConfigToken? Deserialize(Type tokenType, JsonElement json)
    {
        EffectConfigToken? token;
        try { token = (EffectConfigToken?)Activator.CreateInstance(tokenType); }
        catch { return null; }
        if (token is null) return null;

        foreach (var p in tokenType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanWrite || p.GetIndexParameters().Length > 0) continue;
            if (!json.TryGetProperty(p.Name, out var element)) continue;
            try
            {
                var converted = FromElement(element, p.PropertyType);
                p.SetValue(token, converted);
            }
            catch { /* ignore field mismatches to keep best-effort behavior */ }
        }
        return token;
    }

    private static JsonNode? ToNode(object? v)
    {
        if (v is null) return null;
        switch (v)
        {
            case string s: return s;
            case bool b: return b;
            case int i: return i;
            case long l: return l;
            case float f: return f;
            case double d: return d;
            case Enum e: return e.ToString();
            case ColorBgra c:
                return new JsonObject { ["b"] = c.B, ["g"] = c.G, ["r"] = c.R, ["a"] = c.A };
        }
        var t = v.GetType();
        if (t.IsGenericType && t.GetGenericTypeDefinition().Name.StartsWith("Pair"))
        {
            var first = t.GetProperty("First")?.GetValue(v);
            var second = t.GetProperty("Second")?.GetValue(v);
            return new JsonObject { ["first"] = ToNode(first), ["second"] = ToNode(second) };
        }
        // Fallback: stringify. Round-tripping non-primitive types requires per-type handlers.
        return v.ToString();
    }

    private static object? FromElement(JsonElement e, Type target)
    {
        if (target == typeof(string)) return e.GetString();
        if (target == typeof(bool)) return e.GetBoolean();
        if (target == typeof(int)) return e.GetInt32();
        if (target == typeof(long)) return e.GetInt64();
        if (target == typeof(float)) return e.GetSingle();
        if (target == typeof(double)) return e.GetDouble();
        if (target.IsEnum) return Enum.Parse(target, e.GetString() ?? "0");
        if (target == typeof(ColorBgra))
        {
            byte b = (byte)e.GetProperty("b").GetInt32();
            byte g = (byte)e.GetProperty("g").GetInt32();
            byte r = (byte)e.GetProperty("r").GetInt32();
            byte a = (byte)e.GetProperty("a").GetInt32();
            return ColorBgra.FromBgra(b, g, r, a);
        }
        if (target.IsGenericType && target.GetGenericTypeDefinition().Name.StartsWith("Pair"))
        {
            var args = target.GetGenericArguments();
            var first = FromElement(e.GetProperty("first"), args[0]);
            var second = FromElement(e.GetProperty("second"), args[1]);
            return Activator.CreateInstance(target, first, second);
        }
        // TODO: add handlers for PdnRegion, HistogramEntry, curve tokens, etc. as needed.
        return null;
    }
}
