"""根据同机位实机 PNG 和定稿概念复算六份局部材质取样表。"""

import argparse
import csv
from functools import cached_property
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

from regions import DISTRICT_LAWNS, DISTRICTS, FACADES, FACADE_BANDS, FISHING_WATER, OLD_TOWN, OLD_TOWN_SURFACES, PONDS


LUMA = np.array([.2126, .7152, .0722])


def rgb_image(path):
    with Image.open(path) as image:
        return image.convert("RGB")


def quadrant(image, right, bottom=False):
    w, h = image.size
    return image.crop((w // 2 if right else 0, h // 2 if bottom else 0,
                       w if right else w // 2, h if bottom else h // 2))


def lab(rgb):
    rgb = rgb / 255
    linear = np.where(rgb > .04045, ((rgb + .055) / 1.055) ** 2.4, rgb / 12.92)
    xyz = np.array([[.4124564,.3575761,.1804375], [.2126729,.7151522,.072175],
                    [.0193339,.119192,.9503041]]) @ linear / np.array([.95047, 1, 1.08883])
    f = np.where(xyz > (6/29)**3, xyz**(1/3), xyz / (3*(6/29)**2) + 4/29)
    return np.array([116*f[1]-16, 500*(f[0]-f[1]), 200*(f[1]-f[2])])


def rgb_text(rgb, delimiter=","):
    return delimiter.join(map(str, np.rint(rgb).astype(int)))


def polygon_mask(size, coordinates, polygons):
    mask = Image.new("1", size)
    draw = ImageDraw.Draw(mask)
    for polygon in polygons:
        draw.polygon([(round(x * size[0] / coordinates[0]), round(y * size[1] / coordinates[1]))
                      for x, y in polygon], fill=1)
    return np.asarray(mask)


class Frame:
    def __init__(self, image, size):
        image = image.resize(size, Image.Resampling.LANCZOS)
        self.rgb = np.asarray(image)
        self.y = self.rgb @ LUMA
        self.gray = Image.fromarray(np.rint(self.y).astype("uint8"))

    def band(self, inner, outer):
        low = np.asarray(self.gray.filter(ImageFilter.GaussianBlur(inner)), dtype=float)
        high = np.asarray(self.gray.filter(ImageFilter.GaussianBlur(outer)), dtype=float)
        return np.abs(low - high)

    @cached_property
    def fine(self):
        return self.band(.65, 2.1)

    @cached_property
    def medium(self):
        return self.band(2.1, 6.5)

    @cached_property
    def broad(self):
        return self.band(6.5, 14)


def write_table(out, title, fields, rows):
    with (out / (title + ".tsv")).open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.writer(stream, delimiter="\t", lineterminator="\n")
        writer.writerow(fields)
        writer.writerows(rows)


def old_town_surfaces(images, tag, out):
    size = (1600, 900)
    rows = []
    for view, name in OLD_TOWN.items():
        ref_rgb = {}
        for image, source in zip(images[view], ("concept", tag)):
            frame = Frame(image, size)
            for surface, polygons in OLD_TOWN_SURFACES[view].items():
                mask = polygon_mask(size, (792,447), polygons)
                median = np.median(frame.rgb[mask], axis=0)
                if source == "concept":
                    ref_rgb[surface] = median
                dy = np.quantile(frame.y[mask], [.1, .9])
                values = [np.median(frame.y[mask]), dy[1] - dy[0], frame.fine[mask].mean(),
                          frame.medium[mask].mean(), frame.broad[mask].mean()]
                delta = np.linalg.norm(lab(median) - lab(ref_rgb[surface]))
                rows.append([name, surface, source, mask.sum(), rgb_text(median), *[f"{v:.2f}" for v in values],
                             "0" if source == "concept" else f"{delta:.2f}"])
    write_table(out, "城郊草地树冠与铺装取样", ["机位","部位","图源","固定采样像素数","中位RGB","中位Y",
                "P90–P10","细笔触","中尺度","大尺度","中位色ΔE76"], sorted(rows, key=lambda row: row[2] != "concept"))


def tree_shadow(images, tag, out):
    rows = []
    for image, source in zip(images["home"], ("概念", tag)):
        y = Frame(image, (792,447)).y
        lawn, shadow = np.median(y[238:253, 7:18]), np.median(y[240:251, 55:88])
        spans = []
        for row in range(242, 252):
            dark = [np.median(y[row-1:row+2, x-1:x+2]) < .82 * lawn for x in range(4, 102)]
            for n in range(len(dark) - 4):
                if all(dark[n:n+5]):
                    spans.append(100 - (n + 4))
                    break
        width = f"{np.median(spans):.1f}" if spans else ""
        rows.append([source, f"{lawn:.2f}", f"{shadow:.2f}", f"{shadow/lawn:.3f}", width,
                     "y=242–251；5连续像素低于本图局部草地亮度82%"])
    write_table(out, "花树投影取样", ["花树侧投影","参照草地Y","阴影内部Y","阴影/草地",
                "到共同x=100线的阴影跨度px（792宽）","扫线范围"], rows)


def facade_pixels(image, box, viewport, line):
    bounds = tuple(round(v * (image.width / 660 if i % 2 == 0 else image.height / 372))
                   for i, v in enumerate(box))
    crop = image.crop(bounds)
    pixels = np.asarray(crop, dtype=float)
    width, height = crop.size
    crop.thumbnail(viewport)
    x, y, end_x, end_y, depth = line
    along = np.arange(.035, .965, .004)[:, None]
    column = np.rint((x + along*(end_x-x)) * (width/crop.width)).astype(int)
    row = np.rint((y + along*(end_y-y) + np.arange(4, depth, .9)) * (height/crop.height)).astype(int)
    return pixels[row, column].reshape(-1, 3)


def facades(images, tag, out):
    rows = []
    for name, view, box, viewport, ref_line, actual_line in FACADES:
        strips = [facade_pixels(image, box, viewport, line)
                  for image, line in zip(images[view], (ref_line, actual_line))]
        for band, limits in FACADE_BANDS:
            averages = []
            for strip in strips:
                y = strip @ LUMA
                low, high = np.quantile(y, limits)
                averages.append(strip[(y >= low) & (y <= high)].mean(axis=0))
            delta = np.linalg.norm(lab(averages[0]) - lab(averages[1]))
            for rgb, source in zip(averages, ("concept", tag)):
                rows.append([name, OLD_TOWN[view], band, source, rgb_text(rgb, " "), f"{rgb@LUMA:.1f}",
                             "0" if source == "concept" else f"{delta:.1f}"])
    write_table(out, "住宅墙面取样", ["材质部位","机位","取样区间","图源","sRGB","中位带Y′","ΔE76"],
                sorted(rows, key=lambda row: row[3] != "concept"))


def district_lawns(boards, frames, tag, out):
    rows = []
    for view, rectangles in DISTRICT_LAWNS.items():
        images = [quadrant(boards[view[:2]], view[-1] == "B"), rgb_image(frames / (view + ".png"))]
        for image, boxes, source in zip(images, rectangles, ("concept", tag)):
            frame = Frame(image, (1600,900))
            mask = np.zeros(frame.y.shape, dtype=bool)
            for x, y, right, bottom in boxes:
                mask[2*y:2*bottom, 2*x:2*right] = True
            stats = [*np.quantile(frame.y[mask], [.1,.5,.9]), frame.fine[mask].mean(), frame.medium[mask].mean()]
            rows.append([view, source, mask.sum(), rgb_text(np.median(frame.rgb[mask], axis=0)),
                         *[f"{v:.2f}" for v in stats]])
    write_table(out, "三区草地取样", ["view","source","ROI","median RGB","Y p10","median Y","Y p90",
                "fine .65–2.1 px","mid 2.1–6.5 px"], sorted(rows, key=lambda row: row[1] != "concept"))


def water_sample(frame, coordinates, polygons, digits):
    mask = polygon_mask(frame.gray.size, coordinates, polygons)
    quantiles = np.quantile(frame.y[mask], [.1,.5,.9,.98])
    return [mask.sum(), rgb_text(np.median(frame.rgb[mask], axis=0)),
            *[f"{v:.{digits}f}" for v in quantiles], f"{frame.fine[mask].mean():.2f}"]


def ponds(board, frames, tag, out):
    rows = []
    for view, surfaces in PONDS.items():
        is_site = view.endswith(".png")
        images = [(None if is_site else quadrant(board, view[-1] == "B")),
                  rgb_image(frames / (view if is_site else view + ".png"))]
        for i, image in enumerate(images):
            if image is None:
                continue
            frame = Frame(image, (1440,810))
            for name, polygons in surfaces.items():
                rows.append([view, ("concept", tag)[i], name, *water_sample(frame, (720,405), polygons[i], 1)])
    write_table(out, "池岸取样", ["机位","实机轮次","部位","像素数","sRGB中位","Y p10","Y p50","Y p90",
                "Y p98","细笔触强度"], rows)


def fishing(board, frames, tag, out):
    rows = []
    for angle, polygons in FISHING_WATER.items():
        images = [quadrant(board, angle == "b", bottom=True), rgb_image(frames / f"fishing-{angle}.png")]
        for image, polygon, source in zip(images, polygons, ("concept", tag)):
            frame = Frame(image, (1440,810))
            rows.append([angle, source, *water_sample(frame, (480,270), [polygon], 2)])
    write_table(out, "青湾钓场水面取样", ["机位","轮次","池面内部像素","sRGB 中位","Y P10","Y P50","Y P90",
                "Y P98","细笔触"], rows)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--frames", type=Path, required=True, help="含 oldtown-home.png 与 sites/ 的原帧目录")
    parser.add_argument("--concepts", type=Path, required=True, help="同时包含 old-quarter 和四区场景复核的概念根目录")
    parser.add_argument("--output", type=Path, required=True, help="复算表输出目录")
    parser.add_argument("--label", default="review12", help="实机轮次标签")
    args = parser.parse_args()
    measurements = list(DISTRICT_LAWNS) + ["fishing-a", "fishing-b"] + [f"oldtown-{view}" for view in OLD_TOWN]
    sources = [args.frames / (name + ".png") for name in measurements]
    sources.append(args.frames / "sites/02-life_community_park.png")
    for source in sources:
        if not source.is_file():
            parser.error(f"缺少实机原帧 {source}")
    args.output.mkdir(parents=True, exist_ok=True)
    district_art = args.concepts / "四区场景复核"
    old_town = {key: (rgb_image(args.concepts / "old-quarter/交付图档" / f"城郊两种住宅_{name}.png"),
                      rgb_image(args.frames / f"oldtown-{key}.png")) for key, name in OLD_TOWN.items()}
    boards = {key: rgb_image(district_art / "分区概念" / f"{key}-{name}.png") for key, name in DISTRICTS.items()}
    old_town_surfaces(old_town, args.label, args.output)
    tree_shadow(old_town, args.label, args.output)
    facades(old_town, args.label, args.output)
    district_lawns(boards, args.frames, args.label, args.output)
    ponds(boards["03"], args.frames, args.label, args.output)
    fishing(rgb_image(district_art / "城郊绿化与钓场定稿.png"), args.frames, args.label, args.output)


if __name__ == "__main__":
    main()
