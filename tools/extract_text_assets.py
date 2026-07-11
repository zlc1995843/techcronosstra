from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
from pathlib import Path

import UnityPy


JAPANESE_RE = re.compile(r"[\u3040-\u30ff\u3400-\u9fff]")


def default_cache_root() -> Path:
    return (
        Path.home()
        / "AppData"
        / "LocalLow"
        / "Unity"
        / "jp_co_fanzagames_テクロノスX"
    )


def is_unity_bundle(path: Path) -> bool:
    try:
        with path.open("rb") as handle:
            return handle.read(7) == b"UnityFS"
    except OSError:
        return False


def decode_script(value: object) -> str:
    if isinstance(value, bytes):
        return value.decode("utf-8", errors="ignore")
    return str(value)


def extract(cache_root: Path, max_size: int) -> list[dict[str, object]]:
    records: list[dict[str, object]] = []
    candidates = [
        path
        for path in cache_root.rglob("*")
        if path.is_file()
        and path.stat().st_size <= max_size
        and is_unity_bundle(path)
    ]
    print(f"Scanning {len(candidates)} Unity bundles...")
    for index, path in enumerate(candidates, 1):
        try:
            environment = UnityPy.load(str(path))
            for obj in environment.objects:
                if obj.type.name != "TextAsset":
                    continue
                data = obj.read()
                text = decode_script(data.m_Script)
                if not JAPANESE_RE.search(text):
                    continue
                records.append(
                    {
                        "name": data.m_Name,
                        "text": text,
                        "sha256": hashlib.sha256(text.encode("utf-8")).hexdigest(),
                        "cache_file": str(path.relative_to(cache_root)),
                    }
                )
        except Exception:
            continue
        if index % 500 == 0:
            print(f"  {index}/{len(candidates)} bundles, {len(records)} text assets")
    records.sort(key=lambda item: (str(item["name"]), str(item["sha256"])))
    return records


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--cache-root", type=Path, default=default_cache_root())
    parser.add_argument("--max-size-mb", type=int, default=2)
    parser.add_argument(
        "--output",
        type=Path,
        default=Path(".work/extracted_textassets.json"),
    )
    args = parser.parse_args()
    if not args.cache_root.is_dir():
        raise SystemExit(f"Cache directory not found: {args.cache_root}")
    records = extract(args.cache_root, args.max_size_mb * 1024 * 1024)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(records, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    print(f"Saved {len(records)} text assets to {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
