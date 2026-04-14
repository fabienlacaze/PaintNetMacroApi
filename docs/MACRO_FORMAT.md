# Macro JSON Format

Macros are stored as JSON files at
`%AppData%\PaintNetMacroApi\macros\<name>.json`.

## Schema

```json
{
  "Name": "string — must match the file name",
  "Steps": [
    {
      "Op":   "<HTTP-METHOD> <path>",
      "Args": { /* same JSON body the HTTP endpoint would accept */ }
    }
  ]
}
```

* `Op` is simply the HTTP method and path joined by a single space, e.g.
  `"POST /effect/apply"`. This is the exact method+path fed to `Router.DispatchAsync`
  during playback.
* `Args` is an arbitrary `JsonElement`. For `GET` endpoints it should be `{}`.
* `Op` with method `INTERNAL` is reserved for history-derived steps captured
  by `HistoryListener`. Those steps are skipped during playback; they exist
  only for human inspection.

## Example — third-party effect

This macro adds a new layer, draws a red circle, and applies pyrochild's
*Gradient Mapping* effect (a real third-party plugin).

```json
{
  "Name": "circle-with-gradient",
  "Steps": [
    {
      "Op": "POST /layer",
      "Args": { "Name": "drawing" }
    },
    {
      "Op": "POST /draw/ellipse",
      "Args": {
        "X": 100, "Y": 100, "Width": 200, "Height": 200,
        "Fill": true, "StrokeWidth": 0,
        "Color": { "R": 220, "G": 40, "B": 40, "A": 255 }
      }
    },
    {
      "Op": "POST /effect/apply",
      "Args": {
        "Type": "pyrochild.effects.gradientmapping.GradientMapping, GradientMapping",
        "Token": {
          "GradientName": "Fire",
          "Reverse": false,
          "Blend": 100
        }
      }
    }
  ]
}
```

The `Type` string is the fully qualified CLR type name of the effect class —
the same value returned by `GET /effects`. Use that endpoint at runtime to
discover the exact assembly-qualified names of plugins installed on the
target machine; they differ by plugin version.

## Playback semantics

* Steps run sequentially on the Paint.NET UI thread.
* By default, a failing step (`status >= 400`) aborts playback and throws.
  A "best effort" mode that logs and continues will be added later.
* `MacroPlayer.StepStarting` / `StepFinished` events fire around each step
  so the UI can show progress.
* Macros are NOT versioned. A macro recorded against a machine with a
  given set of third-party plugins may fail on a machine that doesn't have
  them installed — the effect type name won't resolve and the step returns
  `400 effect type not found`.

## Recording

1. Click **Record** in the Macro API window and give the macro a name.
2. Perform actions — via the HTTP API or, once `HistoryListener` is fully
   wired, directly in the Paint.NET GUI.
3. Click **Stop**. The macro is saved to the macros folder.

## Editing

Click **Edit** to open a JSON editor. The editor validates the document
against the `Macro` schema on save; invalid JSON is rejected with a
message box.
