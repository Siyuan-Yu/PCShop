"""Author V15 from the saved V14 layout: 1.3x extent, 22 shops, 35 homes, 4 hospitals.

Only writes authored layout data. Does not open Unity, run gameplay, or touch saves.
The baseline is immutable, so rebuilding never scales an already expanded city.
"""
import argparse
import collections
import copy
import json
import math
from pathlib import Path
from residence_access import _crosses_building, _footprint

ROOT = Path(__file__).resolve().parents[1]
TARGET = ROOT / "Assets/Resources/Data/city_layout.json"
BASELINE = ROOT / "tools/data/city-layout-v14.json"
SCALE = 1.3
PLAN = {
    "outskirts": {"tier": 0, "homes": {"s_oldhomes": 7}, "shops": {"s_start": 4, "s_oldhomes": 2}},
    "community": {"tier": 1, "homes": {"s_services": 7}, "shops": {"s_market": 3, "s_services": 2}},
    "park": {"tier": 2, "homes": {"s_sports": 11}, "shops": {"s_sports": 1, "s_creative": 2, "s_expo": 2}},
    "garden": {"tier": 3, "homes": {"s_newtown": 10}, "shops": {"s_night": 3, "s_newtown": 3}},
}
HOSPITALS = {
    "outskirts": ("s_start", "城郊医院"),
    "community": ("s_services", "社区医院"),
    "park": ("s_sports", "公园医院"),
    "garden": ("s_night", "花园医院"),
}

HOUSEHOLDS = {
    "outskirts": ["罗文清", "周秀兰", "陈远山", "梁雪梅"],
    "community": ["宋雨宁", "许慧敏", "林嘉成", "贺晓东"],
    "park": ["沈书怡", "方启辰", "吴素云", "叶晓凡", "姚春明", "程嘉禾"],
    "garden": ["顾明川", "苏雅文", "陆欣然", "高景宁", "秦志诚"],
}
MERCHANTS = {
    "outskirts": [("晨光修鞋铺", "修鞋"), ("庆丰五金铺", "五金"), ("邻里杂货铺", "杂货")],
    "community": [("日新文具店", "文具"), ("锦线裁缝铺", "裁缝"), ("灯下钟表铺", "钟表维修")],
    "park": [("青岚画材店", "画材"), ("木色手作铺", "手作"), ("栖页旧书店", "旧书")],
    "garden": [("观湖家饰店", "家饰"), ("良木修复铺", "家具修复"), ("松庭织物店", "织物")],
}


def seed_expansion_occupancy(layout, baseline):
    original_homes = {block["homeId"] for block in baseline["blocks"]}
    residents = []
    for region, plan in PLAN.items():
        tier = plan["tier"]
        homes = [block for block in layout["blocks"] if block["tier"] == tier and block["homeId"] not in original_homes]
        shops = [place for place in layout["places"] if place.get("tier") == tier and place["id"].startswith("expansion_shop_")]
        occupied_homes = homes[::2]
        occupied_shops = shops[::2]
        assert len(homes) == sum(plan["homes"].values()) and len(shops) == sum(plan["shops"].values())
        assert len(occupied_homes) == len(HOUSEHOLDS[region]) and len(occupied_shops) == len(MERCHANTS[region])
        for home in homes:
            home["initiallyVacant"] = True
        for i, (home, name) in enumerate(zip(occupied_homes, HOUSEHOLDS[region])):
            home["initiallyVacant"] = False
            resident = dict(id="expansion_resident_" + home["homeId"], name=name, homeId=home["homeId"])
            if i < len(occupied_shops):
                shop = occupied_shops[i]
                shop["name"], shop["business"] = MERCHANTS[region][i]
                resident["workId"] = shop["id"]
            residents.append(resident)
    layout["expansionResidents"] = residents


class Polygon(list):
    def __init__(self, points):
        super().__init__(points)
        self.bounds=(min(x for x,y in self),min(y for x,y in self),max(x for x,y in self),max(y for x,y in self))


def footprint(x, y, width, depth, heading, pad=0):
    a = math.radians(heading)
    c, s = math.cos(a), math.sin(a)
    return Polygon([(x+c*u+s*v, y-s*u+c*v) for u, v in
            [(-width/2-pad, -pad), (width/2+pad, -pad),
             (width/2+pad, depth+pad), (-width/2-pad, depth+pad)]])


def overlap(a, b):
    aa,bb=a.bounds,b.bounds
    if aa[2]<=bb[0] or bb[2]<=aa[0] or aa[3]<=bb[1] or bb[3]<=aa[1]:return False
    for polygon in (a, b):
        for u, v in zip(polygon, polygon[1:]+polygon[:1]):
            axis = (u[1]-v[1], v[0]-u[0])
            aa = [x*axis[0]+y*axis[1] for x, y in a]
            bb = [x*axis[0]+y*axis[1] for x, y in b]
            if max(aa) <= min(bb)+1e-7 or max(bb) <= min(aa)+1e-7:
                return False
    return True


def inside(pt, polygon):
    x, y = pt
    result = False
    for a, b in zip(polygon, polygon[1:]+polygon[:1]):
        if (a[1] > y) != (b[1] > y) and x < (b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0]:
            result = not result
    return result


def strip(a, b, half):
    length = math.dist(a, b)
    nx, ny = -(b[1]-a[1])/length*half, (b[0]-a[0])/length*half
    return Polygon([(a[0]+nx,a[1]+ny),(b[0]+nx,b[1]+ny),(b[0]-nx,b[1]-ny),(a[0]-nx,a[1]-ny)])


if not BASELINE.exists():
    source = json.loads(TARGET.read_text(encoding="utf-8-sig"))
    if source["layoutVersion"] != 14:
        raise ValueError("Capture a V14 baseline before applying this expansion")
    BASELINE.write_text(json.dumps(source,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
base = json.loads(BASELINE.read_text(encoding="utf-8-sig"))
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--occupancy-only", action="store_true", help="Update V15 occupants without rebuilding any geometry.")
if parser.parse_args().occupancy_only:
    current = json.loads(TARGET.read_text(encoding="utf-8-sig"))
    if current["layoutVersion"] != 15:
        raise ValueError("Occupancy updates require the existing V15 layout")
    seed_expansion_occupancy(current, base)
    TARGET.write_text(json.dumps(current, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
    print("V15 occupancy written: 19 resident households and 12 merchants.")
    raise SystemExit(0)

d = copy.deepcopy(base)
d["layoutVersion"] = 15
d["expansionScale"] = SCALE
d["lakeSceneScale"] *= SCALE
for node in d["nodes"]:
    node["x"] *= SCALE; node["y"] *= SCALE
for p in d["places"]:
    p["entranceX"] = p.get("entranceX", 0)*SCALE
    p["entranceY"] = p.get("entranceY", 0)*SCALE
for block in d["blocks"]:
    block["x"] *= SCALE; block["y"] *= SCALE
for parcel in d["lakeParcels"]:
    for key in ("x", "y", "width", "depth"):
        parcel[key] *= SCALE
for p in d["lakeShore"]:
    p["x"] *= SCALE; p["y"] *= SCALE
for bridge in d["lakeBridges"]:
    for p in bridge["points"]:
        p["x"] *= SCALE; p["y"] *= SCALE
for edge in d["edges"]:
    if edge.get("length", 0): edge["length"] *= SCALE

nodes = {n["id"]: (n["x"], n["y"]) for n in d["nodes"]}
streets = {s["id"]: s["name"] for s in d["streets"]}
shapes, approaches, raw_footprints, references = [], [], [], []
for p in d["places"]:
    q = nodes[p["node"]]
    pos = (q[0]+p["entranceX"], q[1]+p["entranceY"])
    shapes.append(footprint(*pos,p["width"],p["depth"],p.get("heading",0),2))
    raw_footprints.append(_footprint(*pos,p))
    references.append((pos,p["street"]))
    if math.dist(q,pos)>.1: approaches.append(strip(q,pos,1.2))
for b in d["blocks"]:
    pos = (b["x"],b["y"])
    shapes.append(footprint(*pos,b["width"],b["depth"],b.get("heading",0),2))
    raw_footprints.append(_footprint(*pos,b))
    references.append((pos,b["street"]))
    q=nodes[b["entranceNode"]]
    if math.dist(q,pos)>.1: approaches.append(strip(q,pos,1.2))
for p in d["lakeParcels"]:
    shapes.append(footprint(p["x"],p["y"],p["width"],p["depth"],p.get("heading",0),1))
# Racetracks keep their driving geometry; only their city position changes.
for x,y,w,h in [(-470,-120,130,90),(120,-150,170,115),(390,90,220,145),(70,450,270,165)]:
    shapes.append(footprint(x*SCALE,y*SCALE-h/2-8,w+16,h+16,0,2))

shore=[]
control=[(p["x"],p["y"]) for p in d["lakeShore"]]
for i in range(len(control)):
    a,b,c,e=[control[(i+k)%len(control)] for k in [-1,0,1,2]]
    for j in range(10):
        t=j/10
        shore.append(tuple(.5*(2*b[k]+(-a[k]+c[k])*t+(2*a[k]-5*b[k]+4*c[k]-e[k])*t*t+(-a[k]+3*b[k]-3*c[k]+e[k])*t*t*t) for k in [0,1]))

edges=d["edges"][:]
roads=[strip(nodes[e["a"]],nodes[e["b"]],5.5) for e in edges]
gap_nodes=set(n for route in d.get("lakeGapRoutes",[]) for n in route)
street_edges=collections.defaultdict(list)
for index,edge in enumerate(edges):
    if edge["a"] in gap_nodes or edge["b"] in gap_nodes:continue
    if edge["a"].startswith(("race_","motor_")) or edge["b"].startswith(("race_","motor_")):continue
    a,b=nodes[edge["a"]],nodes[edge["b"]]
    if math.dist(a,b)<.5:continue
    middle=((a[0]+b[0])/2,(a[1]+b[1])/2)
    ref=min(references,key=lambda item:math.dist(item[0],middle))
    if math.dist(ref[0],middle)<100:street_edges[ref[1]].append(index)

splits=collections.defaultdict(list)
new_shapes=[]
counts=collections.Counter()
house_numbers=collections.defaultdict(int)
for b in d["blocks"]:house_numbers[b["street"]]=max(house_numbers[b["street"]],b.get("homeNumber",0))


def place_unit(region,kind,street,tier,ordinal):
    if kind=="home":
        width,depth=[(9.5,10.5),(17,17),(20,19),(26,29)][tier]
        size="small"
    elif kind=="hospital":
        width,depth,size=16,12,"medium"
    else:
        size=("medium" if ordinal%3==1 else "large" if ordinal%5==4 else "small")
        width,depth={"small":(12,11),"medium":(16,13),"large":(20,15)}[size]
    candidates=[]
    for ei in street_edges[street]:
        a,b=nodes[edges[ei]["a"]],nodes[edges[ei]["b"]]
        length=math.dist(a,b);ux,uy=(b[0]-a[0])/length,(b[1]-a[1])/length
        samples=max(2,math.ceil(length/6))
        for sample in range(samples+1):
            t=sample/samples;q=(a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t)
            for side in (-1,1):
                nx,ny=-uy*side,ux*side
                heading=math.degrees(math.atan2(nx,ny))
                for setback in (8,11,15,20,27,35,44):
                    front=(q[0]+nx*setback,q[1]+ny*setback)
                    poly=footprint(*front,width,depth,heading,2)
                    # Match production MountainHeight flat terraces in the enlarged landscape.
                    if any(y/d["lakeSceneScale"]>935 or x/d["lakeSceneScale"]>895 or (x/d["lakeSceneScale"]< -735 and y/d["lakeSceneScale"]> -255) for x,y in poly):continue
                    if any(overlap(poly,road) for road in roads):continue
                    if any(overlap(poly,shape) for shape in shapes):continue
                    if any(overlap(poly,path) for path in approaches):continue
                    if inside(front,shore) or any(inside(p,shore) for p in poly):continue
                    if any(_crosses_building(q,front,shape) for shape in raw_footprints):continue
                    # Prioritize short, clear door approaches and proximity to this street's buildings.
                    near=min(math.dist(front,r) for r,s in references if s==street)
                    candidates.append((setback*1.8+near*.18,ei,t,q,front,heading,poly))
    if not candidates:raise ValueError(f"No clear footprint for {region}/{street}/{kind}/{ordinal}")
    _,ei,t,q,front,heading,poly=min(candidates,key=lambda c:c[:3])
    # Share an existing route node at endpoints; otherwise split that same road segment.
    edge=edges[ei]
    if t<1e-7:node_id=edge["a"]
    elif t>1-1e-7:node_id=edge["b"]
    else:
        match=next((nid for old_t,nid in splits[ei] if abs(old_t-t)<1e-7),None)
        node_id=match or f"expansion_{len(d['nodes'])}"
        if not match:
            d["nodes"].append(dict(id=node_id,x=q[0],y=q[1]));nodes[node_id]=q;splits[ei].append((t,node_id))
    if kind=="home":
        house_numbers[street]+=1;number=house_numbers[street]
        model=["oldhome","apartment","luxury_apartment","villa"][tier]
        d["blocks"].append(dict(street=street,model=model,tier=tier,x=front[0],y=front[1],width=width,depth=depth,
            height=[6,14,22,6][tier],heading=heading,variant=number*7,columns=1,rows=1,
            homeId=f"residence_{street}_{number:02d}",homeNumber=number,entranceNode=node_id,initiallyVacant=True))
    elif kind=="hospital":
        d["places"].append(dict(id=f"hospital_{region}",name=HOSPITALS[region][1],kind="other",
            node=node_id,street=street,business="医院",size=size,explicitEntrance=True,tradable=False,
            entranceX=front[0]-q[0],entranceY=front[1]-q[1],width=width,depth=depth,heading=heading,
            model="office",tier=tier,openMinute=0,closeMinute=1440))
    else:
        number=ordinal+1
        d["places"].append(dict(id=f"expansion_shop_{street}_{number:02d}",name=f"{streets[street]} · {number}号招租商铺",kind="shop",
            node=node_id,street=street,business="空铺",size=size,explicitEntrance=True,tradable=True,
            entranceX=front[0]-q[0],entranceY=front[1]-q[1],width=width,depth=depth,heading=heading,model="shop",tier=tier,
            openMinute=540,closeMinute=1260))
    shapes.append(poly);new_shapes.append(poly);references.append((front,street))
    raw_footprints.append(_footprint(*front,dict(width=width,depth=depth,heading=heading)))
    approaches.append(strip(q,front,1.2));counts[(region,kind)]+=1


for region,plan in PLAN.items():
    for kind in ("shop","home"):
        for street,number in plan["shops" if kind=="shop" else "homes"].items():
            for i in range(number):place_unit(region,kind,street,plan["tier"],i)
            print(region,kind,street,number,flush=True)
for region,(street,_) in HOSPITALS.items():
    place_unit(region,"hospital",street,PLAN[region]["tier"],0)

d["edges"]=[]
for index,e in enumerate(edges):
    marks=[(0,e["a"])]+sorted(splits[index])+[(1,e["b"])]
    for (ta,a),(tb,b) in zip(marks,marks[1:]):
        new=dict(a=a,b=b)
        if e.get("length",0):new["length"]=e["length"]*(tb-ta)
        d["edges"].append(new)

# Geometry authoring validation, before replacing the live layout.
assert sum(n for (r,k),n in counts.items() if k=="home")==35
assert sum(n for (r,k),n in counts.items() if k=="shop")==22
assert sum(n for (r,k),n in counts.items() if k=="hospital")==4
assert len(d["places"])==len(base["places"])+26
assert len(d["blocks"])==len(base["blocks"])+35
adj=collections.defaultdict(set)
for e in d["edges"]:adj[e["a"]].add(e["b"]);adj[e["b"]].add(e["a"])
reached={d["nodes"][0]["id"]};pending=list(reached)
while pending:
    for node in adj[pending.pop()]-reached:reached.add(node);pending.append(node)
assert reached==set(nodes),"Disconnected door node"
assert all(not overlap(a,b) for i,a in enumerate(new_shapes) for b in new_shapes[i+1:])
assert len({p["id"] for p in d["places"]})==len(d["places"])
assert len({b["homeId"] for b in d["blocks"]})==len(d["blocks"])
seed_expansion_occupancy(d, base)
for place in d["places"]:
    if place["id"] == "life_hike":
        place["name"] = "城郊公园 · 青湾垂钓场"
TARGET.write_text(json.dumps(d,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
import runpy
runpy.run_path(str(ROOT / "tools/add-old-market.py"), run_name="__main__")
print("V15 written; all new front doors connected; authored footprints clear roads, gardens and buildings.")
for region in PLAN:print(region,"shops +",counts[(region,"shop")],"homes +",counts[(region,"home")])
