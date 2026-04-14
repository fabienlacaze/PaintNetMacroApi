using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PaintDotNet.Effects;

namespace PaintNetMacroApi.Core;

// Scans all loaded assemblies for classes deriving from PaintDotNet.Effects.Effect.
// Built-in and third-party effects (BoltBait, pyrochild, etc.) are loaded by the
// host before our plugin runs, so they are already present in the AppDomain.
public static class EffectRegistry
{
    public static IReadOnlyList<EffectInfo> Enumerate()
    {
        var results = new List<EffectInfo>();
        var effectBase = typeof(Effect);

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t is not null).ToArray()!; }
            catch { continue; }

            foreach (var t in types)
            {
                if (t is null || t.IsAbstract) continue;
                if (!effectBase.IsAssignableFrom(t)) continue;

                Effect? instance = null;
                try { instance = (Effect?)Activator.CreateInstance(t); }
                catch { /* some effects require args or a live doc — skip silently */ }

                var name = instance?.Name ?? t.Name;
                string? sub = null;
                bool configurable = false;
                try
                {
                    // TODO: SubMenuName is an internal property on some PDN versions.
                    var subProp = t.GetProperty("SubMenuName",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (subProp is not null && instance is not null)
                        sub = subProp.GetValue(instance) as string;
                }
                catch { }
                try
                {
                    if (instance is not null)
                        configurable = (instance.Options.Flags & EffectFlags.Configurable) != 0;
                }
                catch { }

                results.Add(new EffectInfo(t.AssemblyQualifiedName ?? t.FullName ?? t.Name, name, sub, configurable));
            }
        }

        return results;
    }

    public static Type? ResolveType(string fullyQualifiedName)
    {
        var t = Type.GetType(fullyQualifiedName);
        if (t is not null) return t;
        // Fallback: search loaded assemblies by full name.
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            t = asm.GetType(fullyQualifiedName);
            if (t is not null) return t;
        }
        return null;
    }
}
