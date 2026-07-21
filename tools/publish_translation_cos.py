from __future__ import annotations

import json
import os
import sys
import urllib.request
from pathlib import Path

from qcloud_cos import CosConfig, CosS3Client


BUCKET = "techcronoss-offline-1313455599"
REGION = "ap-guangzhou"
PUBLIC_BASE_URL = f"https://{BUCKET}.cos.{REGION}.myqcloud.com"
OBJECT_KEY = "public/translations/zh-Hans.json"
SOURCE = Path(__file__).resolve().parents[1] / "translations" / "zh-Hans.json"


def read_user_environment(name: str) -> str | None:
    value = os.environ.get(name)
    if value:
        return value
    if sys.platform != "win32":
        return None

    import winreg

    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, "Environment") as key:
            value, _ = winreg.QueryValueEx(key, name)
            return str(value) if value else None
    except OSError:
        return None


def upload() -> None:
    secret_id = read_user_environment("AI_SecretId")
    secret_key = read_user_environment("AI_SECRETKEY")
    if not secret_id or not secret_key:
        raise RuntimeError("AI_SecretId or AI_SECRETKEY is missing")

    raw = SOURCE.read_bytes()
    parsed = json.loads(raw.decode("utf-8"))
    config = CosConfig(
        Region=REGION,
        SecretId=secret_id,
        SecretKey=secret_key,
        Scheme="https",
    )
    client = CosS3Client(config)
    client.put_object(
        Bucket=BUCKET,
        Key=OBJECT_KEY,
        Body=raw,
        ACL="public-read",
        ContentType="application/json; charset=utf-8",
        CacheControl="public, max-age=300, must-revalidate",
    )

    # Remove artifacts from the retired manifest + gzip publishing layout.
    try:
        client.delete_object(Bucket=BUCKET, Key="public/translations/manifest.json")
    except Exception:
        pass
    try:
        listed = client.list_objects(Bucket=BUCKET, Prefix="public/translations/data/")
        for item in listed.get("Contents", []):
            client.delete_object(Bucket=BUCKET, Key=item["Key"])
    except Exception:
        pass

    public_url = f"{PUBLIC_BASE_URL}/{OBJECT_KEY}"
    with urllib.request.urlopen(public_url, timeout=30) as response:
        remote = response.read()
    if remote != raw:
        raise RuntimeError("Anonymous COS verification failed: content mismatch")

    print(f"url={public_url}")
    print(f"bytes={len(raw)}")
    print(f"translations={len(parsed.get('translations', {}))}")
    print(f"character_names={len(parsed.get('character_names', {}))}")


if __name__ == "__main__":
    upload()
