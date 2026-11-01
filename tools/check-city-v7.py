"""Configuration integrity only; does not run Unity or touch any save."""
import json, subprocess
from pathlib import Path
from collections import Counter, defaultdict, deque

root=Path(__file__).resolve().parents[1]
d=json.loads((root/'Assets/Resources/Data/city_layout.json').read_text(encoding='utf-8-sig'))
n={n['id']:n for n in d['nodes']};p={p['id']:p for p in d['places']}
assert len(n)==len(d['nodes']) and len(p)==len(d['places'])
assert len(d['streets'])==10
assert len([x for x in p.values() if x.get('tradable')])>=20
assert len([x for x in p.values() if x['kind']=='station'])==8
assert Counter(x['street'] for x in p.values() if x['kind']=='agency')==Counter(s['id'] for s in d['streets'])
for kind in ['restaurant','ktv','bar']:
 venues=[x for x in p.values() if x.get('business')=='venue_'+kind]
 assert len(venues)==4,(kind,len(venues))
 assert {x['tier'] for x in venues}=={0,1,2,3}
 assert {x['street'] for x in venues}=={'s_start','s_market','s_creative','s_night'}
assert all(x['street']=='s_electronics' for x in p.values() if x['kind']=='wholesaler' or x['model']=='warehouse')
assert all(x['street']!='s_start' for x in p.values() if x['kind']=='station')
assert {p['home']['tier'],p['home_tier1']['tier'],p['home_tier2']['tier'],p['home_tier3']['tier']}=={0,1,2,3}
adj=defaultdict(list);seen=set()
for e in d['edges']:
 a,b=n[e['a']],n[e['b']];key=tuple(sorted([e['a'],e['b']]))
 assert key not in seen;seen.add(key)
 assert (a['x']==b['x']) != (a['y']==b['y']),('not orthogonal/nonzero',e,a,b)
 adj[e['a']].append(e['b']);adj[e['b']].append(e['a'])
visited=set();q=deque([next(iter(n))])
while q:
 x=q.popleft()
 if x in visited:continue
 visited.add(x);q.extend(adj[x])
assert len(visited)==len(n),('disconnected',set(n)-visited)
for x in p.values():assert x['node'] in visited
legacy=json.loads(subprocess.check_output(['git','show','HEAD:Assets/Resources/Data/city_layout.json'],cwd=root).decode('utf-8-sig'))
assert {x['id'] for x in legacy['places']}<=set(p),'lost legacy places'
assert {x['id'] for x in legacy['nodes']}<=set(n),'lost legacy nodes'
rects=[]
for x in p.values():
 at=n[x['node']];front=at['y']+x['entranceY'];back=front+(1 if x['entranceY']>0 else -1)*x['depth']
 rect=(at['x']-x['width']/2,min(front,back),at['x']+x['width']/2,max(front,back))
 for id,other in rects:
  assert not(rect[0]<other[2] and rect[2]>other[0] and rect[1]<other[3] and rect[3]>other[1]),('overlap',id,x['id'])
 rects.append((x['id'],rect))
print('PASS: 10 streets / 10 agencies / 8 stations / 12 tiered venues / 4 homes')
print('PASS: all graph edges orthogonal, every node/place reachable, all legacy IDs retained, no interactive building overlap')
print('Nodes:',len(n),'edges:',len(d['edges']),'places:',len(p),'compound buildings:',len(d['blocks']))

# Check exactly the scenery that the runtime now renders (no second proximity cull).
import runpy
from collections import Counter
scenery=runpy.run_path(str(Path(__file__).with_name('densify-city.py')))
reserved=scenery['reserved_rectangles'](d)
counts=Counter(b['street'] for b in d['blocks'])
for street,minimum in [('s_oldhomes',45),('s_services',25),('s_sports',25),('s_newtown',35)]:
 assert counts[street]>=minimum,('residential density',street,counts[street])
for block in d['blocks']:
 rect=scenery['building_rect'](block,.75)
 assert not any(scenery['overlap'](rect,r) for r in reserved),('scenery overlaps circulation/buildings',block)
 reserved.append(rect)
print('PASS: residential density floors, every scenery footprint clear of roads/door aisles/actual buildings/other scenery')
print('Rendered housing scenery:',dict(counts),'total',sum(counts.values()))

for street in ['s_oldhomes','s_services','s_sports','s_newtown']:
 blocks=[b for b in d['blocks'] if b['street']==street]
 assert len({b['width'] for b in blocks})>=3,('uniform building widths',street)
 assert len({b.get('height',0) for b in blocks})>=3,('uniform skyline',street)
print('PASS: each housing district has at least 3 building widths and heights')
