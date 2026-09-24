#!/usr/bin/env python3
"""Release helper for the GitHub Actions workflow.

  meta      stamp version, owner and timestamp into the published meta.json and
            write the changelog to a release-notes file
  manifest  add the new version (download URL + MD5 checksum) to manifest.json,
            the file Jellyfin reads when this repository is added as a plugin repository
"""
import argparse
import datetime
import hashlib
import json
import os


def now_utc():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def cmd_meta(a):
    with open(a.file, encoding="utf-8") as f:
        meta = json.load(f)
    meta["version"] = a.version
    meta["owner"] = a.owner
    meta["timestamp"] = now_utc()
    with open(a.file, "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)
        f.write("\n")
    with open(a.notes, "w", encoding="utf-8") as f:
        f.write(meta.get("changelog", "") + "\n")


def cmd_manifest(a):
    with open(a.meta, encoding="utf-8") as f:
        meta = json.load(f)
    with open(a.zip, "rb") as f:
        checksum = hashlib.md5(f.read()).hexdigest()

    manifest = []
    if os.path.exists(a.manifest):
        with open(a.manifest, encoding="utf-8") as f:
            manifest = json.load(f)

    guid = meta["guid"].lower()
    package = next((p for p in manifest if p.get("guid", "").lower() == guid), None)
    if package is None:
        package = {"guid": meta["guid"], "versions": []}
        manifest.append(package)

    # Package details always follow the latest release.
    package.update({
        "name": meta["name"],
        "description": meta.get("description", ""),
        "overview": meta.get("overview", ""),
        "owner": meta.get("owner", ""),
        "category": meta.get("category", "General"),
        "imageUrl": f"https://raw.githubusercontent.com/{a.repo}/{a.branch}/Jellyfin.Plugin.RadioBrowser/Images/thumb.png",
    })

    versions = [v for v in package.get("versions", []) if v.get("version") != meta["version"]]
    versions.insert(0, {
        "version": meta["version"],
        "changelog": meta.get("changelog", ""),
        "targetAbi": meta["targetAbi"],
        "sourceUrl": f"https://github.com/{a.repo}/releases/download/{a.tag}/{os.path.basename(a.zip)}",
        "checksum": checksum,
        "timestamp": meta.get("timestamp") or now_utc(),
    })
    package["versions"] = versions

    with open(a.manifest, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")
    print(f"manifest: {meta['name']} {meta['version']} md5={checksum}")


def main():
    p = argparse.ArgumentParser()
    sub = p.add_subparsers(dest="cmd", required=True)
    m = sub.add_parser("meta")
    m.add_argument("--file", required=True)
    m.add_argument("--version", required=True)
    m.add_argument("--owner", required=True)
    m.add_argument("--notes", required=True)
    m.set_defaults(func=cmd_meta)
    r = sub.add_parser("manifest")
    r.add_argument("--manifest", required=True)
    r.add_argument("--meta", required=True)
    r.add_argument("--zip", required=True)
    r.add_argument("--repo", required=True)
    r.add_argument("--branch", required=True)
    r.add_argument("--tag", required=True)
    r.set_defaults(func=cmd_manifest)
    a = p.parse_args()
    a.func(a)


if __name__ == "__main__":
    main()
