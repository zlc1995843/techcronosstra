from __future__ import annotations

import argparse
import json
import re
from pathlib import Path


STORY_CONTEXT_RE = re.compile(
    r"^(?:script:)?andoromeda_leg_2026_01_(?:mc|ac)_(?:EP01|EP02|H01)"
)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, default=Path(".work/latest_character_source.json"))
    parser.add_argument("--translations", type=Path, default=Path("translations/zh-Hans.json"))
    parser.add_argument("--output", type=Path, default=Path("translations/zh-Hans-story.json"))
    args = parser.parse_args()

    source_items = json.loads(args.source.read_text(encoding="utf-8"))
    translated_payload = json.loads(args.translations.read_text(encoding="utf-8"))
    translated = translated_payload["translations"]

    story_sources = {
        item["source"]
        for item in source_items
        if any(STORY_CONTEXT_RE.match(context) for context in item.get("contexts", []))
    }
    missing = sorted(story_sources.difference(translated))
    if missing:
        raise SystemExit(f"Missing {len(missing)} story translations")

    payload = {
        "meta": {
            "game": "Techcronoss X",
            "character": "Andromeda (Swimsuit)",
            "language": "zh-Hans",
            "generator": translated_payload.get("meta", {}).get("generator", "DeepSeek-V4-Flash"),
        },
        "translations": {source: translated[source] for source in sorted(story_sources)},
    }
    args.output.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    print(f"Saved {len(payload['translations'])} latest-character story entries to {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
