"""Rebuild the district broadleaf albedo and cutout atlas from the approved concept.

Run python tools/district_tree_atlas.py. Pillow, NumPy and SciPy are required.
The optional --output path is useful for comparing artwork before importing it.
"""
import argparse
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy.ndimage import distance_transform_edt
from scipy.spatial import cKDTree


ROOT = Path(__file__).resolve().parents[1]
SURFACES = ROOT / "Assets/Resources/OldTown/Surfaces"
CONCEPT = ROOT / "docs/art-direction/四区场景复核/分区概念/02-当代社区.png"

# Pixel coordinates in the approved normal-distance view; outlines are local to the crop.
SOURCE_SPRIGS = (
    ((140,84,196,131), (
        (12,39), (15,34), (22,32), (19,31), (15,33), (15,30), (12,29),
        (16,28), (21,29), (19,26), (22,25), (24,27), (25,21), (27,22),
        (28,27), (31,24), (33,24), (31,29), (28,31), (33,31), (37,30),
        (37,32), (33,34), (30,34), (29,38), (27,36), (26,34), (24,39),
        (22,40), (22,36), (19,35), (15,39),
    )),
    ((85,76,141,123), (
        (6,39), (10,37), (8,36), (11,33), (15,32), (13,29), (15,29),
        (18,32), (19,30), (21,30), (21,33), (25,30), (25,32), (22,36),
        (25,37), (28,37), (27,39), (21,39), (20,41), (18,39), (16,42),
        (14,43), (14,40), (10,41), (8,43), (7,42), (10,39),
    )),
    ((120,149,176,196), (
        (21,29), (23,26), (26,25), (24,23), (26,21), (29,21), (29,17),
        (31,18), (32,22), (35,20), (37,20), (35,24), (32,26), (35,27),
        (38,27), (38,29), (34,30), (32,29), (33,33), (31,35), (29,32),
        (28,30), (26,32), (25,35), (24,33), (25,29), (20,30),
    )),
)

# Local (x, y, radius) of each terminal group in a 480 x 365 branch card.
CROWNS = (
    ((.14,.59,.31), (.29,.48,.39), (.43,.28,.49), (.62,.31,.49),
     (.78,.45,.41), (.85,.68,.25), (.54,.58,.52), (.32,.73,.33)),
    ((.08,.53,.26), (.25,.53,.36), (.38,.43,.36), (.55,.32,.49),
     (.76,.39,.45), (.89,.52,.22), (.68,.61,.37), (.42,.70,.30)),
    ((.12,.60,.28), (.31,.44,.42), (.49,.22,.4), (.66,.28,.31),
     (.79,.49,.39), (.67,.71,.38), (.48,.64,.48), (.29,.7,.26)),
    ((.18,.55,.36), (.37,.32,.38), (.53,.25,.42), (.71,.35,.42),
     (.82,.53,.38), (.61,.58,.49), (.43,.71,.28), (.26,.76,.23)),
)


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


# Interior boughs preserve the larger light and shade masses of the drawing.
# Their boundary lies inside the original canopy; individual fans finish the silhouette.
SOURCE_BOUGHS = (
    ((87,99,152,155), ((2,34),(12,29),(9,23),(17,21),(14,17),(23,18),(27,9),(33,12),(41,3),(44,10),(52,8),(49,18),(60,16),(55,26),(62,32),(52,34),(48,44),(39,41),(34,52),(28,46),(20,49),(18,40),(9,45),(12,38))),
    ((107,135,171,189), ((2,31),(12,25),(8,19),(19,20),(18,13),(26,15),(27,6),(36,10),(44,4),(45,14),(52,11),(51,20),(61,21),(55,29),(61,35),(51,37),(47,46),(39,43),(31,51),(27,44),(19,47),(16,38),(7,42),(10,35))),
    ((142,113,204,170), ((3,35),(12,28),(8,23),(16,20),(13,16),(24,17),(29,6),(34,11),(40,4),(44,13),(51,9),(49,20),(59,18),(53,28),(60,33),(49,37),(48,46),(40,41),(33,52),(28,46),(20,50),(18,40),(8,44),(12,37))),
    ((77,148,142,201), ((3,31),(12,26),(8,21),(17,19),(15,13),(25,15),(30,5),(35,10),(43,3),(45,12),(54,8),(51,19),(61,20),(54,28),(61,32),(51,36),(48,44),(40,41),(33,49),(27,43),(19,47),(17,38),(7,42),(12,35))),
)


def tint_sprig(stamp, light):
    pixels = np.array(stamp)
    sunlight = np.clip(light, 0, 1)
    # Preserve the original hue and midrib of each leaf. The lower tiers receive cool sky.
    shade = np.array([.44, .57, .77])
    highlight = np.array([1.07, 1.08, 1.02])
    pixels[:, :, :3] = np.clip(pixels[:, :, :3]*(shade+(highlight-shade)*sunlight), 0, 255)
    return Image.fromarray(pixels)


def paint_bough(centers, fans, cores, rng):
    width, height = 480, 365
    pad = 135
    sprite = Image.new("RGBA", (width+2*pad, height+2*pad))
    pen = ImageDraw.Draw(sprite)
    pieces = []
    for index, (x, y, radius) in enumerate(centers):
        elbow = (round(width*(x*.48+.49*.52))+pad, round(height*(.84*.62+y*.38))+pad)
        pen.line([(round(width*.49)+pad, round(height*.84)+pad), elbow, (round(x*width)+pad,round(y*height)+pad)],
                 fill=(82,70,43,240), width=3)
        light = .63+(.52-y)*1.26
        pieces.append((light-.26, cores[index%len(cores)], x, y, rng.uniform(145,195), rng.uniform(-28,28)))
        for i in range(7):
            angle = rng.uniform(0, math.tau)
            distance = np.sqrt(rng.random())
            px = x+radius*.36*np.cos(angle)*distance
            py = y+radius*.38*np.sin(angle)*distance
            value = light-np.sin(angle)*distance*.31+rng.uniform(-.11,.12)
            pieces.append((value, fans[(index+i)%len(fans)], px, py, rng.uniform(49,79), rng.uniform(-70,70)))
    for light, piece, px, py, scale, angle in sorted(pieces, key=lambda p: p[0]):
        sprig = tint_sprig(piece, light)
        sprig = sprig.resize((round(scale),round(scale*sprig.height/sprig.width)),Image.Resampling.BICUBIC)
        sprig = sprig.rotate(angle,expand=True,resample=Image.Resampling.BICUBIC)
        sprite.alpha_composite(sprig,(round(px*width-sprig.width*.5)+pad,round(py*height-sprig.height*.5)+pad))
    # Fit the finished silhouette, including every extended leaf, into the padded UV island.
    content=sprite.crop(sprite.getchannel("A").getbbox())
    content.thumbnail((width-18,height-18), Image.Resampling.LANCZOS)
    tile=Image.new("RGBA",(width,height))
    tile.alpha_composite(content,((width-content.width)//2,(height-content.height)//2))
    pixels=np.array(tile)
    nearest=distance_transform_edt(pixels[:,:,3]<=8,return_distances=False,return_indices=True)
    pixels[:,:,:3]=pixels[:,:,:3][tuple(nearest)]
    return Image.fromarray(pixels)


def build_atlas(output):
    with Image.open(CONCEPT) as source:
        reference = source.convert("RGB")
    stamps = [sample_sprig(reference, box, polygon) for box, polygon in SOURCE_SPRIGS]
    cores = [sample_sprig(reference, box, polygon) for box, polygon in SOURCE_BOUGHS]
    atlas = Image.new("RGBA", (1024, 1024))
    rng = np.random.default_rng(4616)
    for slot, centers in enumerate(CROWNS):
        atlas.paste(paint_bough(centers, stamps, cores, rng), (slot%2*512+16, slot//2*512+19))
    # The trunk shares the existing bark UV window in PaintedWoodland.
    with Image.open(SURFACES / "summerCanopies.png") as original:
        bark = np.array(original.convert("RGBA").crop((0,916,512,1024)))
    intensity = bark[:, :, :3] @ np.array([.26, .59, .15])
    bark[:, :, :3] = np.clip(intensity[:, :, None]*np.array([1.20,1.01,.80])+[7,4,3], 0, 255)
    atlas.paste(Image.fromarray(bark), (0,916))
    atlas.save(output)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=SURFACES / "streetBoughs.png")
    build_atlas(parser.parse_args().output)
