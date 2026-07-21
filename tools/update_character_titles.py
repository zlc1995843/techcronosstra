from __future__ import annotations

import json
from pathlib import Path


TRANSLATION_PATH = Path("translations/zh-Hans.json")
SOURCE_PATH = Path(".work/source_strings.json")


def main() -> int:
    payload = json.loads(TRANSLATION_PATH.read_text(encoding="utf-8"))
    source_rows = json.loads(SOURCE_PATH.read_text(encoding="utf-8"))
    translations: dict[str, str] = payload["translations"]
    titles: dict[str, str] = {}
    for item in source_rows:
        source = str(item["source"])
        contexts = [str(context) for context in item.get("contexts", [])]
        if any(context.endswith(":Nickname") for context in contexts):
            translated = translations.get(source)
            if translated:
                titles[source] = translated

    payload["character_titles"] = dict(sorted(titles.items()))
    temporary = TRANSLATION_PATH.with_suffix(".json.tmp")
    temporary.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    temporary.replace(TRANSLATION_PATH)
    print(f"character_titles={len(titles)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
