"""Import an already generated PNG; never generate or change image pixels."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import struct
import uuid

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / 'docs/art/npc-anime-portraits.json'

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--id', required=True)
    parser.add_argument('--source', required=True)
    parser.add_argument('--approved-sample', action='store_true')
    args = parser.parse_args()
    data = json.loads(MANIFEST.read_text(encoding='utf-8'))
    person = next(p for p in data['portraits'] if p['id'] == args.id)
    source = Path(args.source).resolve()
    blob = source.read_bytes()
    if blob[:8] != b'\x89PNG\r\n\x1a\n':
        raise SystemExit('Not a PNG')
    width, height = struct.unpack('>II', blob[16:24])
    if not .73 < width / height < .77:
        raise SystemExit(f'Unexpected portrait ratio: {width}x{height}')
    destination = ROOT / person['asset']
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, destination)
    meta = Path(str(destination) + '.meta')
    if not meta.exists():
        template = (ROOT / ('Assets/Resources/PortraitsRetro/' + args.id + '.png.meta')).read_text(encoding='utf-8')
        template = re.sub(r'(?m)^guid: [a-f0-9]+$', 'guid: ' + uuid.uuid4().hex, template)
        meta.write_text(template, encoding='utf-8')
    folder_meta = Path(str(destination.parent) + '.meta')
    if not folder_meta.exists():
        folder_meta.write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8')
    person.update(status='complete', approvedSample=args.approved_sample,
                  sourceFile=str(source), sha256=hashlib.sha256(blob).hexdigest(), width=width, height=height)
    MANIFEST.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'id':args.id, 'complete':sum(p['status']=='complete' for p in data['portraits']), 'total':len(data['portraits'])}))

if __name__ == '__main__':
    main()
