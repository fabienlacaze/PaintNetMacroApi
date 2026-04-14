namespace PaintNetMacroApi.Macros;

// Macro/MacroStep live in Core/Models.cs as the canonical definitions.
// This file is intentionally kept as a thin re-export so `PaintNetMacroApi.Macros.Macro`
// works for callers that expect the type under the Macros namespace.
using MacroType = PaintNetMacroApi.Core.Macro;
using MacroStepType = PaintNetMacroApi.Core.MacroStep;

public static class MacroAliases
{
    public static MacroType Empty(string name) => new(name, new());
}
