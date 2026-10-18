#!/usr/bin/env python3
"""License audit of the NuGet restore graph (ADR-017).

Fails when a package (id + version) in `dotnet list package --include-transitive`
is missing from THIRD-PARTY-NOTICES.md, or is listed with a license outside the
allowlist. When the package is in the local NuGet cache, the .nuspec license
expression must also match the notices entry.

Usage: python3 tools/license-audit.py [--graph graph.json] [solution]
"""
import glob, json, os, re, subprocess, sys

ALLOWED = {"MIT", "Apache-2.0", "BSD-2-Clause", "BSD-3-Clause", "MS-PL"}
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def load_graph(args):
    if args and args[0] == "--graph":
        return json.load(open(args[1], encoding="utf-8")), args[2:]
    sol = args[0] if args else os.path.join(ROOT, "EmlExtraction.slnx")
    out = subprocess.run(
        ["dotnet", "list", sol, "package", "--include-transitive", "--format", "json"],
        check=True, capture_output=True, text=True).stdout
    return json.loads(out), args[1:]


def graph_packages(g):
    s = set()
    for p in g["projects"]:
        for f in p.get("frameworks", []):
            for k in ("topLevelPackages", "transitivePackages"):
                for x in f.get(k, []):
                    s.add((x["id"], x["resolvedVersion"]))
    return s


def notices(path):
    rows = {}
    for line in open(path, encoding="utf-8"):
        m = re.match(r"\|\s*([^|\s]+)\s*\|\s*(\d[^|\s]*)\s*\|\s*([^|]+?)\s*\|", line)
        if m:
            rows[(m[1].lower(), m[2])] = m[3]
    return rows


def nuspec_license(pid, ver):
    base = os.environ.get("NUGET_PACKAGES") or os.path.expanduser("~/.nuget/packages")
    f = glob.glob(f"{base}/{pid.lower()}/{ver}/*.nuspec")
    if not f:
        return None
    m = re.search(r'<license type="expression">(.*?)</license>', open(f[0], encoding="utf-8").read())
    return m[1] if m else None


def main():
    g, _ = load_graph(sys.argv[1:])
    rows = notices(os.path.join(ROOT, "THIRD-PARTY-NOTICES.md"))
    errors = []
    pkgs = sorted(graph_packages(g))
    if not pkgs or any("frameworks" not in p or p.get("problems") for p in g["projects"]):
        errors.append("empty or failed restore graph (run dotnet restore first)")
    for pid, ver in pkgs:
        lic = rows.get((pid.lower(), ver))
        if lic is None:
            errors.append(f"{pid} {ver}: missing from THIRD-PARTY-NOTICES.md")
            continue
        if lic not in ALLOWED:
            errors.append(f"{pid} {ver}: license '{lic}' not in allowlist {sorted(ALLOWED)}")
        actual = nuspec_license(pid, ver)
        if actual and actual != lic:
            errors.append(f"{pid} {ver}: notices say '{lic}' but nuspec says '{actual}'")
    for e in errors:
        print(f"::error::{e}")
    print(f"license audit: {len(pkgs)} packages, {len(errors)} problem(s)")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
