"""Static scene/navigation acceptance; no game simulation or save access."""
import json, math, heapq
from pathlib import Path
root=Path(__file__).resolve().parents[1]
d=json.loads((root/'Assets/Resources/Data/city_layout.json').read_text(encoding='utf-8-sig'))
base=json.loads((root/'tools/data/city-layout-v8.json').read_text(encoding='utf-8-sig'))
nodes={n['id']:n for n in d['nodes']}
assert len(nodes)==len(d['nodes'])
assert {p['id'] for p in d['places']}=={p['id'] for p in base['places']}
assert {n['id'] for n in base['nodes']}<=nodes.keys()
assert len([p for p in d['places'] if p['kind']=='agency'])==4
adj={n:[] for n in nodes};seen=set()
for e in d['edges']:
    a,b=e['a'],e['b'];key=tuple(sorted((a,b)));assert key not in seen;seen.add(key)
    length=math.hypot(nodes[a]['x']-nodes[b]['x'],nodes[a]['y']-nodes[b]['y']);assert length>0
    adj[a].append((b,length));adj[b].append((a,length))
start=d['lakeSouthNode'];q=[(0,start)];best={start:0}
while q:
    cost,a=heapq.heappop(q)
    if cost!=best[a]:continue
    for b,length in adj[a]:
        if cost+length<best.get(b,float('inf')):best[b]=cost+length;heapq.heappush(q,(cost+length,b))
assert len(best)==len(nodes),set(nodes)-best.keys()
assert abs(best[d['lakeNorthNode']]-3528)<.001
assert nodes[d['lakeNorthNode']]['y']==max(n['y'] for n in nodes.values())
assert nodes[d['lakeSouthNode']]['y']==min(n['y'] for n in nodes.values())
assert len(d['lakeBridges'])==2
assert not any(n.startswith('lake_bridge') for n in nodes)
assert d['distanceScale']==1 and d['uniformRoadScale']==1
assert all(not e.get('length') for e in d['edges'])
# All interdistrict traffic must use the long physical corridors, not a hidden graph shortcut.
for route in d['lakeGapRoutes']:
    length=sum(math.hypot(nodes[a]['x']-nodes[b]['x'],nodes[a]['y']-nodes[b]['y']) for a,b in zip(route,route[1:]))
    assert length>350
    for node in route[1:-1]:assert len(adj[node])==2,(node,'unexpected bypass')
    print('Gap physical length:',round(length,2),'walking minutes:',round(length/2.45,1))
# Outskirts zoning is west wholesale, central commerce, east homes; both districts share translated geometry.
places={p['id']:p for p in d['places']}
def px(p):return nodes[p['node']]['x']+p.get('entranceX',0)
warehouses=[px(p) for p in d['places'] if p['street']=='s_electronics' and p['model']=='warehouse']
commerce=[px(p) for p in d['places'] if p['street']=='s_start']
assert max(warehouses)<min(commerce)<max(commerce)<px(places['home'])
scale=d['lakeSceneScale']
for suffix in ['join0','join1','backjoin0','backjoin1','middlejoin0','middlejoin1','court0','court1','backcourt0','backcourt1','midcourt0','midcourt1']:
    a=nodes['lake_old_'+suffix];b=nodes['lake_community_'+suffix]
    assert abs((b['x']-a['x'])/scale-930)<.001 and abs((b['y']-a['y'])/scale-100)<.001
print(f"PASS: all {len(places)} places, 4 agencies and legacy node IDs preserved; all nodes connected")
print('PASS: shortest full route = 3528 units = 1440 game minutes; both bridges excluded from navigation/manual corridors')
def footprint(x,y,w,depth,angle):
    a=math.radians(angle);c,s=math.cos(a),math.sin(a)
    return [(x+c*u+s*v,y-s*u+c*v) for u,v in [(-w/2,0),(w/2,0),(w/2,depth),(-w/2,depth)]]
def overlap(a,b):
    for p in [a,b]:
        for u,v in zip(p,p[1:]+p[:1]):
            axis=(u[1]-v[1],v[0]-u[0]);aa=[x*axis[0]+y*axis[1] for x,y in a];bb=[x*axis[0]+y*axis[1] for x,y in b]
            if max(aa)<=min(bb) or max(bb)<=min(aa):return False
    return True
shapes=[]
for p in d['places']:
    n=nodes[p['node']];shapes.append((p['id'],footprint(n['x']+p.get('entranceX',0),n['y']+p.get('entranceY',0),p['width'],p['depth'],p.get('heading',0))))
for i,b in enumerate(d['blocks']):shapes.append(('block'+str(i),footprint(b['x'],b['y'],b['width'],b['depth'],b.get('heading',0))))
issues=[]
for i,(name,shape) in enumerate(shapes):
    for other,o in shapes[i+1:]:
        if overlap(shape,o):issues.append((name,other))
assert not issues,('Building overlap',issues)
for e in d['edges']:
    a=nodes[e['a']];b=nodes[e['b']];dx=b['x']-a['x'];dy=b['y']-a['y'];length=math.hypot(dx,dy)
    nx=dy/length*2.8;ny=-dx/length*2.8
    road=[(a['x']+nx,a['y']+ny),(b['x']+nx,b['y']+ny),(b['x']-nx,b['y']-ny),(a['x']-nx,a['y']-ny)]
    for name,shape in shapes:
        if overlap(road,shape):issues.append((e,name))
assert not issues,('Road/building conflicts',issues)
print('PASS: all rotated building footprints clear one another and all road segments')
# Match the flat envelope of MountainHeight: no buildings or walkable streets inside rising terrain.
scale=d['lakeSceneScale']
for name,shape in shapes:
    for x,y in shape:
        x/=scale;y/=scale
        assert y<=940 and x<=900 and (x>=-740 or y<=-250),(name,'rising terrain',x,y)
for n in nodes.values():
    x=n['x']/scale;y=n['y']/scale
    assert y<=940 and x<=900 and (x>=-740 or y<=-250),(n['id'],'rising terrain',x,y)
print('PASS: roads and buildings stay on flat terrain; termini are the actual northernmost/southernmost road points')
# Use the same closed Catmull-Rom shoreline as the renderer, rather than its coarse control polygon.
control=[(p['x'],p['y']) for p in d['lakeShore']];shore=[]
for i in range(len(control)):
    a,b,c,e=[control[(i+k)%len(control)] for k in [-1,0,1,2]]
    for j in range(10):
        t=j/10
        shore.append(tuple(.5*(2*b[k]+(-a[k]+c[k])*t+(2*a[k]-5*b[k]+4*c[k]-e[k])*t*t+(-a[k]+3*b[k]-3*c[k]+e[k])*t*t*t) for k in [0,1]))
def in_lake(x,y):
    inside=False
    for a,b in zip(shore,shore[1:]+shore[:1]):
        if (a[1]>y)!=(b[1]>y) and x<(b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0]:inside=not inside
    return inside
for name,shape in shapes:
    assert not any(in_lake(x,y) for x,y in shape),(name,'building in water')
for n in nodes.values():assert not in_lake(n['x'],n['y']),(n['id'],'road in water')
print('PASS: land buildings and walkable roads remain outside the rendered lake')
