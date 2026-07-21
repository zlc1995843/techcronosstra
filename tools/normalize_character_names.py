from __future__ import annotations

import argparse
import http.client
import json
import os
import random
import re
import time
import urllib.error
import urllib.request
from collections import Counter
from pathlib import Path


API_URL = "https://api.deepseek.com/chat/completions"
MARKUP = re.compile(r"(<[^>]*>|\{[^{}]*\})")
KATAKANA = "\u30a0-\u30ff"
SYSTEM_PROMPT = """You are a terminology consistency checker for Japanese-to-Simplified-Chinese game translations.
For every item, inspect the Japanese source, current Chinese translation, and required canonical character-name mappings.
Return ONLY exact substring replacements needed to make character names in the current translation use the required canonical Chinese names.

Rules:
1. Do not rewrite, polish, insert, or delete any other text. Only report character-name spelling variants or Japanese names that must be replaced.
2. A replacement's `from` must be an exact substring of `current`, and `to` must exactly equal one of the provided canonical Chinese names for that item.
3. Never replace text inside <...> or {...} markup.
4. Do not replace pronouns, common nouns, titles, or player-name placeholders.
5. Include every item id, using an empty replacements array when no change is needed.
6. Output valid JSON only: {"items":[{"id":0,"replacements":[{"from":"...","to":"..."}]}]}.
"""
FIXED_ALIASES = {
    "オク": ("奥",),
    "ガオ": ("高", "嗷"),
    "イズナ": ("伊祖娜",),
    "アーサー": ("阿瑟",),
}


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def save_json(path: Path, payload) -> None:
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    temporary.replace(path)


def outside_markup(value: str) -> str:
    return "".join(" " if MARKUP.fullmatch(part) else part for part in MARKUP.split(value))


def replace_outside_markup(value: str, source: str, target: str) -> tuple[str, int]:
    parts = MARKUP.split(value)
    changed = 0
    for index, part in enumerate(parts):
        if not part or MARKUP.fullmatch(part):
            continue
        count = part.count(source)
        if count:
            parts[index] = part.replace(source, target)
            changed += count
    return "".join(parts), changed


def replace_alias_outside_markup(value: str, source: str, target: str) -> tuple[str, int]:
    parts = MARKUP.split(value)
    changed = 0
    for index, part in enumerate(parts):
        if not part or MARKUP.fullmatch(part):
            continue
        protected = part.split(target)
        for protected_index in range(0, len(protected), 2):
            count = protected[protected_index].count(source)
            if count:
                protected[protected_index] = protected[protected_index].replace(source, target)
                changed += count
        parts[index] = target.join(protected)
    return "".join(parts), changed


def exact_name_pattern(name: str) -> re.Pattern[str]:
    honorific = r"(?:チャン|サン|クン)"
    return re.compile(
        rf"(?<![{KATAKANA}]){re.escape(name)}(?={honorific}|[^{KATAKANA}]|$)"
    )


def source_names(source: str, names: dict[str, str]) -> list[tuple[str, str]]:
    plain = outside_markup(source)
    result: list[tuple[str, str]] = []
    for japanese, chinese in names.items():
        if exact_name_pattern(japanese).search(plain):
            result.append((japanese, chinese))
    return result


def deterministic_japanese_replacements(
    translation: str, expected: list[tuple[str, str]]
) -> tuple[str, Counter[tuple[str, str]]]:
    updated = translation
    changes: Counter[tuple[str, str]] = Counter()
    for japanese, chinese in sorted(expected, key=lambda item: len(item[0]), reverse=True):
        parts = MARKUP.split(updated)
        for index, part in enumerate(parts):
            if not part or MARKUP.fullmatch(part):
                continue
            replaced, count = exact_name_pattern(japanese).subn(chinese, part)
            if count:
                parts[index] = replaced
                changes[(japanese, chinese)] += count
        updated = "".join(parts)
    return updated, changes


def batches(items: list[dict[str, object]], max_items: int, max_chars: int):
    batch: list[dict[str, object]] = []
    characters = 0
    for item in items:
        size = len(str(item["source"])) + len(str(item["current"]))
        if batch and (len(batch) >= max_items or characters + size > max_chars):
            yield batch
            batch = []
            characters = 0
        batch.append(item)
        characters += size
    if batch:
        yield batch


def request_replacements(api_key: str, model: str, batch: list[dict[str, object]]):
    request_items = []
    for index, item in enumerate(batch):
        request_items.append(
            {
                "id": index,
                "source": item["source"],
                "current": item["current"],
                "canonical_names": [
                    {"ja": japanese, "zh": chinese}
                    for japanese, chinese in item["expected"]
                ],
            }
        )
    body = json.dumps(
        {
            "model": model,
            "temperature": 0,
            "response_format": {"type": "json_object"},
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {
                    "role": "user",
                    "content": json.dumps({"items": request_items}, ensure_ascii=False),
                },
            ],
        },
        ensure_ascii=False,
    ).encode("utf-8")
    request = urllib.request.Request(
        API_URL,
        data=body,
        headers={
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
        },
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=180) as response:
        payload = json.load(response)
    parsed = json.loads(payload["choices"][0]["message"]["content"])
    result = {int(item["id"]): item.get("replacements", []) for item in parsed["items"]}
    if set(result) != set(range(len(batch))):
        raise ValueError(f"Incomplete response: {len(result)}/{len(batch)}")
    return result


def apply_validated_replacements(
    current: str,
    expected: list[tuple[str, str]],
    replacements: list[dict[str, object]],
) -> tuple[str, Counter[tuple[str, str]]]:
    allowed_targets = {chinese for _, chinese in expected}
    updated = current
    changes: Counter[tuple[str, str]] = Counter()
    for replacement in replacements:
        source = str(replacement.get("from", ""))
        target = str(replacement.get("to", ""))
        if not source or len(source) < 2 or target not in allowed_targets or source == target:
            continue
        if source not in outside_markup(updated):
            continue
        candidate, count = replace_outside_markup(updated, source, target)
        if not count:
            continue
        updated = candidate
        changes[(source, target)] += count
    return updated, changes


def apply_alias_catalog(
    translations: dict[str, str],
    names: dict[str, str],
    report: dict[str, object],
) -> Counter[tuple[str, str]]:
    aliases: dict[str, set[str]] = {}
    for section in ("deterministic", "model"):
        for item in report.get(section, []):
            source = str(item.get("from", ""))
            target = str(item.get("to", ""))
            if source and target and source != target:
                aliases.setdefault(target, set()).add(source)

    changes: Counter[tuple[str, str]] = Counter()
    for source, current in list(translations.items()):
        expected = source_names(source, names)
        expected_targets = {chinese for _, chinese in expected}
        updated = current
        for japanese, target in expected:
            for alias in FIXED_ALIASES.get(japanese, ()):
                updated, count = replace_alias_outside_markup(updated, alias, target)
                if count:
                    changes[(alias, target)] += count
        for target in expected_targets:
            for alias in sorted(aliases.get(target, ()), key=len, reverse=True):
                updated, count = replace_alias_outside_markup(updated, alias, target)
                if count:
                    changes[(alias, target)] += count
        translations[source] = updated
    return changes


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--translation", type=Path, default=Path("translations/zh-Hans.json"))
    parser.add_argument("--sources", type=Path, default=Path(".work/source_strings.json"))
    parser.add_argument("--model", default="deepseek-v4-flash")
    parser.add_argument("--max-items", type=int, default=45)
    parser.add_argument("--max-chars", type=int, default=12000)
    parser.add_argument("--attempts", type=int, default=6)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--apply-report", type=Path)
    args = parser.parse_args()

    payload = load_json(args.translation)
    translations: dict[str, str] = payload["translations"]
    names: dict[str, str] = payload["character_names"]
    source_rows = load_json(args.sources)
    contexts = {
        str(item["source"]): [str(context) for context in item.get("contexts", [])]
        for item in source_rows
    }

    if args.apply_report:
        report = load_json(args.apply_report)
        changes = apply_alias_catalog(translations, names, report)
        save_json(args.translation, payload)
        print(
            f"catalog_replacements={sum(changes.values())} "
            f"aliases={len(changes)} saved={args.translation}",
            flush=True,
        )
        return 0

    candidates: list[dict[str, object]] = []
    deterministic_changes: Counter[tuple[str, str]] = Counter()
    for source, current in list(translations.items()):
        expected = source_names(source, names)
        if not expected:
            continue
        updated, changes = deterministic_japanese_replacements(current, expected)
        if changes:
            translations[source] = updated
            deterministic_changes.update(changes)
            current = updated
        missing = [(ja, zh) for ja, zh in expected if zh not in outside_markup(current)]
        if missing:
            candidates.append(
                {
                    "source": source,
                    "current": current,
                    "expected": missing,
                    "contexts": contexts.get(source, []),
                }
            )

    print(
        f"deterministic={sum(deterministic_changes.values())} "
        f"candidates={len(candidates)} model={args.model}",
        flush=True,
    )
    if args.dry_run:
        for (source, target), count in deterministic_changes.most_common():
            print(f"{source} -> {target}: {count}")
        return 0

    api_key = os.environ.get("DEEPSEEK_API_KEY", "").strip()
    if not api_key:
        raise SystemExit("DEEPSEEK_API_KEY is not set")

    model_changes: Counter[tuple[str, str]] = Counter()
    work = list(batches(candidates, args.max_items, args.max_chars))
    for batch_index, batch in enumerate(work, 1):
        for attempt in range(1, args.attempts + 1):
            try:
                result = request_replacements(api_key, args.model, batch)
                for index, item in enumerate(batch):
                    source = str(item["source"])
                    updated, changes = apply_validated_replacements(
                        translations[source], item["expected"], result[index]
                    )
                    translations[source] = updated
                    model_changes.update(changes)
                print(
                    f"batch={batch_index}/{len(work)} "
                    f"replacements={sum(model_changes.values())}",
                    flush=True,
                )
                break
            except (
                OSError,
                ValueError,
                KeyError,
                json.JSONDecodeError,
                urllib.error.HTTPError,
                http.client.IncompleteRead,
            ) as error:
                if attempt == args.attempts:
                    raise
                delay = min(60, 2**attempt + random.random() * 2)
                print(
                    f"batch={batch_index} attempt={attempt} failed={error}; "
                    f"retry={delay:.1f}s",
                    flush=True,
                )
                time.sleep(delay)

    payload.setdefault("meta", {})["name_normalizer"] = args.model
    save_json(args.translation, payload)
    report = {
        "deterministic": [
            {"from": source, "to": target, "count": count}
            for (source, target), count in deterministic_changes.most_common()
        ],
        "model": [
            {"from": source, "to": target, "count": count}
            for (source, target), count in model_changes.most_common()
        ],
        "candidate_count": len(candidates),
    }
    catalog_changes = apply_alias_catalog(translations, names, report)
    report["catalog"] = [
        {"from": source, "to": target, "count": count}
        for (source, target), count in catalog_changes.most_common()
    ]
    save_json(args.translation, payload)
    report_path = Path(".work/name_normalization_report.json")
    save_json(report_path, report)
    print(
        f"saved={args.translation} model_replacements={sum(model_changes.values())} "
        f"report={report_path}",
        flush=True,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
