"""Rebuild the district broadleaf albedo and cutout atlas from the approved concept.

Run python tools/district_tree_atlas.py. Pillow, NumPy and SciPy are required.
The optional --output path is useful for comparing artwork before importing it.
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy.ndimage import distance_transform_edt
from scipy.spatial import cKDTree


ROOT = Path(__file__).resolve().parents[1]
SURFACES = ROOT / "Assets/Resources/OldTown/Surfaces"
CONCEPT = ROOT / "docs/art-direction/四区场景复核/分区概念/02-当代社区.png"

def sample_sprig(concept, rect, outline):
    painted = concept.crop(rect)
    mask = Image.new("L", painted.size)
    ImageDraw.Draw(mask).polygon(outline, fill=255)
    selected = np.array(mask) > 0
    y, x = np.indices(selected.shape)
    features = np.dstack((x * 5, y * 5, np.asarray(painted, float)))
    inside = distance_transform_edt(selected) > 1.4
    outside = distance_transform_edt(~selected) > 1.6
    edge = ~(inside | outside)
    # At the traced edge, assign the pixel to the nearest locally matching pigment.
    leaf_distance = cKDTree(features[inside]).query(features[edge])[0]
    background_distance = cKDTree(features[outside]).query(features[edge])[0]
    selected[edge] = leaf_distance < background_distance
    alpha = Image.fromarray(np.uint8(selected * 255)).filter(ImageFilter.GaussianBlur(.30))
    sprite = painted.convert("RGBA")
    sprite.putalpha(alpha)
    return sprite.crop(alpha.getbbox())


# Each mask encloses a continuous sunlit branch and its lower, shaded leaf fans.
# The close view reads the broad original transitions without restamping isolated leaves.
NATURAL_BRANCHES = (
    ((47,77,239,221), ((77,11),(82,17),(95,9),(102,14),(113,6),(127,8),(136,3),(141,16),(155,13),(153,23),(165,20),(168,33),(182,34),(174,42),(189,47),(177,53),(187,65),(175,69),(185,80),(169,81),(181,93),(164,97),(163,109),(149,113),(141,122),(122,123),(115,134),(101,128),(88,137),(76,128),(65,137),(64,125),(50,134),(48,125),(39,130),(38,117),(24,122),(28,110),(11,117),(18,105),(6,104),(13,91),(3,87),(15,79),(10,74),(25,63),(15,63),(27,51),(30,41),(42,42),(38,32),(48,35),(51,23),(62,27),(57,17),(69,21))),
    ((1492,104,1671,252), ((6,80),(16,74),(10,68),(22,59),(16,55),(30,50),(23,44),(39,40),(36,31),(47,34),(51,23),(59,25),(64,13),(76,18),(85,8),(94,14),(103,4),(111,14),(125,7),(129,16),(140,17),(143,26),(156,22),(152,34),(165,33),(161,46),(176,43),(177,112),(157,116),(160,127),(146,124),(137,132),(121,128),(114,140),(99,133),(91,142),(74,135),(64,139),(61,128),(46,135),(41,125),(28,128),(34,115),(17,121),(23,108),(7,110),(14,97),(2,96),(12,85))),
)


def build_atlas(output):
    with Image.open(CONCEPT) as source:
        reference = source.convert("RGB")
    branches = [sample_sprig(reference, rect, outline) for rect, outline in NATURAL_BRANCHES]
    atlas = Image.new("RGBA", (1024, 1024))
    for slot in range(4):
        branch=branches[slot%2]
        if slot>=2:
            # Rotate lateral forks independently around their attachment.
            branch=branch.transpose(Image.Transpose.FLIP_LEFT_RIGHT).rotate(-11 if slot==2 else 8,expand=True,resample=Image.Resampling.BICUBIC)
        branch=branch.resize((458,347),Image.Resampling.LANCZOS)
        tile=Image.new("RGBA",(480,365))
        tile.alpha_composite(branch,(11,9))
        pixels=np.array(tile)
        nearest=distance_transform_edt(pixels[:,:,3]<=8,return_distances=False,return_indices=True)
        pixels[:,:,:3]=pixels[:,:,:3][tuple(nearest)]
        atlas.paste(Image.fromarray(pixels),(slot%2*512+16,slot//2*512+19))
    # The trunk shares the existing bark UV window in PaintedWoodland.
    with Image.open(SURFACES / "summerCanopies.png") as original:
        bark = np.array(original.convert("RGBA").crop((0,916,512,1024)))
    intensity = bark[:, :, :3] @ np.array([.26, .59, .15])
    bark[:, :, :3] = np.clip(intensity[:, :, None]*np.array([1.20,1.01,.80])+[7,4,3], 0, 255)
    atlas.paste(Image.fromarray(bark), (0,916))
    atlas.save(output)


def build_grove_atlas(output):
    with Image.open(SURFACES / "summerCanopies.png") as summer:
        atlas = summer.convert("RGBA")
    with Image.open(SURFACES / "streetBoughs.png") as street:
        boughs = street.convert("RGBA")
        for source, target in ((0, 0), (2, 3)):
            sx, sy = source % 2 * 512, source // 2 * 512
            tx, ty = target % 2 * 512, target // 2 * 512
            # The lower 104 texels of each cell are outside the foliage UV interval.
            fan = np.asarray(boughs.crop((sx, sy, sx + 512, sy + 408))).copy()
            rgb = fan[:, :, :3].astype(float)
            value = rgb @ np.array([.27, .60, .13])
            tips = np.clip((value - 92) / 100, 0, 1)[:, :, None]
            multiplier = np.array([.79, .86, .91]) + tips * np.array([.21, .16, .17])
            fan[:, :, :3] = np.clip(rgb * multiplier, 0, 255)
            atlas.paste(Image.fromarray(fan), (tx, ty))
        # Opaque interior leaves supply the underside, between the separate branch tips.
        # This swatch sits beside the bark strip, outside the leaf and blossom rectangles.
        base = boughs.crop((218, 126, 293, 202)).convert("RGB").resize((480, 78), Image.Resampling.BICUBIC)
        pigment = np.asarray(base).astype(float)
        value = (pigment @ np.array([.27, .60, .13])) / 255
        dark = np.array([43., 66., 37.])
        bright = np.array([119., 148., 67.])
        patch = np.clip(dark + value[:, :, None] * (bright - dark), 0, 255).astype("uint8")
        atlas.paste(Image.fromarray(patch).convert("RGBA"), (528, 937))
    atlas.save(output)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--groves", action="store_true", help="生成远景立体树冠使用的枝叶、花树和柳树图集")
    args = parser.parse_args()
    if args.groves:
        build_grove_atlas(args.output or SURFACES / "summerGroves.png")
    else:
        build_atlas(args.output or SURFACES / "streetBoughs.png")
