# Forum post for forums.getpaint.net → "Plugins - Publishing ONLY!"

**Suggested topic title:**
```
Macro API — record/replay anything + HTTP API for scripting (v1.1.0)
```

**Suggested topic prefix/category:** `Effects`

---

## Body (paste this as the first post — formatting is BBCode-friendly markdown)

Hi everyone,

This is my first plugin for Paint.NET. It adds two things the app has been missing for a long time:

**1. A macro recorder & player.** Click Record, do whatever you want in Paint.NET (paint, apply effects, change layers, make selections), click Stop. The macro is saved and you can replay it later — pixel-perfect, because each step captures a full bitmap snapshot of the touched layers. Disable steps, reorder them, undo a playback in one click.

**2. A local HTTP API.** While the plugin window is open, a tiny server listens on `http://127.0.0.1:8787`. From any external tool — Python, curl, n8n, your own scripts — you can read the document/layer state, apply effects (built-in **or third-party**), draw, manage layers, and trigger saved macros.

This is meant for batch processing, automation pipelines, integration with AI tooling, custom hotkeys, etc. — but the macro recorder alone is useful for anyone who repeats the same sequence often.

---

### Where to find it / Direct download

GitHub repository (source + releases): https://github.com/fabienlacaze/PaintNetMacroApi
Latest release (v1.1.0): https://github.com/fabienlacaze/PaintNetMacroApi/releases/tag/v1.1.0
Direct DLL/installer zip: https://github.com/fabienlacaze/PaintNetMacroApi/releases/download/v1.1.0/PaintNetMacroApi-v1.1.0.zip

### Install

The zip contains an `Install.bat` that self-elevates and copies the DLL into `Effects\`. Manual install also works — just drop `PaintNetMacroApi.dll` into `C:\Program Files\paint.net\Effects\`.

After installing, open Paint.NET and look for **Effects → Tools → Macro API**.

### Requirements

- Paint.NET **5.x** (tested on 5.1.12 — does **not** work on 4.x because the recorder uses internal types that exist only in 5.x)
- Windows 10 / 11 x64

### Key features

- ▶ Record / Stop / Play any sequence of actions
- ↶ One-click Undo of a playback (restores pre-Play canvas state)
- ✏️ Step editor: enable/disable, reorder, delete individual steps inside a saved macro
- 🌐 HTTP API on `127.0.0.1:8787` for external scripting
- 📋 Lists every effect installed (built-in **and** third-party plugins) via `GET /effects`
- ⊟ Compact mode: collapse the window to a small floating bar
- 💾 Macros stored as JSON in `%AppData%\PaintNetMacroApi\macros\` — easy to share or version-control

### Quick scripting example (Python)

```python
import requests
B = "http://127.0.0.1:8787"

# What's open?
print(requests.get(B + "/document").json())

# Add a layer and apply Gaussian Blur to the active one
requests.post(B + "/layer", json={"name": "FX"})
requests.post(B + "/effect/apply", json={
    "type": "PaintDotNet.Effects.GaussianBlurEffect",
    "token": {"Radius": 8}
})
```

### Known limitations

- Macros store full PNG snapshots per step → ~50–250 KB per action. Large macros on a 4K canvas grow quickly. Future work: differential / WebP encoding.
- The action recorder uses reflection on internal Paint.NET types (`HistoryStack`, `NewHistoryMemento`). If a future Paint.NET version renames those, recording new actions will silently stop working until I update — please let me know if it breaks for your version. The HTTP API and replay path are not affected by that.
- The plugin opens a local HTTP listener. Some antivirus products may warn about a .NET DLL opening a port. It only listens on `127.0.0.1` — never on a network interface.

### License

MIT — full source on GitHub. Feel free to fork, contribute, file issues, or just use it.

Feedback very welcome. If you hit a bug or have an API endpoint you'd like added, open an issue on GitHub or reply here.

— fabidou

---

## Tips before posting

1. **Read the forum's posting rules** (sticky in the same section). They expect at least: download link, license, what it does, screenshots.
2. **Add 1–2 screenshots** before posting — drag-drop them into the editor. Recommended:
   - The Macro API window with a few macros in the list and an action sequence visible
   - The plugin entry inside the Effects → Tools menu
3. **Pick the right category** — "Plugins - Publishing ONLY!" sub-forum, with the [Effects] prefix tag.
4. **Don't bump or repost.** When you ship a new version, edit the FIRST post of the topic to update the version + changelog, then add a reply saying "v1.X.0 released" so subscribers get notified.
5. The forum allows BBCode/markdown-ish formatting. Headings render fine. Bullet lists too.
