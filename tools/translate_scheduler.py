from __future__ import annotations

import argparse
import base64
import json
import os
import re
import subprocess
import sys
import time
from datetime import datetime, timedelta
from pathlib import Path


def notify(title: str, message: str) -> None:
    title = "\u94c1\u6298\u5609\u5e74\u534e"
    def decode(value: str) -> str:
        return re.sub(
            r"\\u([0-9a-fA-F]{4})",
            lambda match: chr(int(match.group(1), 16)),
            value,
        )

    title = decode(title)
    message = decode(message)
    script = (
        "Add-Type -AssemblyName System.Windows.Forms; "
        "$n = New-Object System.Windows.Forms.NotifyIcon; "
        "$n.Icon = [System.Drawing.SystemIcons]::Information; "
        "$n.Visible = $true; "
        f"$n.BalloonTipTitle = '{title.replace(chr(39), chr(39) * 2)}'; "
        f"$n.BalloonTipText = '{message.replace(chr(39), chr(39) * 2)}'; "
        "$n.ShowBalloonTip(8000); Start-Sleep -Seconds 8; $n.Dispose()"
    )
    encoded = base64.b64encode(script.encode("utf-16le")).decode("ascii")
    try:
        subprocess.run(
            ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-EncodedCommand", encoded],
            check=False,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            timeout=12,
        )
    except (OSError, subprocess.TimeoutExpired):
        pass


def role_for(item: dict) -> str:
    for context in item.get("contexts", []):
        if context.startswith("script:"):
            return context.split(":", 2)[1]
        if context.endswith(":Name"):
            return context.rsplit(":", 1)[0]
    return "unknown"


def main() -> int:
    parser = argparse.ArgumentParser(description="Translate character/story groups until a deadline.")
    parser.add_argument("--input", type=Path, default=Path(".work/character_story_source.json"))
    parser.add_argument("--output", type=Path, default=Path("translations/zh-Hans.json"))
    parser.add_argument("--until", help="Stop at local time HH:MM, for example 14:00")
    parser.add_argument("--max-minutes", type=int)
    parser.add_argument("--max-chars", type=int, default=6000)
    parser.add_argument("--glossary", type=Path, default=Path(r"C:\Users\曾罗畅\Downloads\铁扣对照\glossary.json"))
    args = parser.parse_args()

    if not os.environ.get("DEEPSEEK_API_KEY"):
        raise SystemExit("DEEPSEEK_API_KEY is not set")
    if args.until:
        hour, minute = (int(x) for x in args.until.split(":", 1))
        now = datetime.now()
        deadline = now.replace(hour=hour, minute=minute, second=0, microsecond=0)
        if deadline <= now:
            deadline += timedelta(days=1)
    elif args.max_minutes:
        deadline = datetime.now() + timedelta(minutes=args.max_minutes)
    else:
        raise SystemExit("Specify --until HH:MM or --max-minutes N")

    source = json.loads(args.input.read_text(encoding="utf-8"))
    translated = json.loads(args.output.read_text(encoding="utf-8"))["translations"] if args.output.exists() else {}
    temp_dir = args.input.parent / "scheduler_batches"
    temp_dir.mkdir(exist_ok=True)
    glossary = json.loads(args.glossary.read_text(encoding="utf-8")) if args.glossary.exists() else {"names": {}, "pronouns": {}}
    known_names = set(glossary.get("names", {}))
    known_names.update(
        item["source"]
        for item in source
        if any(c.endswith(":Name") for c in item.get("contexts", [])) and item["source"] in translated
    )
    unknown_names = sorted({item["source"] for item in source if any(c.endswith(":Name") for c in item.get("contexts", []))} - known_names)
    if unknown_names:
        pending = args.glossary.parent / "待确认人名.json"
        pending.write_text(
            json.dumps({"说明": "以下人名先由 DeepSeek 初译，完成后请人工确认", "names": unknown_names}, ensure_ascii=False, indent=2),
            encoding="utf-8",
        )
        notify("铁抠嘉年华", f"发现 {len(unknown_names)} 个未对照人名\n将先使用 DeepSeek 初译")
    pending_name_items = [
        item for item in source
        if any(c.endswith(":Name") for c in item.get("contexts", []))
        and item["source"] not in translated
    ]
    if pending_name_items:
        name_file = temp_dir / "names_first.json"
        name_file.write_text(json.dumps(pending_name_items, ensure_ascii=False, indent=2), encoding="utf-8")
        command = [
            sys.executable,
            str(Path(__file__).with_name("translate_deepseek.py")),
            "--input", str(name_file),
            "--output", str(args.output),
            "--max-items", "40",
            "--max-chars", str(args.max_chars),
        ]
        result = subprocess.run(command, cwd=Path(__file__).parent.parent, check=False, capture_output=True, text=True, encoding="utf-8", errors="replace")
        if result.stdout:
            print(result.stdout, end="", flush=True)
        if result.returncode != 0:
            (args.input.parent / "scheduler_failure.log").write_text(result.stderr or result.stdout or "name translation failed", encoding="utf-8")
            notify("铁抠嘉年华", "人名初译中断，详情已写入 scheduler_failure.log")
            return result.returncode
        notify("铁抠嘉年华", f"待确认人名初译完成：{len(pending_name_items)} 个\n请修改 glossary.json 后再启动剧情翻译")
        return 0
    groups: dict[str, list[dict]] = {}
    for item in source:
        if item["source"] in translated:
            continue
        groups.setdefault(role_for(item), []).append(item)

    failure_log = args.input.parent / "scheduler_failure.log"
    print(f"Pending groups: {len(groups)}; deadline: {deadline:%Y-%m-%d %H:%M}", flush=True)
    for index, (role, items) in enumerate(groups.items(), 1):
        if datetime.now() >= deadline:
            notify("铁抠嘉年华", "翻译已停止：到达设定时间，进度已保存")
            break
        batch_file = temp_dir / f"{index:04d}.json"
        batch_file.write_text(json.dumps(items, ensure_ascii=False, indent=2), encoding="utf-8")
        command = [sys.executable, str(Path(__file__).with_name("translate_deepseek.py")), "--input", str(batch_file), "--output", str(args.output), "--max-items", "50", "--max-chars", str(args.max_chars)]
        result = subprocess.run(
            command,
            cwd=Path(__file__).parent.parent,
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
        if result.returncode != 0:
            detail = (result.stderr or result.stdout or "未知错误").strip()
            failure_log.write_text(
                f"time={datetime.now():%Y-%m-%d %H:%M:%S}\nrole={role}\n"
                f"batch={index}/{len(groups)}\nreturncode={result.returncode}\n\n{detail}\n",
                encoding="utf-8",
            )
            skipped_path = args.input.parent / "scheduler_skipped.jsonl"
            recovered = 0
            skipped = 0
            for item_index, item in enumerate(items, 1):
                if datetime.now() >= deadline:
                    break
                single_file = temp_dir / f"{index:04d}_{item_index:04d}.json"
                single_file.write_text(
                    json.dumps([item], ensure_ascii=False, indent=2),
                    encoding="utf-8",
                )
                single_command = [
                    sys.executable,
                    str(Path(__file__).with_name("translate_deepseek.py")),
                    "--input", str(single_file),
                    "--output", str(args.output),
                    "--max-items", "1",
                    "--max-chars", str(args.max_chars),
                    "--attempts", "2",
                ]
                single_result = subprocess.run(
                    single_command,
                    cwd=Path(__file__).parent.parent,
                    check=False,
                    capture_output=True,
                    text=True,
                    encoding="utf-8",
                    errors="replace",
                )
                if single_result.returncode == 0:
                    recovered += 1
                    if single_result.stdout:
                        print(single_result.stdout, end="", flush=True)
                    continue
                skipped += 1
                with skipped_path.open("a", encoding="utf-8") as handle:
                    handle.write(json.dumps({
                        "time": datetime.now().isoformat(timespec="seconds"),
                        "role": role,
                        "source": item.get("source", ""),
                        "contexts": item.get("contexts", []),
                        "error": (single_result.stderr or single_result.stdout or "unknown error")[-2000:],
                    }, ensure_ascii=False) + "\n")
            print(
                f"Fallback role={role} recovered={recovered} skipped={skipped}",
                flush=True,
            )
            notify("铁抠嘉年华", f"{role} 单条恢复 {recovered}，跳过 {skipped}")
            continue
        if result.stdout:
            print(result.stdout, end="", flush=True)
        remaining = len(groups) - index
        notify("铁抠嘉年华", f"角色翻译完成：{role}\n还剩 {remaining} 个角色")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
