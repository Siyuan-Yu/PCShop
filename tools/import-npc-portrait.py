"""Copy a built-in image-generation result into Unity and record its provenance.

This script only imports already generated files; it never calls an image API or
alters image pixels. Run one --id / --source pair for each completed portrait.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import time

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets/Resources/PortraitsRetro"
MANIFEST = ROOT / "docs/art/npc-retro-portraits.json"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--id", required=True)
    parser.add_argument("--source", required=True)
    parser.add_argument("--approved-sample", action="store_true")
    args = parser.parse_args()
    if not re.fullmatch(r"[a-z0-9_]+", args.id):
        raise SystemExit("Invalid stable portrait ID")
    source = Path(args.source).resolve()
    if not source.is_file() or source.read_bytes()[:8] != b"\x89PNG\r\n\x1a\n":
        raise SystemExit("Expected a completed PNG image")
    # A short exclusive lock protects the manifest when independent tool jobs finish together.
    lock = ROOT / "work/retro-portraits/import.lock"
    lock.parent.mkdir(parents=True, exist_ok=True)
    for _ in range(400):
        try:
            handle = os.open(lock, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
            break
        except FileExistsError:
            time.sleep(.05)
    else:
        raise SystemExit("Portrait import lock timed out")
    try:
        data = json.loads(MANIFEST.read_text(encoding="utf-8"))
        person = next((p for p in data["portraits"] if p["id"] == args.id), None)
        if person is None:
            raise SystemExit("Portrait ID is absent from the reviewed roster")
        ART.mkdir(parents=True, exist_ok=True)
        destination = ART / (args.id + ".png")
        temporary = lock.parent / (args.id + ".png.pending")
        shutil.copyfile(source, temporary)
        os.replace(temporary, destination)
        person.update(status="complete", sourceFile=source.name,
                      approvedSample=args.approved_sample,
                      sha256=hashlib.sha256(destination.read_bytes()).hexdigest(),
                      asset=str(destination.relative_to(ROOT)).replace("\\", "/"))
        MANIFEST.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        completed = sum(p["status"] == "complete" for p in data["portraits"])
        print(json.dumps({"id": args.id, "complete": completed, "total": len(data["portraits"])}))
    finally:
        os.close(handle)
        lock.unlink(missing_ok=True)


if __name__ == "__main__":
    main()
