"""从城市门址绘制分区草地权重图。红为公园，绿为花园，蓝为社区。"""
import json
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "Assets/Resources/Data/city_layout.json"
OUTPUT = ROOT / "Assets/Resources/OldTown/Surfaces/parkGroundZones.png"


def main():
    city = json.loads(DATA.read_text(encoding="utf-8-sig"))
    districts = {street: item["id"] for item in city["agencyDistricts"] for street in item["streets"]}
    nodes = {item["id"]: item for item in city["nodes"]}
    size = 192
    x = -1536 + (np.arange(size) + .5) * (3072 / size)
    z = -1024 + (size - np.arange(size) - .5) * (3072 / size)
    xx, zz = np.meshgrid(x, z)
    order = ("park", "garden", "community", "outskirts")
    distances = {district: np.full((size, size), np.inf) for district in order}
    for place in city["places"]:
        district = districts.get(place.get("street"))
        if district not in distances:
            continue
        node = nodes[place["node"]]
        px = node["x"] + place.get("entranceX", 0)
        pz = node["y"] + place.get("entranceY", 0)
        distances[district] = np.minimum(distances[district], (xx-px)**2 + (zz-pz)**2)
    field = np.stack([distances[key] for key in order], axis=2)
    weights = np.exp(-(field - field.min(axis=2, keepdims=True)) / 90**2)
    weights /= weights.sum(axis=2, keepdims=True)
    Image.fromarray(np.round(weights[:, :, :3] * 255).astype("uint8")).save(OUTPUT)


if __name__ == "__main__":
    main()
