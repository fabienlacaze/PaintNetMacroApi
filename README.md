# PaintNetMacroApi

Plugin Paint.NET qui expose une API HTTP locale + UI pour enregistrer, éditer
et rejouer des macros — **y compris les effets fournis par des plugins tiers
installés manuellement** (BoltBait Pack, pyrochild, dpy, etc.).

## Structure

```
PaintNetMacroApi/
├── PaintNetMacroApi.csproj
├── src/
│   ├── MacroApiEffect.cs              # Entrée : Effect qui démarre serveur + UI
│   ├── Api/
│   │   ├── ApiServer.cs               # HttpListener sur http://127.0.0.1:8787
│   │   ├── Router.cs                  # Dispatch endpoints
│   │   └── Endpoints/
│   │       ├── DocumentEndpoints.cs   # /document, /canvas
│   │       ├── LayerEndpoints.cs      # /layers, /layer/{id}
│   │       ├── SelectionEndpoints.cs  # /selection/*
│   │       ├── EffectEndpoints.cs     # /effects (list), /effect/{fqn} (apply)
│   │       ├── FileEndpoints.cs       # /open, /save, /export
│   │       └── DrawEndpoints.cs       # /draw/line, /draw/rect, /draw/pixels
│   ├── Core/
│   │   ├── PaintDotNetBridge.cs       # Accès AppWorkspace / ActiveDocument
│   │   ├── UiInvoker.cs               # Marshal sur thread UI (Control.Invoke)
│   │   ├── EffectRegistry.cs          # Découverte de TOUS les effects (built-in + tiers)
│   │   ├── TokenSerializer.cs         # Sérialise ConfigToken d'un effect en JSON
│   │   └── Models.cs                  # DTOs JSON
│   ├── Macros/
│   │   ├── Macro.cs                   # Liste ordonnée d'ApiCall
│   │   ├── MacroRecorder.cs           # Observe HistoryStack + API calls
│   │   ├── HistoryListener.cs         # Hook sur Document.History pour capter actions UI
│   │   ├── MacroPlayer.cs             # Rejoue via Router (même chemin que l'API)
│   │   └── MacroStore.cs              # JSON dans %AppData%/PaintNetMacroApi/macros/
│   └── Ui/
│       ├── MacroWindow.cs             # Fenêtre principale : liste + Record/Stop/Play
│       ├── MacroEditor.cs             # Édition step-by-step (JSON + form)
│       └── EffectPicker.cs            # Liste des effects disponibles (built-in + tiers)
└── docs/
    ├── API.md
    └── MACRO_FORMAT.md
```

## Comment ça marche pour les plugins tiers

1. **Découverte** : au démarrage, `EffectRegistry` énumère tous les `Effect`
   chargés par Paint.NET (pas juste les built-in). Chacun expose `Name`,
   `SubMenu`, son `Type` .NET et sa capacité à créer un `ConfigDialog`.
2. **Exécution** : `EffectEndpoints.Apply()` instancie l'effect par son
   type fully-qualified, reconstruit un `ConfigToken` à partir du JSON
   reçu (via reflection sur les propriétés publiques), puis appelle
   `effect.Render(...)` — exactement comme Paint.NET le fait en interne.
3. **Enregistrement** : quand l'utilisateur clique "Record" puis applique
   un effect via le menu normal, `HistoryListener` détecte le nouveau
   `HistoryMemento` (type `ApplyEffectHistoryMemento`), extrait le token
   via reflection et ajoute un `ApiCall` équivalent dans la macro en
   cours.

## Format de macro (JSON)

```json
{
  "name": "Bordure stylisée",
  "version": 1,
  "steps": [
    { "op": "selection.rect", "args": { "x": 0, "y": 0, "w": 100, "h": 100 } },
    { "op": "effect.apply",
      "args": {
        "type": "BoltBait.Outline.OutlineEffect, BoltBait.Outline",
        "token": { "Thickness": 3, "Color": "#000000" } } },
    { "op": "layer.flatten" }
  ]
}
```

Chaque step est identique à un appel HTTP — rejouer = re-router.

## Dépendances

- .NET 7 (Paint.NET 5.x) ou .NET Framework 4.7.2 (Paint.NET 4.x — ajuster csproj)
- Références (copier depuis `C:\Program Files\paint.net\`) :
  `PaintDotNet.Base.dll`, `PaintDotNet.Core.dll`, `PaintDotNet.Effects.dll`,
  `PaintDotNet.Data.dll`, `PaintDotNet.Framework.dll`, `PaintDotNet.Windows.Framework.dll`
- `System.Text.Json` (inclus)

## Install

Compiler → copier `PaintNetMacroApi.dll` dans
`C:\Program Files\paint.net\Effects\` → redémarrer Paint.NET →
menu **Effects → Tools → Macro API**.

## Licence

MIT. Compatible SDK Paint.NET (MIT).
