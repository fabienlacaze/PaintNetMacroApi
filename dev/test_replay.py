"""Replay an existing saved macro by feeding each step to /layer/{i}/replace."""
import json, sys, urllib.request, urllib.error, glob, os, base64

BASE = "http://127.0.0.1:8787"

def call(method, path, body=None):
    data = json.dumps(body).encode() if body else None
    req = urllib.request.Request(BASE + path, data=data,
                                  headers={"Content-Type": "application/json"} if body else {},
                                  method=method)
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            return r.status, r.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()

macros_dir = os.path.expandvars(r"%AppData%\PaintNetMacroApi\macros")
files = sorted(glob.glob(os.path.join(macros_dir, "*.json")))
print(f"Found {len(files)} macro(s)")
for f in files:
    print(" -", os.path.basename(f), os.path.getsize(f), "bytes")

if not files:
    sys.exit(0)

target = files[-1]
print(f"\nReplaying: {target}\n")
with open(target, encoding="utf-8") as fh:
    macro = json.load(fh)

for i, step in enumerate(macro["Steps"]):
    op = step["Op"]
    args = step.get("Args", {})
    if op == "HISTORY /history/snapshot":
        layers = args.get("layers", [])
        print(f"step {i}: snapshot with {len(layers)} layer(s)")
        for L in layers:
            idx = L["index"]
            png = L["png"]
            print(f"   -> /layer/{idx}/replace ({len(png)} chars b64)")
            status, text = call("POST", f"/layer/{idx}/replace", {"png": png})
            print(f"      status={status} body={text[:150]}")
    elif op.startswith("HISTORY"):
        print(f"step {i}: skip {op}")
    else:
        print(f"step {i}: {op}")
