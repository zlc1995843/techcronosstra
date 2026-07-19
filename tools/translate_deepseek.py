from __future__ import annotations

import argparse
import http.client
import json
import os
import random
import time
import urllib.error
import urllib.request
from pathlib import Path


API_URL = "https://api.deepseek.com/chat/completions"
SYSTEM_PROMPT = """You are a professional Japanese-to-Simplified-Chinese game localizer.
Translate dialogue and UI strings naturally and concisely for a fantasy/adult Japanese game.
Rules:
1. Preserve every markup tag, placeholder, variable, and line break exactly, including <...>, </...>, $..., ${...}, %...%, ＜ユーザー名は１２文字＞, and {...}.
   Never translate or alter any text inside <...> or {...}. The translated text must contain exactly the same number of newline characters as the source.
2. Do not add explanations, censorship, quotation marks, or translator notes.
3. Keep character names and terminology consistent within the batch.
4. Fixed character names: アンドロメダ = 安德洛墨达; アンドロメダ(水着) = 安德洛墨达（泳装）. Never vary these names.
5. Return one translation for every numeric id.
6. Output valid JSON only in this shape: {"translations":[{"id":0,"text":"..."}]}.
"""


def load_output(path: Path) -> dict[str, object]:
    if path.is_file():
        return json.loads(path.read_text(encoding="utf-8"))
    return {
        "meta": {
            "language": "zh-Hans",
            "generator": "DeepSeek-V4-Flash",
            "game": "Techcronoss X",
        },
        "translations": {},
    }


def save_output(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True),
        encoding="utf-8",
    )
    temporary.replace(path)


def batches(items: list[dict[str, object]], max_items: int, max_chars: int):
    batch: list[dict[str, object]] = []
    characters = 0
    for item in items:
        size = len(str(item["source"]))
        if batch and (len(batch) >= max_items or characters + size > max_chars):
            yield batch
            batch = []
            characters = 0
        batch.append(item)
        characters += size
    if batch:
        yield batch


def request_translation(api_key: str, model: str, batch: list[dict[str, object]]) -> dict[int, str]:
    content = {
        "items": [
            {
                "id": index,
                "source": item["source"],
                "context": (item.get("contexts") or [""])[0],
            }
            for index, item in enumerate(batch)
        ]
    }
    body = json.dumps(
        {
            "model": model,
            "temperature": 0.15,
            "response_format": {"type": "json_object"},
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": json.dumps(content, ensure_ascii=False)},
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
    raw = payload["choices"][0]["message"]["content"]
    parsed = json.loads(raw)
    result = {
        int(item["id"]): str(item["text"])
        for item in parsed.get("translations", [])
    }
    if set(result) != set(range(len(batch))):
        raise ValueError(f"Incomplete response: {len(result)}/{len(batch)}")
    for index, item in enumerate(batch):
        source = str(item["source"])
        translated = result[index]
        if source.count("\n") != translated.count("\n"):
            raise ValueError(f"Line-break mismatch for item {index}")
        for opening, closing in (("<", ">"), ("{", "}")):
            source_tokens = _enclosed_tokens(source, opening, closing)
            translated_tokens = _enclosed_tokens(translated, opening, closing)
            if source_tokens != translated_tokens:
                raise ValueError(f"Markup mismatch for item {index}")
    return result


def _enclosed_tokens(value: str, opening: str, closing: str) -> list[str]:
    tokens: list[str] = []
    offset = 0
    while True:
        start = value.find(opening, offset)
        if start < 0:
            return tokens
        end = value.find(closing, start + 1)
        if end < 0:
            return tokens
        tokens.append(value[start : end + 1])
        offset = end + 1


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", type=Path, default=Path(".work/source_strings.json"))
    parser.add_argument("--output", type=Path, default=Path("translations/zh-Hans.json"))
    parser.add_argument("--model", default="deepseek-v4-flash")
    parser.add_argument("--max-items", type=int, default=80)
    parser.add_argument("--max-chars", type=int, default=7000)
    parser.add_argument("--limit", type=int)
    parser.add_argument("--attempts", type=int, default=6)
    args = parser.parse_args()

    api_key = os.environ.get("DEEPSEEK_API_KEY", "").strip()
    if not api_key:
        raise SystemExit("DEEPSEEK_API_KEY is not set")
    source_items = json.loads(args.input.read_text(encoding="utf-8"))
    output = load_output(args.output)
    output.setdefault("meta", {})["generator"] = "DeepSeek-V4-Flash"
    translated: dict[str, str] = output.setdefault("translations", {})
    pending = [item for item in source_items if item["source"] not in translated]
    if args.limit is not None:
        pending = pending[: args.limit]
    work = list(batches(pending, args.max_items, args.max_chars))
    print(f"Pending={len(pending)} batches={len(work)} existing={len(translated)}", flush=True)

    for batch_index, batch in enumerate(work, 1):
        for attempt in range(1, args.attempts + 1):
            try:
                result = request_translation(api_key, args.model, batch)
                for index, item in enumerate(batch):
                    translated[str(item["source"])] = result[index]
                save_output(args.output, output)
                print(
                    f"Batch {batch_index}/{len(work)} saved={len(batch)} total={len(translated)}",
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
                delay = min(60, (2 ** attempt) + random.random() * 2)
                print(f"Batch {batch_index} attempt {attempt} failed: {error}; retry {delay:.1f}s", flush=True)
                time.sleep(delay)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
