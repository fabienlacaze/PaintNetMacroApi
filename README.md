# Paint.NET Macro API

A plugin for **Paint.NET 5.x** that adds two long-missing features:

1. A **macro recorder & player** — record what you do in Paint.NET, replay it later, pixel-perfect.
2. A local **HTTP API** (http://127.0.0.1:8787) so any external tool (Python, curl, n8n, your own scripts) can drive Paint.NET.

![Effects > Tools > Macro API](docs/screenshot-menu.png)

## Features

- ▶ **Record / Stop / Play** any sequence of UI actions (paintbrush, selection, layer ops, effects…)
- 📸 Captures **pixel-perfect bitmap snapshots** after each action, so replay is byte-identical
- 🌐 **HTTP API** for scripted automation: read document/layer state, draw shapes, apply effects, run macros
- 💾 Macros saved as **JSON** in `%AppData%\PaintNetMacroApi\macros\`
- 🎨 Modern dark UI with action sequence preview
- 🔌 Stays out of the way — server only runs while the Macro API window is open

## Installation

### Quick install (recommended)

1. Download the latest `PaintNetMacroApi-vX.Y.Z.zip` from the [Releases page](https://github.com/fabienlacaze/PaintNetMacroApi/releases)
2. Extract anywhere
3. **Right-click `Install.bat` → Run as administrator**
4. That's it — start Paint.NET and look for **Effects → Tools → Macro API**

### Manual install

If you don't want to run a `.bat`:

1. Close Paint.NET
2. Copy `PaintNetMacroApi.dll` from the zip into:
   ```
   C:\Program Files\paint.net\Effects\
   ```
   (Windows will prompt for admin rights — accept)
3. Start Paint.NET → **Effects → Tools → Macro API**

### Uninstall

Run `Uninstall.bat` as administrator, **or** simply delete `PaintNetMacroApi.dll` from `C:\Program Files\paint.net\Effects\`.

## Requirements

- **Paint.NET 5.x** (tested on 5.1.12 — older 4.x will not work)
- **Windows 10 or 11** (x64)
- .NET 9 runtime (already shipped with Paint.NET 5.x — nothing to install)

## How to use

### As a macro recorder (no scripting needed)

1. Open Paint.NET, open or create a document
2. **Effects → Tools → Macro API** — the window opens
3. Click **● Record**, give your macro a name
4. Do whatever you want in Paint.NET (paint, apply effects, add layers…)
5. Click **■ Stop** — your macro is saved automatically
6. To replay: select it in the list and click **▶ Play** (or just double-click the row)

### As an HTTP API (for scripts)

While the Macro API window is open, the server listens on `http://127.0.0.1:8787`.

**Quick examples:**

```bash
# Get current document info
curl http://127.0.0.1:8787/document

# List all available effects (built-in + third-party plugins)
curl http://127.0.0.1:8787/effects

# Add a new layer
curl -X POST http://127.0.0.1:8787/layer -H "Content-Type: application/json" \
     -d '{"name":"My layer"}'

# Make a rectangular selection
curl -X POST http://127.0.0.1:8787/selection/rect \
     -d '{"x":50,"y":50,"w":200,"h":150}'

# Draw a red ellipse
curl -X POST http://127.0.0.1:8787/draw/ellipse \
     -d '{"x":10,"y":10,"w":300,"h":300,"color":"#ff0000","fill":true}'
```

```python
import requests
BASE = "http://127.0.0.1:8787"

# Apply Gaussian Blur
requests.post(f"{BASE}/effect/apply", json={
    "type": "PaintDotNet.Effects.GaussianBlurEffect",
    "token": {"Radius": 5}
})
```

See [`docs/API.md`](docs/API.md) for the full endpoint reference and [`docs/MACRO_FORMAT.md`](docs/MACRO_FORMAT.md) for the macro JSON schema.

## Use cases

- **Batch processing**: open 200 photos, apply the same recorded macro, export each one
- **Pipeline integration**: post-process AI image output (ComfyUI, SDXL…) through Paint.NET filters
- **Tutorials**: record once, ship a `.json` macro that plays back step by step on any machine
- **Custom hotkeys**: bind a script to a shortcut that triggers a macro
- **Asset generation**: programmatically generate variations of a graphic

## Limitations / known issues

- **Macros are large** (~50–250 KB per action) — each step stores a full PNG of the touched layer. Multiple actions on a 4K canvas will quickly grow.
- **Per-pixel paintbrush capture** is via bitmap diff (not stroke replay). The visual result is identical but the macro doesn't "know" you used a brush.
- **History recording uses reflection** on internal Paint.NET types. If a future Paint.NET version renames `HistoryStack` or `NewHistoryMemento`, the recorder may stop capturing UI actions (the API itself will keep working).
- Some Windows AV products may warn about a .NET DLL that opens a local HTTP port. The plugin only listens on `127.0.0.1`, never on a network interface.

## Building from source

```bash
git clone https://github.com/fabienlacaze/PaintNetMacroApi
cd PaintNetMacroApi
dotnet build -c Release
# DLL is at: bin\Release\net9.0-windows\PaintNetMacroApi.dll
```

The `.csproj` references DLLs from `C:\Program Files\paint.net\` directly — adjust the `<HintPath>` if your install lives elsewhere.

A development helper is provided:

```bash
# Kill PDN, redeploy DLL with elevation, run API tests
./dev/dev.sh
```

## License

[MIT](LICENSE) — same spirit as the Paint.NET SDK. Use it however you want.

## Acknowledgements

- [Paint.NET](https://getpaint.net) by Rick Brewster
- Built collaboratively with assistance from Claude (Anthropic)
