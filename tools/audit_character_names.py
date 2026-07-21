from __future__ import annotations

import argparse
import json
from collections import Counter
from pathlib import Path

from normalize_character_names import outside_markup, source_names


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--translation", type=Path, default=Path("translations/zh-Hans.json"))
    args = parser.parse_args()

    payload = json.loads(args.translation.read_text(encoding="utf-8"))
    translations: dict[str, str] = payload["translations"]
    names: dict[str, str] = payload["character_names"]
    japanese_residue: list[tuple[str, str, str]] = []
    missing_canonical: list[tuple[str, str, str, str]] = []
    covered = Counter()

    for source, translated in translations.items():
        expected = source_names(source, names)
        plain = outside_markup(translated)
        for japanese, chinese in expected:
            covered[japanese] += 1
            if japanese in plain:
                japanese_residue.append((japanese, source, translated))
            if chinese not in plain:
                missing_canonical.append((japanese, chinese, source, translated))

    print(f"translations={len(translations)}")
    print(f"character_names={len(names)}")
    print(f"character_titles={len(payload.get('character_titles', {}))}")
    print(f"name_mentions={sum(covered.values())}")
    print(f"japanese_residue={len(japanese_residue)}")
    print(f"missing_canonical={len(missing_canonical)}")
    for japanese, source, translated in japanese_residue[:20]:
        print(f"RESIDUE {japanese}: {source!r} => {translated!r}")
    for japanese, chinese, source, translated in missing_canonical[:20]:
        print(f"MISSING {japanese}->{chinese}: {source!r} => {translated!r}")
    return 1 if japanese_residue else 0


if __name__ == "__main__":
    raise SystemExit(main())
