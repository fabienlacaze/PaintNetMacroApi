"""End-to-end API tests for PaintNetMacroApi.
Run while Paint.NET is open with the Macro API window visible.
"""
import json
import sys
import time
import urllib.request
import urllib.error

BASE = "http://127.0.0.1:8787"

GREEN = "\033[92m"
RED   = "\033[91m"
YELL  = "\033[93m"
GRAY  = "\033[90m"
END   = "\033[0m"

results = []

def call(method: str, path: str, body=None, expect=200):
    url = BASE + path
    data = None
    headers = {}
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=5) as r:
            status = r.status
            text = r.read().decode("utf-8", errors="replace")
    except urllib.error.HTTPError as e:
        status = e.code
        text = e.read().decode("utf-8", errors="replace")
    except Exception as e:
        return ("ERR", str(e))
    return (status, text)

def test(name: str, method: str, path: str, body=None, expect_status=200, expect_in_body=None):
    status, text = call(method, path, body, expect_status)
    ok = (status == expect_status)
    if expect_in_body is not None and ok:
        ok = expect_in_body in text
    color = GREEN if ok else RED
    icon = "PASS" if ok else "FAIL"
    snippet = text[:150].replace("\n", " ") if isinstance(text, str) else str(text)
    print(f"{color}[{icon}]{END} {method:6s} {path:30s} -> {status}   {GRAY}{snippet}{END}")
    results.append((name, ok, status, text))
    return text

print(f"\n=== Probing {BASE} ===\n")
status, _ = call("GET", "/")
if status == "ERR":
    print(f"{RED}Server unreachable. Make sure Paint.NET + Macro API window are open.{END}")
    sys.exit(1)

print("--- READ endpoints ---")
test("document",  "GET", "/document")
test("layers",    "GET", "/layers")
test("effects",   "GET", "/effects")

print("\n--- WRITE endpoints ---")
test("layer-add", "POST", "/layer", {"name": "TestLayer"})
test("layers2",   "GET",  "/layers")
test("sel-rect",  "POST", "/selection/rect", {"x": 10, "y": 10, "w": 50, "h": 50})
test("sel-clear", "POST", "/selection/clear")
test("draw-rect", "POST", "/draw/rect", {"x": 0, "y": 0, "w": 100, "h": 100, "color": "#ff0000"})

print("\n--- RECORD/REPLAY ---")
# Cannot trigger Record from API (it's UI-only currently). Skip recorder check.

print("\n=== SUMMARY ===")
passed = sum(1 for _, ok, _, _ in results if ok)
total = len(results)
color = GREEN if passed == total else (YELL if passed > 0 else RED)
print(f"{color}{passed}/{total} passed{END}\n")

# Dump full failures for debugging
fails = [(n, s, t) for n, ok, s, t in results if not ok]
if fails:
    print(f"{RED}=== FAILURES (full bodies) ==={END}")
    for n, s, t in fails:
        print(f"\n--- {n} (status {s}) ---")
        print(t[:2000])

sys.exit(0 if passed == total else 1)
