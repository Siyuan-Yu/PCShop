"""Populate V7 residential blocks only, preserving all gameplay places and routes."""
import json
from pathlib import Path

def overlap(a,b):
    return a[0]<b[2] and a[2]>b[0] and a[1]<b[3] and a[3]>b[1]

def reserved_rectangles(data):
    nodes={n['id']:n for n in data['nodes']}
    # Central gardens visible in the approved four-agency reference. Keep their
    # footprints synchronized with StreetWorldBuilder.ReferenceLandscape.cs.
    reserved=[(-108,191,-82,214),(-102,277,-57,315)]
    for p in data['places']:
        n=nodes[p['node']];x=n['x']+p.get('entranceX',0);front=n['y']+p.get('entranceY',0)
        back=front+(1 if p.get('entranceY',0)>=0 else -1)*p.get('depth',9)
        # Include the actual facade, roof overhang and access aisle, not a whole nearby block.
        reserved.append((x-p.get('width',10)/2-1.5,min(front,back)-1.5,x+p.get('width',10)/2+1.5,max(front,back)+1.5))
        reserved.append((x-2.5,min(front,n['y'])-1,x+2.5,max(front,n['y'])+1))
    for edge in data['edges']:
        a,b=nodes[edge['a']],nodes[edge['b']]
        # Road + pavement + a small setback; never create buildings in the circulation corridor.
        reserved.append((min(a['x'],b['x'])-6.1,min(a['y'],b['y'])-6.1,max(a['x'],b['x'])+6.1,max(a['y'],b['y'])+6.1))
    return reserved

def building_rect(b,margin=0):
    extra=1.6 if b['model']=='villa' else .6
    return (b['x']-b['width']/2-extra-margin,b['y']-.6-margin,
            b['x']+b['width']/2+extra+margin,b['y']+b['depth']+.6+margin)

def populate(data):
    reserved=reserved_rectangles(data);blocks=[]
    # Mixed frontage widths and heights form real urban grain, without consuming game RNG.
    districts=[('s_oldhomes',0,-193,-7,0),('s_services',1,-193,-7,135),
      ('s_sports',2,-193,-7,270),('s_newtown',3,-193,-117,405),('s_newtown',3,17,113,405)]
    models=['oldhome','apartment','luxury_apartment','villa']
    sizes=[[(6.5,9,5),(9,11,8),(12,12,11),(16,11,14),(19,12,10)],
      [(9,10,9),(13,12,12),(18,12,16),(24,11,19)],
      [(11,10,16),(16,12,20),(22,12,24),(28,11,27)],
      [(18,25,5.8),(22,27,6.2),(26,28,6.8),(30,26,6.4)]]
    for street,tier,x0,x1,y0 in districts:
        for lane in range(3):
            for row,front_offset in enumerate([7.5] if tier==3 else [7.5,24]):
                y=y0+45*lane+front_offset
                left=x0;index=0
                while left+6<x1:
                    first=(index*3+lane*2+row+tier)%len(sizes[tier])
                    options=sizes[tier][first:]+sizes[tier][:first]
                    found=None
                    for w,d,height in options:
                        extra=1.6 if tier==3 else .6
                        x=left+extra+.75+w/2
                        b=dict(street=street,model=models[tier],tier=tier,x=round(x,3),y=y,width=w,depth=d,
                               height=height,variant=index+lane*11+row*7,columns=1,rows=1)
                        rect=building_rect(b,.75)
                        if rect[2]>x1:continue
                        if any(overlap(rect,r) for r in reserved):continue
                        found=(b,rect);break
                    if found:
                        b,rect=found;blocks.append(b);reserved.append(rect);left=rect[2]+.25
                    else:left+=2
                    index+=1
    # The reference has continuous commercial frontages, not isolated functional
    # shops surrounded by empty grass. These are scenery only; stable shop IDs stay intact.
    commercial=[('s_start',0,17,113,0,'shop'),('s_market',1,17,113,135,'shop'),
      ('s_creative',2,17,113,270,'shop'),('s_night',3,-113,-7,405,'shop'),
      ('s_electronics',0,117,253,0,'warehouse'),('s_expo',2,117,253,270,'office')]
    for street,tier,x0,x1,y0,model in commercial:
        for lane in range(3):
            for row,offset in enumerate([8,25]):
                left=x0;index=0
                while left+8<x1:
                    w=[10,14,18][(index+lane+row)%3]
                    depth=11 if model!='warehouse' else 13
                    height=(5+tier*2+(index%3)*2) if model=='shop' else (6 if model=='warehouse' else 16+(index%3)*5)
                    x=left+w/2+1
                    b=dict(street=street,model=model,tier=tier,x=round(x,3),y=y0+lane*45+offset,
                        width=w,depth=depth,height=height,variant=100+index+lane*11+row*7,columns=1,rows=1)
                    rect=building_rect(b,1)
                    if rect[2]<=x1 and not any(overlap(rect,r) for r in reserved):
                        blocks.append(b);reserved.append(rect);left=rect[2]+1
                    else:left+=3
                    index+=1
    data['blocks']=blocks
    return data

if __name__=='__main__':
    from collections import Counter
    path=Path(__file__).resolve().parents[1]/'Assets/Resources/Data/city_layout.json'
    data=populate(json.loads(path.read_text(encoding='utf-8-sig')))
    path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Residential scenery:',dict(Counter(b['street'] for b in data['blocks'])),'total',len(data['blocks']))
