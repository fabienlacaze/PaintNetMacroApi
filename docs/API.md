# Paint.NET Macro API — HTTP Reference

Base URL: `http://127.0.0.1:8787`
Content type: `application/json; charset=utf-8`

All endpoints return either `{ "ok": true, ... }` on success or
`{ "error": "<message>" }` with a `4xx`/`5xx` status on failure.

---

## Document

### `GET /document`
Returns info about the active document.

Response:
```json
{
  "Width": 1920,
  "Height": 1080,
  "LayerCount": 3,
  "ActiveLayerIndex": 1,
  "FilePath": null,
  "DpuX": 96.0,
  "DpuY": 96.0
}
```

### `POST /document/new`
Body: `{ "Width": 800, "Height": 600 }`

### `POST /document/open`
Body: `{ "Path": "C:\\images\\photo.png" }`

### `POST /document/save`
Body: `{ "Path": "C:\\images\\out.pdn" }`
Format is inferred from extension when the FileType plugin chain is wired up.

### `POST /document/flatten`
Flattens all visible layers into a single layer. Currently not implemented —
returns `400` until `FlattenFunction` is reflected.

---

## Layers

### `GET /layers`
Returns an array of `LayerInfo` objects ordered from bottom to top.

### `POST /layer`
Body: `{ "Name": "Sketch" }` (name optional)
Adds a new empty `BitmapLayer` at document size. Response: `{ "index": N }`.

### `DELETE /layer/{index}`
Removes the layer at `index`. Cannot delete the last remaining layer.

### `POST /layer/{index}/move`
Body: `{ "To": 0 }`
Moves a layer to a new position.

### `PATCH /layer/{index}`
Body (all fields optional):
```json
{ "Name": "Top", "Visible": true, "Opacity": 200, "BlendMode": "Multiply" }
```
BlendMode is a string; mapping to `UserBlendOp` is still TODO.

---

## Selection

### `POST /selection/rect`
Body: `{ "X": 0, "Y": 0, "Width": 100, "Height": 100 }`

### `POST /selection/clear`
Clears the current selection.

### `POST /selection/invert`
Inverts the current selection.

### `POST /selection/all`
Selects the full canvas.

Note: all selection endpoints currently rely on reflection into the internal
`Selection` type and will return 400 on Paint.NET builds where signatures differ.

---

## Effects

### `GET /effects`
Returns every `Effect`-derived class present in the AppDomain. This includes
built-in effects plus third-party plugins installed manually (BoltBait,
pyrochild, etc.).

Each entry:
```json
{
  "FullyQualifiedTypeName": "PaintDotNet.Effects.GaussianBlurEffect, PaintDotNet.Effects",
  "DisplayName": "Gaussian Blur",
  "SubMenu": "Blurs",
  "HasConfigDialog": true
}
```

### `POST /effect/apply`
Body:
```json
{
  "Type": "PaintDotNet.Effects.GaussianBlurEffect, PaintDotNet.Effects",
  "Token": { "Radius": 8 }
}
```

`Token` is a JSON object whose keys are the public property names of the
effect's `EffectConfigToken`. Unknown keys are ignored. Missing keys are
filled from `CreateDefaultConfigToken()`. Supported value types today:
`int`, `double`, `bool`, `string`, `enum` (as string), `ColorBgra`
(`{b,g,r,a}`), `Pair<T1,T2>` (`{first,second}`).

---

## Draw

All draw endpoints operate on the active `BitmapLayer` surface. Color is
`{ "R": 0, "G": 0, "B": 0, "A": 255 }`.

### `POST /draw/line`
```json
{ "X1": 0, "Y1": 0, "X2": 100, "Y2": 100, "Width": 2.0,
  "Color": { "R": 255, "G": 0, "B": 0, "A": 255 } }
```

### `POST /draw/rect` and `POST /draw/ellipse`
```json
{ "X": 10, "Y": 10, "Width": 80, "Height": 40,
  "Fill": true, "StrokeWidth": 1.0,
  "Color": { "R": 0, "G": 0, "B": 255, "A": 255 } }
```

### `POST /draw/pixels`
```json
{ "Pixels": [ { "X": 0, "Y": 0, "R": 255, "G": 255, "B": 255, "A": 255 } ] }
```
