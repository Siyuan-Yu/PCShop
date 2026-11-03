"""V13: approved four-neighbourhood / 24-hour plan; compact shared outskirts template.
Geometry is regenerated from the V8 source, never cumulatively warped.
"""
import json, math, heapq
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
d=json.loads((ROOT/'tools/data/city-layout-v8.json').read_text(encoding='utf-8-sig'))
old={n['id']:n for n in d['nodes']};places=d['places']
nodes={};edges=[];routes={};occupied=[];parcels=[];blocks=[]
def dist(a,b):return math.hypot(a[0]-b[0],a[1]-b[1])
def smooth(points,steps=12):
    out=[];p=[points[0]]+points+[points[-1]]
    for i in range(1,len(p)-2):
        a,b,c,e=p[i-1:i+3]
        for j in range(steps):
            t=j/steps
            out.append(tuple(.5*(2*b[k]+(-a[k]+c[k])*t+(2*a[k]-5*b[k]+4*c[k]-e[k])*t*t+(-a[k]+3*b[k]-3*c[k]+e[k])*t*t*t) for k in [0,1]))
    return out+[points[-1]]
def route(name,controls):
    p=smooth(controls);s=[0]
    for a,b in zip(p,p[1:]):s.append(s[-1]+dist(a,b))
    r=dict(name=name,p=p,s=s,length=s[-1],marks=[]);routes[name]=r;return r

def point(r,s):
    s=max(0,min(s,r['length']))
    for i in range(1,len(r['p'])):
        if r['s'][i]+1e-8>=s:
            a,b=r['p'][i-1:i+1];length=dist(a,b);t=(s-r['s'][i-1])/length
            return (a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t),((b[1]-a[1])/length,-(b[0]-a[0])/length)
    raise ValueError(s)
def mark(r,s,id):
    while any(abs(s-v[0])<.002 for v in r['marks']):s+=.007
    p,_=point(r,s);nodes[id]=dict(id=id,x=p[0],y=p[1]);r['marks'].append((s,id));return id
template=[(0,0),(130,10),(270,50),(400,100)]
main=[route('old',[(x-760,y-420) for x,y in template]),
      route('community',[(x+170,y-320) for x,y in template]),
      route('park',[(650,200),(690,330),(650,470),(560,550)]),
      route('garden',[(120,690),(-20,720),(-180,715),(-380,670)])]
for r in main:
    offset={'old':92,'community':92,'park':125,'garden':100}[r['name']]
    controls=[]
    for j in range(9):
        p,n=point(r,r['length']*j/8);controls.append((p[0]+n[0]*offset,p[1]+n[1]*offset))
    back=route(r['name']+'_back',controls)
    for end in [0,1]:
        pa,_=point(r,r['length']*end);pb,_=point(back,back['length']*end)
        link=route(r['name']+'_link'+str(end),[pa,pb])
        a=mark(r,r['length']*end,'lake_'+r['name']+'_join'+str(end))
        b=mark(back,back['length']*end,'lake_'+r['name']+'_backjoin'+str(end))
        link['marks']=[(0,a),(link['length'],b)]
# Long, unbuilt interdistrict roads; no parallel shortcut edges between neighbourhoods.
gap_controls=[ [(-360,-320),(-260,-370),(-130,-340),(10,-365),(90,-315),(170,-320)],
               [(570,-220),(610,-100),(560,10),(625,100),(650,200)],
               [(560,550),(470,600),(410,700),(300,680),(215,735),(120,690)] ]
for i,(a,b) in enumerate(zip(main,main[1:])):
    r=route('gap'+str(i+1),gap_controls[i]);r['marks']=[(0,'lake_'+a['name']+'_join1'),(r['length'],'lake_'+b['name']+'_join0')]
# Outskirts and community use the exact same three-street planning template.
for name in ['old','community']:
    r=routes[name];controls=[]
    for j in range(9):
        p,n=point(r,r['length']*j/8);controls.append((p[0]+n[0]*46,p[1]+n[1]*46))
    mid=route(name+'_middle',controls)
    for end in [0,1]:
        at=point(mid,mid['length']*end)[0];link=routes[name+'_link'+str(end)]
        id=mark(mid,mid['length']*end,'lake_'+name+'_middlejoin'+str(end));link['marks'].append((dist(point(link,0)[0],at),id))
# Short residential cross streets form courts rather than two isolated ribbons of buildings.
for name in ['old','community','park','garden']:
    r=routes[name];back=routes[name+'_back']
    for i,fraction in enumerate([.34,.69]):
        a=mark(r,r['length']*fraction,'lake_'+name+'_court'+str(i))
        b=mark(back,back['length']*fraction,'lake_'+name+'_backcourt'+str(i))
        pa=point(r,r['length']*fraction)[0];pb=point(back,back['length']*fraction)[0]
        link=route(name+'_court'+str(i),[pa,((pa[0]+pb[0])/2+5,(pa[1]+pb[1])/2),pb])
        link['marks']=[(0,a),(link['length'],b)]
        if name in ['old','community']:
            # Split the cross street at the middle street's point, forming a real navigable junction.
            mid=routes[name+'_middle'];pm=point(mid,mid['length']*fraction)[0]
            id=mark(mid,mid['length']*fraction,'lake_'+name+'_midcourt'+str(i))
            link=route(name+'_court'+str(i),[pa,pm,pb]);link['marks']=[(0,a),(link['s'][12],id),(link['length'],b)]
# The northern endpoint sits beyond every estate road, connected through the centre of the villas.
north='lake_north_terminal';back=routes['garden_back'];s=back['length']*.96
a=mark(back,s,'lake_estate_north_junction');pa=point(back,s)[0]
north_route=route('north_approach',[pa,(pa[0]-15,810),(pa[0],875)])
north_route['marks']=[(0,a)];mark(north_route,north_route['length'],north)
# Build road samples first so parcel fitting checks every future road, including cross streets.
road_segments=[(a,b) for r in routes.values() for a,b in zip(r['p'],r['p'][1:])]
def poly(x,y,w,depth,heading,pad=0):
    a=math.radians(heading);c,s=math.cos(a),math.sin(a)
    return [(x+c*u+s*v,y-s*u+c*v) for u,v in [(-w/2-pad,-pad),(w/2+pad,-pad),(w/2+pad,depth+pad),(-w/2-pad,depth+pad)]]
def overlap(a,b):
    for p in [a,b]:
        for u,v in zip(p,p[1:]+p[:1]):
            axis=(u[1]-v[1],v[0]-u[0]);aa=[x*axis[0]+y*axis[1] for x,y in a];bb=[x*axis[0]+y*axis[1] for x,y in b]
            if max(aa)<=min(bb) or max(bb)<=min(aa):return False
    return True
# Road buffers are true oriented strips; long diagonal roads do not reserve giant rectangles.
road_polys=[]
for a,b in road_segments:
    length=dist(a,b)
    if length<1e-6:continue
    nx,ny=(b[1]-a[1])/length,-(b[0]-a[0])/length
    road_polys.append([(a[0]+nx*4.2,a[1]+ny*4.2),(b[0]+nx*4.2,b[1]+ny*4.2),(b[0]-nx*4.2,b[1]-ny*4.2),(a[0]-nx*4.2,a[1]-ny*4.2)])
def candidate(r,s,w,depth,side=1,setback=8.5):
    at,n=point(r,s);n=(n[0]*side,n[1]*side);heading=math.degrees(math.atan2(n[0],n[1]));front=(at[0]+n[0]*setback,at[1]+n[1]*setback)
    shape=poly(*front,w,depth,heading,2)
    if any(overlap(shape,o) for o in occupied):return None
    if any(overlap(shape,o) for o in road_polys):return None
    return at,n,heading,front,shape
assignment={
 's_start':['old','old_middle','old_back'],'s_oldhomes':['old_back','old_middle','old'], 's_electronics':['old','old_middle','old_back'],
 's_services':['community_back','community_middle'],'s_market':['community','community_middle'],
 's_sports':['park_back'],'s_creative':['park'],'s_expo':['park_back'],
 's_night':['garden'],'s_newtown':['garden_back']}
# A recognisable central fountain square anchors the community district.
for name in ['old','community']:
    r=routes[name+'_back'];c=candidate(r,r['length']*.53,32,25,-1)
    if c:
        at,n,h,front,shape=c;occupied.append(shape)
        parcels.append(dict(x=front[0],y=front[1],width=32,depth=25,heading=h,kind='fountain'))
r=routes['park_back']
c=candidate(r,r['length']*.51,48,65,-1)
if c:
    at,n,h,front,shape=c;occupied.append(shape)
    parcels.append(dict(x=front[0],y=front[1],width=48,depth=65,heading=h,kind='park'))
# Place landmark venues first; distribute all 90 functional doors along the curved streets.
placement_order={p['id']:i for i,p in enumerate(sorted(places,key=lambda p:-(44*24 if p['street']=='s_electronics' and p['model']=='warehouse' else p['width']*p['depth'])))}
for p in places:
    if p['street']=='s_electronics' and p['model']=='warehouse':p.update(width=27,depth=16)
    if p['street']=='s_oldhomes' and p['model']=='oldhome':p.update(width=10,depth=10)
    if p['street'] in ['s_night','s_newtown'] and p['model']=='shop':p.update(width=18,depth=17)
    if p['id'] in ['life_restaurant_tier4','life_ktv_tier4','life_bar_tier4']:p.update(width=30,depth=21)
garden_commerce=['life_unit_s_night_0','life_restaurant_tier4','life_unit_s_newtown_0','life_ktv_tier4','life_unit_s_night_2','life_bar_tier4','life_unit_s_newtown_2']
ordered=sorted(places,key=lambda p:(0 if p['id']=='home' else 1,-p['width']*p['depth']))
for index,p in enumerate(ordered):
    index=placement_order[p['id']]
    options=assignment[p['street']];chosen=None
    if p['id']=='life_expo':
        options=['gap2'];p.update(street='s_services',tier=1)
    if p['id'] in garden_commerce:options=['garden']
    for name in options:
        r=routes[name]
        # Evenly spread the search starting position; large venues should not all collect at one end.
        steps=list(range(24,int(r['length'])-24,2));offset=0 if p['model']=='warehouse' else (index*19)%len(steps);steps=steps[offset:]+steps[:offset]
        if p['id']=='life_expo':steps=sorted([s for s in steps if .15*r['length']<s<.35*r['length']],key=lambda s:abs(s-r['length']*.25))
        if name.startswith('old'):
            lo,hi=(.025,.30) if p['street']=='s_electronics' else (.72,.96) if p['street']=='s_oldhomes' else (.31,.70)
            steps=[s for s in steps if lo*r['length']<=s<=hi*r['length']]
            if p['id']=='home':steps=sorted(steps,key=lambda s:abs(s-r['length']*.95))
        if p['id'] in garden_commerce:
            target=r['length']*.5+(garden_commerce.index(p['id'])-3)*33
            steps=sorted(steps,key=lambda s:abs(s-target))
        for side in ([1,-1] if name.endswith(('_back','_middle')) else [1]):
            for s in steps:
                chosen=candidate(r,s,p['width'],p['depth'],side)
                if chosen:break
            if chosen:break
        if chosen:break
    if not chosen:raise ValueError(('No functional parcel',p['id']))
    at,n,heading,front,shape=chosen;occupied.append(shape);mark(r,s,p['node'])
    p.update(entranceX=n[0]*8.5,entranceY=n[1]*8.5,heading=heading,explicitEntrance=True)
    if p['street']=='s_night' and p['model']=='luxury_apartment':p['model']='villa'
# Courtyard residences: varied silhouettes, fewer repetitive filler blocks, wider green setbacks.
specs=[('old_back','s_oldhomes','oldhome',0,16,[(9,10,6),(10,11,7),(12,12,8)]),
       ('old_middle','s_oldhomes','oldhome',0,12,[(9,10,6),(10,11,7),(12,12,8)]),
       ('community_back','s_services','apartment',1,18,[(18,18,17),(23,19,22),(27,20,25)]),
       ('park_back','s_sports','luxury_apartment',2,16,[(21,20,27),(25,22,34),(30,23,39)]),
       ('garden_back','s_newtown','villa',3,28,[(27,32,11),(32,35,11),(36,36,11)])]
for name,street,model,tier,count,sizes in specs:
    r=routes[name];placed=0
    for wanted in range(count):
        w,depth,h=sizes[wanted%len(sizes)]
        desired=25+(r['length']-50)*(wanted+.5)/count
        chosen=None
        search=sorted(range(22,int(r['length'])-22,3),key=lambda s:abs(s-desired))
        if name.startswith('old'):search=[s for s in search if .72*r['length']<=s<=.96*r['length']]
        for side in ([1,-1] if wanted%2==0 else [-1,1]):
            for s in search:
                chosen=candidate(r,s,w,depth,side)
                if chosen:break
            if chosen:break
        if not chosen:continue
        at,n,heading,front,shape=chosen;occupied.append(shape)
        blocks.append(dict(street=street,model=model,tier=tier,x=front[0],y=front[1],heading=heading,width=w,depth=depth,height=h,variant=wanted*7,columns=1,rows=1));placed+=1
    print(street,placed,'courtyard buildings')
# Buildings inside the courts provide district depth; these are scenery, not extra functional shops.
for name,street,model,tier,count in [('community','s_services','apartment',1,8),('park','s_sports','luxury_apartment',2,10)]:
    r=routes[name];added=0
    for s in range(28,int(r['length'])-28,12):
        w,depth,h=(18,20,19) if tier==1 else (22,24,32)
        c=candidate(r,s,w,depth,1,42 if tier==1 else 49)
        if not c:continue
        at,n,heading,front,shape=c;occupied.append(shape)
        blocks.append(dict(street=street,model=model,tier=tier,x=front[0],y=front[1],heading=heading,width=w,depth=depth,height=h,variant=added*9+3,columns=1,rows=1));added+=1
        if added>=count:break
    print(street,added,'interior courtyard buildings')
# Landscape parcels fill the remaining frontage with proper gardens instead of bare lawns.
for name in ['old','old_middle','old_back','community','community_middle','community_back','park','park_back','garden','garden_back']:
    r=routes[name]
    for side in [1,-1]:
        for s in range(20,int(r['length'])-20,16):
            w,depth=13,16;c=candidate(r,s,w,depth,side)
            if not c:continue
            at,n,heading,front,shape=c
            # Lakeside gardens stop before the water; decorative parcels only.
            if side<0 and not name.endswith(('_back','_middle')):depth=9;shape=poly(*front,w,depth,heading,1)
            occupied.append(shape);parcels.append(dict(x=front[0],y=front[1],width=w,depth=depth,heading=heading,kind='garden' if 'garden' in name else 'plaza'))
# Old non-door nodes remain on the same district streets for save compatibility.
used={p['node'] for p in places}
for i,(id,n) in enumerate(old.items()):
    if id in used:continue
    t=min(3,int(n['y']//135));r=main[t]
    fraction=max(.02,min(.98,(n['x']+205)/470));s=r['length']*fraction+(i%7)*.013
    mark(r,s,id)
# Attach all samples and door marks to their respective street, preserving exact doorway access.
for name,r in routes.items():
    for i in range(1,int(r['length']//9)):
        s=i*9
        if any(abs(s-v[0])<.08 for v in r['marks']):continue
        mark(r,s,'lake_'+name+'_'+str(i))
    if not any(abs(s)<.1 for s,_ in r['marks']):mark(r,0,'lake_'+name+'_start')
    if not any(abs(s-r['length'])<.1 for s,_ in r['marks']):mark(r,r['length'],'lake_'+name+'_finish')
    marks=sorted(r['marks'])
    edges.extend(dict(a=a[1],b=b[1]) for a,b in zip(marks,marks[1:]) if a[1]!=b[1])
def distance(a,b):
    adj={n:[] for n in nodes}
    for e in edges:
        u,v=e['a'],e['b'];length=dist((nodes[u]['x'],nodes[u]['y']),(nodes[v]['x'],nodes[v]['y']))
        adj[u].append((v,length));adj[v].append((u,length))
    best={a:0};q=[(0,a)]
    while q:
        cost,u=heapq.heappop(q)
        if u==b:return cost
        if cost!=best[u]:continue
        for v,length in adj[u]:
            if cost+length<best.get(v,float('inf')):best[v]=cost+length;heapq.heappush(q,(cost+length,v))
    raise ValueError('disconnected')
south='lake_south_terminal';nodes[south]=dict(id=south,x=-780,y=-540);edges.append(dict(a=south,b='lake_old_backjoin0'))
base=distance(south,north)
# Only a small final physical calibration; most extra distance is in the three real landscape roads.
# No hidden edge-length multiplier and no change to walking speed.
scale=3528/base
for n in nodes.values():n['x']*=scale;n['y']*=scale
for p in places:
    for key in ['entranceX','entranceY','width','depth']:p[key]*=scale
for item in blocks+parcels:
    for key in ['x','y','width','depth']:item[key]*=scale
    if 'height' in item:item['height']*=scale
# Lake follows the town frontage, with three wooded western promontories and an eastern inlet.
shore=[(-710,-355),(-550,-350),(-375,-250),(-270,-300),(-160,-270),(-50,-300),(80,-250),(185,-255),(360,-220),(515,-130),(505,0),(565,110),(595,230),(610,370),(520,485),(410,535),(340,625),(220,605),(110,630),(-30,660),(-180,650),(-350,610),(-470,540),(-560,460),(-535,355),(-635,300),(-590,190),(-685,135),(-630,20),(-720,-70),(-675,-170)]
# Bridge ramps meet existing boulevard nodes; no shortcut edges enter the graph yet.
def bridge_start(r,s):return point(r,s)[0]
a=bridge_start(main[0],210);b=bridge_start(main[2],175);c=bridge_start(main[1],290);e=bridge_start(main[3],360)
bridges=[dict(id='lake_bridge_1',name='跨湖道路①',requiredTier=2,points=smooth([a,(a[0]+35,a[1]+75),(-150,0),(230,210),b],6)),dict(id='lake_bridge_2',name='跨湖道路②',requiredTier=3,points=smooth([c,(300,-80),(25,210),(-190,460),e],6))]
for bridge in bridges:bridge['points']=[dict(x=x*scale,y=y*scale) for x,y in bridge['points']]
d.update(layoutVersion=13,lakeCity=True,lakeSceneScale=scale,lakeShore=[dict(x=x*scale,y=y*scale) for x,y in shore],lakeBridges=bridges,lakeParcels=parcels,nodes=list(nodes.values()),edges=edges,blocks=blocks,lakeSouthNode=south,lakeNorthNode=north,
         lakeGapRoutes=[[id for _,id in sorted(routes['gap'+str(i)]['marks'])] for i in [1,2,3]])
# Patrols still use stable nodes; their route planner resolves the new curving roads.
(ROOT/'Assets/Resources/Data/city_layout.json').write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('V13:',len(nodes),'nodes;',len(places),'functional places;',len(blocks),'scenery buildings;',len(parcels),'gardens')
print('physical scale',scale,'shortest walk',distance(south,north)/2.45,'game minutes')
