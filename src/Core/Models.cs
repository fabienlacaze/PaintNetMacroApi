using System.Collections.Generic;
using System.Text.Json;

namespace PaintNetMacroApi.Core;

public readonly record struct ApiCall(string Method, string Path, string Body);

public sealed record EffectInfo(
    string FullyQualifiedTypeName,
    string DisplayName,
    string? SubMenu,
    bool HasConfigDialog);

public sealed record LayerInfo(
    int Index,
    string Name,
    bool Visible,
    byte Opacity,
    string BlendMode,
    int Width,
    int Height);

public sealed record DocumentInfo(
    int Width,
    int Height,
    int LayerCount,
    int ActiveLayerIndex,
    string? FilePath,
    double DpuX,
    double DpuY);

public sealed record MacroStep(string Op, JsonElement Args);

public sealed record Macro(string Name, List<MacroStep> Steps);
