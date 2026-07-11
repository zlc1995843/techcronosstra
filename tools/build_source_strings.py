from __future__ import annotations

import argparse
import json
import re
from collections import defaultdict
from pathlib import Path


JAPANESE_RE = re.compile(r"[\u3040-\u30ff\u3400-\u9fff]")
TEXT_BLOCK_RE = re.compile(
    r"\[\+(?:Maxro|Macro)\b(?=[^\]]*Label=セリフ)[^\]]*\]"
    r"(.*?)\[/\+(?:Maxro|Macro)\]",
    re.DOTALL | re.IGNORECASE,
)
ATTRIBUTE_RE = re.compile(
    r"\b(Name|Nickname|Text|ChapterText|TitleText)=(.*?)"
    r"(?=\s+[A-Za-z][A-Za-z0-9]*=|\s*/?\])"
)


def normalize(value: str) -> str:
    lines = [line.strip() for line in value.replace("\r\n", "\n").split("\n")]
    return "\n".join(lines).strip()


def add(found: dict[str, set[str]], text: str, context: str) -> None:
    text = normalize(text)
    if text and JAPANESE_RE.search(text) and "$TEXT" not in text:
        found[text].add(context)


def extract_script_strings(records: list[dict[str, object]]) -> dict[str, set[str]]:
    found: dict[str, set[str]] = defaultdict(set)
    seen_assets: set[str] = set()
    for record in records:
        digest = str(record["sha256"])
        if digest in seen_assets:
            continue
        seen_assets.add(digest)
        name = str(record["name"])
        script = str(record["text"])
        for match in TEXT_BLOCK_RE.finditer(script):
            add(found, match.group(1), f"script:{name}")
        for line in script.splitlines():
            if line.lstrip().startswith(("#", "//")):
                continue
            for match in ATTRIBUTE_RE.finditer(line):
                add(found, match.group(2), f"{name}:{match.group(1)}")
    return found


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--input",
        type=Path,
        default=Path(".work/extracted_textassets.json"),
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path(".work/source_strings.json"),
    )
    args = parser.parse_args()
    records = json.loads(args.input.read_text(encoding="utf-8"))
    found = extract_script_strings(records)
    payload = [
        {"source": source, "contexts": sorted(contexts)[:8]}
        for source, contexts in sorted(found.items())
    ]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    print(
        f"Saved {len(payload)} unique strings, "
        f"{sum(len(item['source']) for item in payload)} characters to {args.output}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
