"""Append an independent market on a vacant, reachable plot; never replace a player shop."""
import ast
import json
import math
from pathlib import Path
from residence_access import _footprint, _crosses_building

ROOT = Path(__file__).resolve().parents[1]
target = ROOT / 'Assets/Resources/Data/city_layout.json'
d = json.loads(target.read_text(encoding='utf-8-sig'))
if any(p['id'] == 'life_oldmarket' for p in d['places']):
    print('Independent market already authored; unchanged.')
    raise SystemExit(0)
# Reuse only the pure polygon helpers, without running the city expansion script.
tree = ast.parse((ROOT / 'tools/expand-lake-city.py').read_text(encoding='utf-8-sig'))
helpers = [n for n in tree.body if isinstance(n, (ast.ClassDef, ast.FunctionDef)) and n.name in {'Polygon', 'footprint', 'overlap', 'inside', 'strip'}]
exec(compile(ast.Module(body=helpers, type_ignores=[]), '<geometry helpers>', 'exec'))
nodes = {n['id']: (n['x'], n['y']) for n in d['nodes']}
raw, shapes, approaches, refs = [], [], [], []
for p in d['places']:
    q = nodes[p['node']]; pos = (q[0] + p.get('entranceX', 0), q[1] + p.get('entranceY', 0))
    raw.append(_footprint(*pos, p)); shapes.append(footprint(*pos, p['width'], p['depth'], p.get('heading', 0), 2))
    refs.append((pos, p['street']))
    if math.dist(q, pos) > .1: approaches.append(strip(q, pos, 1.5))
for b in d['blocks']:
    pos = (b['x'], b['y']); q = nodes[b['entranceNode']]
    raw.append(_footprint(*pos, b)); shapes.append(footprint(*pos, b['width'], b['depth'], b.get('heading', 0), 2))
    refs.append((pos, b['street']))
    if math.dist(q, pos) > .1: approaches.append(strip(q, pos, 1.5))
for p in d['lakeParcels']:
    shapes.append(footprint(p['x'], p['y'], p['width'], p['depth'], p.get('heading', 0), 2))
roads = [strip(nodes[e['a']], nodes[e['b']], 6) for e in d['edges'] if math.dist(nodes[e['a']], nodes[e['b']]) > .01]
shore = [(p['x'], p['y']) for p in d['lakeShore']]
choices = []
for e in d['edges']:
    a, b = nodes[e['a']], nodes[e['b']]; length = math.dist(a, b)
    if length < 15 or min(refs, key=lambda r: math.dist(r[0], ((a[0]+b[0])/2, (a[1]+b[1])/2)))[1] != 's_oldhomes': continue
    ux, uy = (b[0]-a[0])/length, (b[1]-a[1])/length
    for t in [.25, .5, .75]:
        q = (a[0]+(b[0]-a[0])*t, a[1]+(b[1]-a[1])*t)
        for side in [-1, 1]:
            nx, ny = -uy*side, ux*side; heading = math.degrees(math.atan2(nx, ny))
            for setback in [9, 14, 20, 28]:
                front = (q[0]+nx*setback, q[1]+ny*setback); poly = footprint(*front, 22, 18, heading, 3)
                if any(overlap(poly, p) for p in shapes + roads + approaches): continue
                if any(inside(p, shore) for p in poly) or inside(front, shore): continue
                if any(_crosses_building(q, front, f) for f in raw): continue
                scale=d['lakeSceneScale']
                if any(y/scale > 935 or x/scale > 895 or (x/scale < -735 and y/scale > -255) for x,y in poly): continue
                near = min(math.dist(front, pos) for pos, street in refs if street == 's_oldhomes')
                choices.append((setback+near*.1, e, q, front, heading))
assert choices, 'No vacant plot with a clear road approach'
_, edge, q, front, heading = min(choices, key=lambda c: c[0])
node = 'oldmarket_entrance'
d['nodes'].append(dict(id=node, x=q[0], y=q[1]))
# Split only this road edge; all existing nodes and places keep their positions.
d['edges'].remove(edge)
for left, right in [(edge['a'], node), (node, edge['b'])]:
    segment = dict(a=left, b=right)
    if edge.get('length', 0): segment['length'] = edge['length'] * math.dist(nodes[edge['a']] if left != node else q, q if right == node else nodes[edge['b']]) / math.dist(nodes[edge['a']], nodes[edge['b']])
    d['edges'].append(segment)
d['places'].append(dict(id='life_oldmarket', name='青湾旧物市场', street='s_oldhomes', node=node,
    business='venue_oldmarket', kind='shop', size='small', explicitEntrance=True, entranceX=front[0]-q[0], entranceY=front[1]-q[1],
    width=22, depth=18, heading=heading, tradable=False, openMinute=540, closeMinute=1320, model='oldmarket', tier=0))
target.write_text(json.dumps(d, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print('Added separate market, four stalls and reachable road node. Collision checks passed; existing places unchanged.')
