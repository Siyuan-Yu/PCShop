"""Deterministically update the real city configuration, retaining legacy place/node IDs.
V7: four living circles, 10 agency jurisdictions, 8 distributed metro entrances.
"""
import json
from pathlib import Path

path = Path(__file__).resolve().parents[1] / 'Assets/Resources/Data/city_layout.json'
data = json.loads(path.read_text(encoding='utf-8-sig'))
old_nodes = data['nodes']
places = data['places']
streets = [
 ('s_oldhomes','01 城郊旧居街','低级住宅'),('s_electronics','02 城外批发街','集中批发'),
 ('s_start','03 创业商业街','平价商业'),('s_services','04 宜居社区街','中级住宅'),
 ('s_market','05 社区商业街','中档商业'),('s_creative','06 品质商业街','高档商业'),
 ('s_sports','07 公园公寓街','高级住宅'),('s_expo','08 会展商务街','会展与办公'),
 ('s_night','09 顶奢商业街','顶奢商业'),('s_newtown','10 花园别墅街','豪华住宅·东西片')]
names={s[0]:s[1] for s in streets}
# District bands are 90 view meters high; roads at 0/30/60 provide local streets.
origin={'s_oldhomes':(-180,0),'s_start':(10,0),'s_electronics':(130,0),
 's_services':(-180,90),'s_market':(10,90),'s_sports':(-180,180),
 's_creative':(10,180),'s_expo':(130,180),'s_night':(-90,270),'s_newtown':(30,270)}
nodes={}; loc={}; assigned={}; slot={}; node_owner={}
def node(id,x,y):
    x,y=round(x,3),round(y,3)
    # Distinct old IDs may share an intersection; short orthogonal alias spurs below.
    if (x,y) in loc and loc[(x,y)]!=id: x+=.01
    nodes[id]={'id':id,'x':x,'y':y};loc[(x,y)]=id
    return id
def put(p,street,x,y,model=None,tier=0,width=12,depth=12):
    p.update(street=street,explicitEntrance=True,entranceX=0,entranceY=6,
      width=width,depth=depth,heading=0,model=model or ('shop' if p['kind']!='home' else 'apartment'),tier=tier)
    if p['node'] in node_owner and node_owner[p['node']]!=p['id']:p['node']='v7_door_'+p['id']
    node_owner[p['node']]=p['id']
    node(p['node'],x,y);assigned[p['id']]=True
def find(id):return next(p for p in places if p['id']==id)
def add(id,name,kind,street,x,y,model,tier=0,business='',width=12,depth=12):
    if id in [p['id'] for p in places]:p=find(id)
    else:
        p=dict(id=id,name=name,kind=kind,node='v7_'+id,size='medium',business=business)
        places.append(p)
    p.update(name=name,kind=kind,business=business)
    put(p,street,x,y,model,tier,width,depth)
    return p
# The old home is retained at its original stable ID; upgraded homes have real addresses.
put(find('home'),'s_oldhomes',-165,0,'oldhome',0,12,12)
find('home')['name']='城郊老破小出租屋'
for tier,street,x,y in [(1,'s_services',-165,90),(2,'s_sports',-165,180),(3,'s_newtown',65,270)]:
    add('home_tier'+str(tier),['','宜居改善住宅','公园豪华公寓','花园独栋别墅'][tier],
        'home',street,x,y,['','apartment','luxury_apartment','villa'][tier],tier,width=16,depth=15)
# Legacy commercial frontage remains a coherent three-lane local shopping area.
shop_ids=['player_shop','shop07','shop02','shop04','shop01','shop05','dealer','shop08','shop09','shop10','shop12','shop13','shop14','shop15','shop16','shop17','shop18','shop19','shop03']
for i,id in enumerate(shop_ids):
    p=find(id);p['tradable']=True;put(p,'s_start',18+(i%6)*17,(i//6)*30,'shop',0,14,12)
# One agency per stable jurisdiction, including both villa parcels as a SINGLE street.
for street,x,y in [('s_start',112,60),('s_electronics',245,60),('s_oldhomes',-20,0),('s_services',-20,90),('s_market',112,150),('s_creative',112,240),('s_sports',-20,180),('s_expo',245,240),('s_night',-6,270),('s_newtown',112,270)]:
    p=find('agency' if street=='s_start' else 'agency_'+street)
    p['name']=names[street]+'房产中介';put(p,street,x,y,'agency',0,10,10)
# Metro IDs metro_a/b remain valid. No station in the starter commercial street.
for id,name,street,x,y in [('metro_a','城郊站','s_oldhomes',-185,0),('metro_b','批发市场站','s_electronics',140,0),
 ('metro_community','宜居社区站','s_services',-95,90),('metro_center','中心商业站','s_market',65,90),
 ('metro_park','公园公寓站','s_sports',-20,210),('metro_expo','会展站','s_expo',140,180),
 ('metro_luxury','顶奢商业站','s_night',-85,270),('metro_garden','花园站','s_newtown',75,270)]:
    add(id,name,'station',street,x,y,'station',width=8,depth=8)
for i,id in enumerate(['stall_cpu','stall_gpu','stall_mem','stall_psu','clothing_wholesale','model_market']):
    p=find(id);p['tradable']=True;put(p,'s_electronics',150+i%3*33,30+i//3*30,'warehouse',0,27,16)
# Base IDs retain existing activity/story references, with nine additional tiered venues.
for tier,street,x,y in [(0,'s_start',20,90),(1,'s_market',20,180),(2,'s_creative',20,270),(3,'s_night',-80,300)]:
    # Use north-facing rows at the previous band's upper local road, with safe southern frontage.
    for k,kind in enumerate(['restaurant','ktv','bar']):
        id='life_'+kind+('' if tier==0 else '_tier'+str(tier+1))
        p=add(id,['街坊','邻里','云庭','星苑'][tier]+['餐厅','歌房','酒吧'][k], 'shop',street,x+k*30,y-30,
            kind,tier,'venue_'+kind,24,16)
        p.update(openMinute=600 if kind=='restaurant' else 840 if kind=='ktv' else 1080,closeMinute=1320 if kind=='restaurant' else 120,tradable=False)
# Major destinations distributed by living circle, all outdoor recreation stays flat.
for id,street,x,y,model,tier,w,d in [
 ('life_expo','s_expo',190,210,'expo',2,60,28),('life_company','s_expo',155,240,'office',2,25,15),
 ('life_court','s_start',105,30,'court',0,14,15),('life_golf','s_night',-10,300,'golf',3,32,20),
 ('life_park','s_sports',-80,210,'park',2,45,25),('life_hike','s_oldhomes',-100,60,'park',0,28,16),
 ('life_market','s_market',45,120,'market',1,24,14),('life_studio','s_creative',80,210,'studio',2,18,12),
 ('model_workshop','s_creative',45,210,'shop',2,18,12),('clothing_boutique','s_creative',15,210,'shop',2,18,12),
 ('life_service','s_services',-135,120,'shop',1,16,12)]:
    put(find(id),street,x,y,model,tier,w,d)
find('life_hike')['name']='城郊平地散步公园'
add('life_court_mid','社区运动场','shop','s_market',100,120,'court',1,'venue_court',18,17)
add('life_fitness','公园健身场','shop','s_creative',85,240,'court',2,'venue_court',18,17)
for p in places:
    if p['id'] in assigned: continue
    street=p.get('street','s_start');i=slot.get(street,0);slot[street]=i+1
    ox,oy=origin[street]
    # Background homes in commercial neighborhoods are still valid stable resident addresses.
    x=ox+8+(i%4)*23;y=oy+30+(i//4)*30
    model='villa' if street=='s_newtown' and p['kind']=='home' else 'apartment' if p['kind']=='home' else 'shop'
    tier=3 if street=='s_newtown' else 2 if street in ['s_sports','s_creative','s_expo','s_night'] else 1 if street in ['s_services','s_market'] else 0
    if p['kind']=='home':model=['oldhome','apartment','luxury_apartment','villa'][tier]
    put(p,street,x,y,model,tier,16,12)
    if p['kind']=='home':p['name']=names[street]+'居民住宅'
    elif p['id'].startswith('life_unit_'):p['name']=names[street]+('西侧商铺' if p['id'].endswith('0') else '东侧商铺')
# Allocate disjoint frontage footprints; preserve IDs and actual entrance orientation.
areas={'s_oldhomes':(-195,-5,0),'s_start':(8,115,0),'s_electronics':(135,250,0),
 's_services':(-195,-5,90),'s_market':(8,115,90),'s_sports':(-195,-5,180),
 's_creative':(8,115,180),'s_expo':(135,250,180),'s_night':(-100,-5,270),'s_newtown':(15,115,270)}
occupied=[]
def footprint(p,x,y,side):
 front=y*1.5+side*6; back=front+side*p['depth']
 return (x-p['width']/2-1,min(front,back)-1,x+p['width']/2+1,max(front,back)+1)
def intersects(a,b):return a[0]<b[2] and a[2]>b[0] and a[1]<b[3] and a[3]>b[1]
for p in sorted(places,key=lambda p:(0 if p['kind']=='station' else 1 if p['kind']=='agency' else 2 if p.get('business','').startswith('venue_') else 3)):
 n=nodes[p['node']];xmin,xmax,oy=areas[p['street']]
 candidates=[(n['x'],n['y'],1)]
 for yy in [oy,oy+30,oy+60]:
  for side in [1,-1]:
   for xx in range(int(xmin+p['width']/2+1),int(xmax-p['width']/2),3):candidates.append((xx,yy,side))
 found=None
 for x,y,side in candidates:
  rect=footprint(p,x,y,side)
  if any(intersects(rect,other) for other in occupied):continue
  if any(rect[0]<v+5 and rect[2]>v-5 for v in [-200,-110,0,120,260]):continue
  found=(x,y,side,rect);break
 if found is None:raise ValueError(('No free frontage',p['id']))
 x,y,side,rect=found;n.update(x=x,y=y);p['entranceY']=side*6;p['heading']=0 if side==1 else 180
 occupied.append(rect)
# Preserve every old route/node identifier, relocating redundant legacy transit nodes to road intersections.
for i,n in enumerate(old_nodes):
    if n['id'] not in nodes:node(n['id'],-200+(i%5)*100,(i//5%13)*30)
# Split each road at EVERY entrance/intersection. No diagonal links, disconnected alias points or crossing without junction.
# Rebuild the coordinate index after frontage allocation moved door nodes.
loc={}
for n in nodes.values():
 while (n['x'],n['y']) in loc:n['x']=round(n['x']+.01,3)
 loc[(n['x'],n['y'])]=n['id']
verticals=[-200,-110,0,120,260]
horizontals=list(range(0,361,30))
for x in verticals:
    for y in horizontals:
        if (x,y) not in loc:node('grid_'+str(x)+'_'+str(y),x,y)
for n in list(nodes.values()):
    if n['y'] not in horizontals:raise ValueError(('not on road',n))
edges=[];seen=set()
def edge(a,b):
    if a==b:return
    key=tuple(sorted((a,b)))
    if key in seen:return
    seen.add(key);edges.append(dict(a=a,b=b))
for y in horizontals:
    row=sorted((n for n in nodes.values() if n['y']==y),key=lambda n:n['x'])
    for a,b in zip(row,row[1:]):edge(a['id'],b['id'])
for x in verticals:
    col=sorted((n for n in nodes.values() if n['x']==x),key=lambda n:n['y'])
    for a,b in zip(col,col[1:]):edge(a['id'],b['id'])
# Decorative compound rows leave lane-frontage clear and skip all actual interactive footprints.
blocks=[]
for street,tier,x0,x1,y in [('s_oldhomes',0,-195,-5,0),('s_services',1,-195,-5,90),('s_sports',2,-195,-5,180),
 ('s_newtown',3,-195,-115,270),('s_newtown',3,15,115,270)]:
    for base in [y,y+30,y+60]:
        for x in range(x0+10,x1-7,22):
            if any(abs(nodes[p['node']]['x']-x)<(p['width']/2+10) and abs(nodes[p['node']]['y']-base)<20 for p in places):continue
            blocks.append(dict(street=street,model=['oldhome','apartment','luxury_apartment','villa'][tier],tier=tier,x=x,y=base+13,width=16,depth=12,columns=1,rows=1))
for n in nodes.values():n['y']*=1.5
for b in blocks:b['y']*=1.5
data.update(layoutVersion=7,uniformRoadScale=1,distanceScale=1,
 streets=[dict(id=a,name=b,purpose=c) for a,b,c in streets],nodes=list(nodes.values()),edges=edges,places=places,blocks=blocks,
 patrols=[dict(id='v7_patrol',nodes=['grid_0_0','grid_0_90','grid_120_90','grid_120_0'])])
for p in data['places']:
 if p.get('model')=='shop':p['width']={'small':8,'medium':11,'large':14}.get(p.get('size'),11)
import runpy
data=runpy.run_path(str(Path(__file__).with_name('densify-city.py')))['populate'](data)
path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('V7:',len(nodes),'nodes,',len(edges),'edges,',len(places),'places,',len(blocks),'compound buildings')
