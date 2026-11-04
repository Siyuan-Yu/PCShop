"""Record game-source and asset hashes without reading saves or Unity caches."""
import hashlib
import json
import subprocess
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'docs/snapshot-2026-11-04-files.json'


def main():
    names = subprocess.check_output(
        ['git', 'ls-files', '--cached', '--others', '--exclude-standard', '-z'], cwd=ROOT
    ).decode('utf-8').split('\0')
    files = []
    for name in sorted(set(names)):
        if not name or not name.startswith(('Assets/', 'Packages/', 'ProjectSettings/', 'tools/')):
            continue
        path = ROOT / name
        if not path.is_file():
            continue
        data = path.read_bytes()
        files.append(dict(path=name, bytes=len(data), sha256=hashlib.sha256(data).hexdigest()))
    counts = Counter(f['path'].split('/')[0] for f in files)
    payload = dict(date='2026-11-04', baseCommit=subprocess.check_output(
        ['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
        scope='Current game assets, code, package/project settings and tools. Documentation and previews accompany this snapshot in Git; ignored caches, builds and player saves are excluded.',
        counts=dict(counts), totalFiles=len(files), totalBytes=sum(f['bytes'] for f in files), files=files)
    DEST.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({k: payload[k] for k in ('counts', 'totalFiles', 'totalBytes')}))


if __name__ == '__main__':
    main()
