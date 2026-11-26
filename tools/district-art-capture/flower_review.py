"""用相同机位的两次 Play Mode 抓帧复核花境和三区的四个概念视角。"""
import argparse
import csv
from pathlib import Path

import numpy as np
from PIL import Image

from contact_sheets import quadrants, read, rgb_score, sheet


DISTRICTS = (
    ("02", "社区", "锦线裁缝铺", "裁缝铺旁的座椅花境", (.005, .41, .465, .87)),
    ("03", "公园", "河岸公园", "池岸与临街白墙", (.02, .33, .64, .95)),
    ("04", "花园", "庭院别墅", "临街露台与泳池花园", (.20, .23, .70, .73)),
)
ANGLES = (("streetA", "临街正面"), ("streetB", "街角反向"), ("wideA", "鸟瞰一"), ("wideB", "鸟瞰二"))


def blossom_pixels(frame):
    reduced = frame.resize((800, 450), Image.Resampling.LANCZOS)
    red, green, blue = np.moveaxis(np.asarray(reduced, dtype=np.float32) / 255, -1, 0)
    mask = ((red > green + .041) & (blue > green + .011) & (red > .36)
            & (blue > .27) & (blue > red * .67))
    return round(100 * float(mask.mean()), 3)


def crop(frame, rectangle):
    x, y, right, bottom = rectangle
    return frame.crop((round(x * frame.width), round(y * frame.height),
                       round(right * frame.width), round(bottom * frame.height)))


def write_table(path, header, rows):
    with path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.writer(stream, delimiter="\t")
        writer.writerow(header)
        writer.writerows(rows)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("concepts", type=Path, help="分区概念图目录")
    parser.add_argument("before", type=Path, help="改造前的已配准实机目录")
    parser.add_argument("after", type=Path, help="当前的已配准实机目录")
    parser.add_argument("output", type=Path)
    parser.add_argument("--font", type=Path, default=Path("C:/Windows/Fonts/msyh.ttc"))
    args = parser.parse_args()
    args.output.mkdir(exist_ok=True, parents=True)
    colour_rows, frame_rows, closeups, panoramas = [], [], [], []
    for number, name, landmark, garden, rectangle in DISTRICTS:
        concepts = quadrants(next(args.concepts.glob(number + "-*.png")))
        rows = []
        for concept, (angle, label) in zip(concepts, ANGLES):
            key = f"match-{number}-{angle}"
            before = read(args.before / (key + ".png"))
            after = read(args.after / (key + ".png"))
            if before.size != after.size:
                raise ValueError(f"{key} 的两次复拍画幅不同")
            rows.append([(label + " · 定稿概念", concept), (label + " · 改造前", before),
                         (label + " · 当前实机", after)])
            colour_rows.append([name, label, *map(blossom_pixels, (concept, before, after))])
            frame_rows.append([name, label, key, round(rgb_score(concept, before), 2),
                               round(rgb_score(concept, after), 2)])
            if angle == "streetA":
                closeups.append([(name + " · 概念局部", crop(concept, rectangle)),
                                 (garden + " · 改造前", crop(before, rectangle)),
                                 (garden + " · 当前实机", crop(after, rectangle))])
            if angle == "wideA" and number in ("02", "04"):
                panoramas.append([(name + " · 概念鸟瞰", concept), (name + " · 改造前鸟瞰", before),
                                  (name + " · 当前实机鸟瞰", after)])
        sheet(args.output / (name + "四角度.jpg"), name + "　" + landmark + "与街区全景",
              rows, 3, 760, args.font)
    sheet(args.output / "花境与花树.jpg", "花境、露台与湖岸花树　同机位复拍",
          closeups + panoramas, 3, 680, args.font)
    write_table(args.output / "花色面积.tsv", ["区域", "机位", "概念粉紫像素百分比", "改造前粉紫像素百分比",
                                                "当前粉紫像素百分比"], colour_rows)
    write_table(args.output / "固定机位整帧色差.tsv", ["地区", "机位", "原帧名", "改造前", "当前"], frame_rows)


if __name__ == "__main__":
    main()
