"""Assign each residential facade a stable address and an unobstructed road approach."""
import math


def _footprint(x, y, building):
    angle = math.radians(building.get("heading", 0))
    return x, y, math.sin(angle), math.cos(angle), building["width"] / 2, building["depth"]


def _crosses_building(start, end, footprint):
    x, y, sine, cosine, width, depth = footprint

    def local(point):
        dx, dy = point[0] - x, point[1] - y
        return dx * cosine - dy * sine, dx * sine + dy * cosine

    first, last = 0.0, 1.0
    for a, b, low, high in zip(local(start), local(end), (-width - .8, -.8), (width + .8, depth + .8)):
        delta = b - a
        if abs(delta) < 1e-9:
            if not low <= a <= high:
                return False
            continue
        near, far = sorted(((low - a) / delta, (high - a) / delta))
        first, last = max(first, near), min(last, far)
        if first > last:
            return False
    return True


def assign_residence_entrances(layout):
    nodes = {node["id"]: (node["x"], node["y"]) for node in layout["nodes"]}
    blocks = layout["blocks"]
    footprints = [_footprint(block["x"], block["y"], block) for block in blocks]
    for place in layout["places"]:
        x, y = nodes[place["node"]]
        footprints.append(_footprint(x + place.get("entranceX", 0), y + place.get("entranceY", 0), place))

    numbers = {}
    for index, block in enumerate(blocks):
        if block["model"] not in {"oldhome", "apartment", "luxury_apartment", "villa"}:
            continue
        x, y, sine, cosine, _, _ = footprints[index]
        street = block["street"]
        number = numbers.get(street, 0) + 1
        numbers[street] = number
        candidates = sorted(
            (math.dist((x, y), point), node_id, point)
            for node_id, point in nodes.items()
            if (point[0] - x) * sine + (point[1] - y) * cosine < -.5
        )
        for _, node_id, point in candidates:
            if any(other != index and _crosses_building((x, y), point, footprint)
                   for other, footprint in enumerate(footprints)):
                continue
            block.update(homeId=f"residence_{street}_{number:02d}", homeNumber=number, entranceNode=node_id)
            break
        else:
            raise ValueError(f"No clear entrance for {street} residence {number}")
