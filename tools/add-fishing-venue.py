"""Add the fishing facility's searchable name without moving existing park geometry."""
import json
import re
from pathlib import Path

target = Path(__file__).resolve().parents[1] / "Assets/Resources/Data/city_layout.json"
text = target.read_text(encoding="utf-8-sig")
match = re.search(r'\{\s*"id": "life_hike",', text)
assert match, "The existing outskirts park must be present"
_, length = json.JSONDecoder().raw_decode(text[match.start():])
block = text[match.start():match.start() + length]
block, count = re.subn(r'"name": "(?:[^"\\]|\\.)*"', '"name": "城郊公园 · 青湾垂钓场"', block, count=1)
assert count == 1
target.write_text(text[:match.start()] + block + text[match.start() + length:], encoding="utf-8")
print("Updated the existing park's fishing-facility name; routes and footprints preserved.")
