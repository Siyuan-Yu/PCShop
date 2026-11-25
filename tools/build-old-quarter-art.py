"""Generate the old quarter meshes and textures from the project art boards.

Requires Pillow and NumPy. From the project root, render into a staging directory with:
    python tools/build-old-quarter-art.py . work/old-quarter-preview
"""
import argparse
from pathlib import Path
from math import sin, cos, pi
import random
import numpy as np
from PIL import Image, ImageDraw
from quarter_surfaces import bake_surfaces
from quarter_fittings import END_WINDOWS, STREET_WINDOWS, window_fittings, shop_awning, produce_crate
args = argparse.ArgumentParser(description=__doc__)
args.add_argument('project', type=Path, help='Project containing docs/art-direction')
args.add_argument('output', type=Path, help='Destination for OBJ meshes and PNG textures')
options = args.parse_args()
repo = options.project
dest = options.output
dest.mkdir(parents=True, exist_ok=True)
source = repo / "docs/art-direction"
painted = source / 'concept/old-quarter'
sheet = Image.open(source / "concept/asset-sheet-buildings-props.jpg").convert("RGB")
ref = Image.open(source / "reference/ref3_scene.jpg").convert("RGB")
S = 192
atlas = Image.new("RGB", (S*8, S*8), "#766854")
slots = {}
rng = random.Random(457)
noise = np.random.default_rng(457)

def quad(img, tl,tr,br,bl, size=(S,S)):
    # PIL QUAD orders its source corners clockwise from upper left downwards.
    return img.transform(size, Image.Transform.QUAD, tuple(v for p in (tl,bl,br,tr) for v in p), Image.Resampling.BICUBIC)

def tile(name, image):
    index=len(slots)
    slots[name]=index
    atlas.paste(image.resize((S,S),Image.Resampling.LANCZOS),((index%8)*S,(index//8)*S))

def paint(rgb, variation=3):
    broad=np.asarray(Image.fromarray(noise.integers(70,185,(13,13),dtype=np.uint8)).resize((S,S),Image.Resampling.BICUBIC)).astype(float)
    d=(broad-128)*variation/85+noise.normal(0,variation*.55,(S,S))
    return Image.fromarray(np.uint8(np.clip(np.array(rgb)+d[:,:,None],0,255)))

wall=quad(sheet,(59,58),(198,69),(197,216),(59,200))
tile('plaster',wall)
tile('sageplaster',quad(sheet,(661,61),(799,73),(799,225),(661,213)))
# Straighten the reference facade before taking individual upper-storey bays.
front=quad(ref,(518,171),(739,6),(739,252),(518,417),(600,600))
sidewall=quad(ref,(292,83),(511,171),(511,406),(292,318),(600,600))
sidewall=Image.fromarray(np.uint8(np.clip(np.asarray(sidewall,dtype=float)*1.35+[8,7,4],0,255)))
for i in range(4):
    for j,(top,bottom) in enumerate([(16,220),(224,399),(408,580)]):
        tile(f'win{i}{j}',front.crop((i*150+3,top,(i+1)*150-4,bottom)))
for i in range(3):
    for j,(top,bottom) in enumerate([(16,219),(225,402),(409,589)]):
        tile(f'side{i}{j}',sidewall.crop((i*200+5,top,(i+1)*200-5,bottom)))
tile('panel',quad(sheet,(453,285),(579,300),(578,447),(453,431)))
tile('store',quad(sheet,(247,313),(381,329),(381,441),(247,423)))
tile('shutter',quad(sheet,(70,326),(167,336),(167,429),(70,418)))
tile('ac',quad(sheet,(52,535),(121,549),(120,596),(52,580)))
tile('awning', Image.blend(quad(sheet,(865,82),(1007,96),(989,132),(854,117)),paint((108,125,99)),.52))
tile('rustawning',quad(sheet,(72,292),(172,303),(163,324),(60,315)))
tile('crate',quad(sheet,(829,340),(866,351),(865,388),(829,375)))
for name,col in dict(ink=(54,45,35),sill=(162,142,111),rust=(136,94,63),metal=(97,93,79),pipe=(108,95,76),wood=(108,79,53),sand=(172,151,117),green=(91,108,62),terra=(150,100,67),water=(69,98,101),bark=(104,79,49),linen=(184,170,138),blue=(87,108,120)).items():
    tile(name,paint(col))
roof=paint((137,95,72),1.6)
# Small differences between fired tiles survive the normal camera distance.
# Leave stretches of the lower joints light where the drawn line wears away.
d=ImageDraw.Draw(roof,'RGBA')
pottery=random.Random(736)
for row in range(5):
    y=round(row*S/5); end=round((row+1)*S/5)
    for x in range(-48+(row%2)*24,S,48):
        shade=pottery.randrange(-5,6)
        d.rectangle((x+1,y+1,x+47,end-1),fill=(138+shade,95+shade,70+shade,185))
        d.line([(x,y),(x+17,y+pottery.choice([0,0,1])),(x+35,y),(x+48,y)],fill=(77,54,43,185),width=2)
        d.line([(x,y+1),(x,end-2)],fill=(88,66,50,160),width=1)
        if pottery.random()<.55:
            d.line((x+3,y+2,x+pottery.randrange(11,34),y+2),fill=(183,132,93,93),width=1)
        for k in range(3):
            px=x+pottery.randrange(3,37); py=pottery.randrange(y+3,end-4)
            d.line((px,py,px+pottery.randrange(4,12),py-1),fill=(180,125,87,24),width=2)
tile('roof',roof)
for k,col in enumerate([(66,82,46),(83,99,49),(104,119,53),(126,135,63),(148,150,73)]):
    leaf=paint(col,4); d=ImageDraw.Draw(leaf)
    for _ in range(45):
        x,y=rng.randrange(S),rng.randrange(S); n=rng.randint(3,12); v=rng.choice([-8,6,9])
        d.polygon([(x-n,y),(x-n*.6,y-n*.6),(x+n*.2,y-n),(x+n,y-.3*n),(x+n*.6,y+n*.3),(x,y+n*.6)],fill=tuple(max(0,c+v) for c in col))
    tile('leaf'+str(k),leaf)
def limewash(color, variant):
    # Broad stains and sparse rain tracks stay on the plaster around the opening.
    prng = np.random.default_rng(820 + variant)
    broad = Image.fromarray(prng.integers(80, 175, (7, 11), dtype=np.uint8))
    broad = np.asarray(broad.resize((S, S), Image.Resampling.BICUBIC), dtype=float)
    delta = (broad - 128) * .12 + prng.normal(0, 1.3, (S, S))
    out = Image.fromarray(np.clip(np.array(color) + delta[:, :, None], 0, 255).astype('uint8'))
    d = ImageDraw.Draw(out, 'RGBA')
    if variant < 6:
        # Gravity gives the dirt a direction. Leave the center of the wall quiet.
        for k in range(11):
            x = int(prng.choice([39, 48, 137, 149])) + int(prng.integers(-5, 7))
            y = int(prng.integers(136, 148))
            h = int(prng.integers(10, 36))
            d.polygon([(x-2,y),(x+3,y),(x+2,y+h//2),(x,y+h)],fill=(76,65,51,28))
        for x in [int(prng.integers(16,28)),int(prng.integers(163,179))]:
            points = [(x,0),(x-1,15),(x+2,31),(x+1,49),(x+4,56)]
            d.line(points,fill=(83,73,60,67),width=2)
            d.line([(x+2,31),(x-3,36),(x-5,44)],fill=(83,73,60,42),width=1)
        d.line([(0,188),(44,188),(81,187),(128,189),(191,188)],fill=(64,54,47,64),width=2)
    for x in (45, 53, 134, 142):
        y = int(prng.integers(116, 149))
        d.line([(x, y), (x + 1, y + 9), (x, y + int(prng.integers(15, 32)))], fill=(61, 52, 45, 26), width=1)
    if variant % 2 == 0:
        d.line([(157, 12), (156, 27), (159, 36), (158, 48)], fill=(89, 75, 58, 35))
    return out

for index in range(3):
    tile('lightwall' + str(index), limewash((192, 156, 120), index))
    if index==0:
        felt=limewash((89,94,95),7)
        seam=ImageDraw.Draw(felt)
        for y in (7,70,135):
            seam.line((0,y,S,y+1),fill=(64,69,69),width=2)
            seam.line((0,y+3,S,y+4),fill=(102,105,102))
        tile('feltroof',felt)
    elif index==1:
        tile('yardplaster',limewash((133,124,109),7))
    else:
        tile('tinhood',limewash((106,100,91),7))
for name, color in [('concrete', (133, 115, 94)), ('jamb', (114, 100, 83)), ('frame', (85,77,65)),
                    ('turf', (124, 125, 72)), ('drysoil', (115, 107, 71)),
                    ('humus', (93, 87, 58)), ('straw', (157, 143, 96))]:
    tile(name, limewash(color, 7))
tile('oxidecloth',limewash((148,84,65),7))
tile('oldcanvas',Image.blend(quad(sheet,(72,292),(172,303),(163,324),(60,315)),limewash((110,113,99),7),.73))
assert len(slots) <= 64

def ground_map(name,color,kind):
    yy,xx=np.mgrid[0:512,0:512]*(2*pi/512)
    variation=1.2*np.sin(2*xx+yy)+1.1*np.sin(xx-3*yy)+.75*np.sin(xx*7+yy*4)+noise.normal(0,1.4,(512,512))
    im=Image.fromarray(np.uint8(np.clip(np.array(color)+variation[:,:,None],0,255)))
    d=ImageDraw.Draw(im)
    if kind=='grass':
        for _ in range(18):
            px,py=rng.randrange(512),rng.randrange(512)
            for j in range(rng.randrange(6,19)):
                x,y=px+rng.randrange(-8,9),py+rng.randrange(-5,6)
                v=rng.choice([-18,-12,11,16]); col=tuple(c+v for c in color)
                d.line((x,y,x+rng.randrange(-4,6),y-rng.randrange(2,7)),fill=col,width=1)
        for _ in range(1200):
            x,y=rng.randrange(512),rng.randrange(512); v=rng.choice([-8,-4,5,8])
            d.line((x,y,x+rng.randrange(1,5),y-rng.randrange(1,4)),fill=tuple(c+v for c in color))
    if kind=='road':
        for _ in range(3):
            x,y=rng.randrange(30,460),rng.randrange(30,460)
            pts=[(x+cos(a*.62)*(4+rng.random()*12),y+sin(a*.62)*(3+rng.random()*5)) for a in range(10)]
            d.polygon(pts,fill=(105,100,88))
        for _ in range(2800):
            x,y=rng.randrange(512),rng.randrange(512);v=rng.randrange(-7,9)
            d.point((x,y),fill=tuple(c+v for c in color))
        for _ in range(3):
            x,y=rng.randrange(100,410),rng.randrange(100,410)
            pts=[(x,y)]
            for j in range(7): x+=rng.randrange(-4,9); y+=rng.randrange(2,7);pts.append((x,y))
            d.line(pts,fill=(61,57,51))
    if kind=='paving':
        for i in range(0,513,64):
            d.line((i,0,i,512),fill=(138,121,102),width=2);d.line((0,i,512,i),fill=(138,119,98),width=2)
            d.line((i+2,0,i+2,512),fill=(183,161,133))
    im.save(dest/(name+'.png'))
ground_map('Grass',(142,143,80),'grass')
ground_map('Road',(95,88,79),'road')
ground_map('Pavement',(178,153,126),'paving')

class Model:
    def __init__(self):
        self.faces=[]
    def face(self,pts,material,expected=None,uv_rect=None):
        pts=np.array(pts,dtype=float)
        uvs=[(0,0),(1,0),(1,1),(0,1)] if len(pts)==4 else [(0,0),(1,0),(.5,1)]
        if uv_rect is not None:
            l,b,r,t = uv_rect
            uvs = [(l+u*(r-l), b+v*(t-b)) for u,v in uvs]
        if expected is not None and np.dot(np.cross(pts[1]-pts[0],pts[2]-pts[0]),expected)<0:
            pts=pts[::-1];uvs=uvs[::-1]
        self.faces.append((pts,material,uvs))
    def box(self,x,y,z,w,h,d,mat):
        a,b=x-w/2,x+w/2; c,e=y-h/2,y+h/2; f,g=z-d/2,z+d/2
        self.face([(a,c,f),(b,c,f),(b,e,f),(a,e,f)],mat,(0,0,-1))
        self.face([(b,c,g),(a,c,g),(a,e,g),(b,e,g)],mat,(0,0,1))
        self.face([(a,c,g),(a,c,f),(a,e,f),(a,e,g)],mat,(-1,0,0))
        self.face([(b,c,f),(b,c,g),(b,e,g),(b,e,f)],mat,(1,0,0))
        self.face([(a,e,f),(b,e,f),(b,e,g),(a,e,g)],mat,(0,1,0))
        self.face([(a,c,g),(b,c,g),(b,c,f),(a,c,f)],mat,(0,-1,0))
    def tube(self,p,q,r1,r2,mat,n=9):
        p,q=np.array(p),np.array(q);axis=(q-p)/np.linalg.norm(q-p)
        u=np.cross(axis,[1,0,0] if abs(axis[0])<.8 else [0,0,1]);u=u/np.linalg.norm(u);v=np.cross(axis,u)
        rings=[[c+r*(u*cos(2*pi*i/n)+v*sin(2*pi*i/n)) for i in range(n)] for c,r in [(p,r1),(q,r2)]]
        for i in range(n):
            j=(i+1)%n; a,b=rings
            self.face([a[i],a[j],b[j],b[i]],mat,a[i]-p)
            self.face([b[i],b[j],q],mat,axis)
    def save(self,name):
        # Store editable meshes as OBJ; Unity converts right-handed OBJ to left-handed world space.
        lines=['o '+name]; idx=1
        for pts,mat,uvs in self.faces:
            for x,y,z in pts:lines.append(f'v {-x:.5f} {y:.5f} {z:.5f}')
            if mat == 'elevation':
                # The second UV range selects the whole-wall painting in QuarterPaint.
                for u,v in uvs:lines.append(f'vt {2+u:.7f} {v:.7f}')
            else:
                n=slots[mat]; col,row=n%8,n//8
                for u,v in uvs:lines.append(f'vt {(col+.018+u*.964)/8:.7f} {1-(row+.982-v*.964)/8:.7f}')
            lines.append('f '+' '.join(f'{i}/{i}' for i in reversed(range(idx,idx+len(pts)))))
            idx+=len(pts)
        (dest/(name+'.obj')).write_text('\n'.join(lines)+'\n',encoding='ascii')

def building(shop=False, neighbor=False):
    m=Model()
    w, d = 9.65, (7.50 if not shop and not neighbor else 9.65)
    ground, pitch = 2.77, 2.43
    floors = 1 if shop else (2 if neighbor else 3)
    top = ground+floors*pitch
    m.box(0,ground/2,d/2,w,ground,d,'plaster')
    m.box(0,(ground+top)/2,d/2,w-.55,top-ground,d-.55,'plaster')
    for side in range(4):
        along=[(1,0),(0,1),(-1,0),(0,-1)][side];out=[(0,-1),(1,0),(0,1),(-1,0)][side]
        mid=[(0,0),(w/2,d/2),(0,d),(-w/2,d/2)][side]
        length = w if side%2==0 else d
        def xyz(u,y,t):return (mid[0]+along[0]*u+out[0]*t,y,mid[1]+along[1]*u+out[1]*t)
        def box(u,y,t,a,b,c,mat):
            x,y,z=xyz(u,y,t);m.box(x,y,z,a if side%2==0 else c,b,c if side%2==0 else a,mat)
        def panel(u,y,t,a,b,mat):
            m.face([xyz(u-a/2,y-b/2,t),xyz(u+a/2,y-b/2,t),xyz(u+a/2,y+b/2,t),xyz(u-a/2,y+b/2,t)],mat,(out[0],0,out[1]))
        for row in range(floors):
            count=4 if side%2==0 else 3
            for i in range(count):
                u=(i-(count-1)/2)*length/count;y=ground+pitch*(row+.5)
                bay = length/count
                span = bay * ((.61, .63, .62, .59)[i] if count == 4 else (.57, .60, .56)[i])
                h = (1.26, 1.32, 1.27)[row]
                cy = y + .10
                left, right = u-span/2, u+span/2
                bottom, head = cy-h/2, cy+h/2
                l, b, r, t = u-bay/2, y-pitch/2, u+bay/2, y+pitch/2
                def flat(x0,y0,x1,y1,z):
                    pts=[xyz(x0,y0,z),xyz(x1,y0,z),xyz(x1,y1,z),xyz(x0,y1,z)]
                    half = .5 if count==4 else 0
                    def su(x):
                        s = .5+x/length
                        return half+.5*(.003+.994*(1-s if shop else s))
                    def sv(y):
                        return .003+.994*((y-ground)/pitch+(1 if shop or neighbor else 0))/3
                    m.face(pts,'elevation',(out[0],0,out[1]),(su(x0),sv(y0),su(x1),sv(y1)))
                for x0,y0,x1,y1 in [(l,b,r,bottom),(l,head,r,t),(l,bottom,left,head),(right,bottom,r,head)]:
                    flat(x0,y0,x1,y1,0)
                depth = -.15
                flat(left,bottom,right,head,depth)
                for inner, normal in [(left,1),(right,-1)]:
                    m.face([xyz(inner,bottom,0),xyz(inner,head,0),xyz(inner,head,depth),xyz(inner,bottom,depth)],
                           'jamb',(normal*along[0],0,normal*along[1]))
                for level, normal in [(bottom,1),(head,-1)]:
                    m.face([xyz(left,level,0),xyz(right,level,0),xyz(right,level,depth),xyz(left,level,depth)],
                           'concrete',(0,normal,0))
                # An inner frame stays fine in screen space; the hood catches a separate shadow.
                for x in (left+.025, right-.025):
                    box(x,cy,depth+.036,.050,h,.062,'frame')
                for edge in (bottom+.024,head-.024):
                    box(u,edge,depth+.036,span,.048,.062,'frame')
                box(u,bottom-.040,.028,span+.09,.028,.13,'frame')
                box(u,bottom-.012,.030,span+.09,.037,.13,'concrete')
                windows = END_WINDOWS if count==3 else STREET_WINDOWS
                floor = (row+1)%3 if shop or neighbor else row
                column = count-i-1 if side in (1,2) or shop else i
                window_fittings(m,xyz,box,panel,windows[floor][column],u,bottom,head,span,side+3*row+i,
                                basket_unit=count==3 and floor==1 and column==1,
                                open_sash=count==4 and (floor,column) in [(2,0),(0,2)],
                                warm_cloth=count==4)
            box(0,ground+pitch*row,.012,length,.103,.046,'rust')
            box(0,ground+pitch*row-.056,.017,length,.018,.044,'frame')
        for i in range(3):
            u=(i-1)*length/3
            mat=(['shutter','panel','store'] if side==0 else ['panel','shutter','plaster'] if side==3 else ['plaster','panel','plaster'])[i]
            panel(u,ground/2,.027,length/3-.07,ground-.06,mat)
            covered = (side==0 and (not shop or i!=0)) or (side==3 and i==1 and not shop)
            if neighbor:
                covered = side==0 and i==2
            if covered:
                aw = 'oldcanvas' if side==3 or shop and i==1 else ('rustawning' if i==0 else 'awning')
                shop_awning(m,xyz,u,length/3-.11,aw,i+side)
                for x in (u-length/6+.09,u+length/6-.09):
                    box(x,1.05,.062,.096,2.1,.12,'wood')
                box(u,.045,.125,length/3-.21,.09,.23,'concrete')
                if side==0 and i==2 and not shop:
                    # Narrow sales shelf beside the entry, below the green canvas.
                    a=u+length/6-.55
                    for dx in (-.38,.38):
                        box(a+dx,.65,.31,.057,1.30,.36,'wood')
                    for shelf in (.19,.71,1.20):
                        box(a,shelf,.32,.91,.06,.44,'wood')
                        for j in range(4):
                            x0,y0,z0=xyz(a-.29+.18*j,shelf+.035,.35)
                            m.tube((x0,y0,z0),(x0,y0+.19+.05*(j%2),z0),.068,.061,
                                   ['green','terra','linen','wood'][j],6)
        box(-length/2+.11,top*.47,.095,.075,top*.94,.10,'pipe')
        for k in [0,1,2]:box(-length/2+.11,1.5+k*2.7,.114,.11,.06,.12,'ink')
        box(0,top+.06,0,length+.22,.18,.24,'concrete')
        box(0,top-.055,.044,length+.14,.075,.18,'ink')
        # Pencil-width outlines are attached to the masonry and end at each corner.
        box(0,top+.147,.108,length+.20,.025,.027,'frame')
        box(-length/2+.018,(top+ground)/2,.012,.028,top-ground,.029,'ink')
    for i in range(3):
        for j in range(3):
            m.box((i-1)*w/3,top+.024,(j+.5)*d/3,w/3,.048,d/3,'feltroof' if shop else 'roof')
    tanks = [] if neighbor else ([(2.4,6.4)] if shop else [(-1.95,4.9),(.80,5.65)])
    if neighbor:
        for x,z in [(-2.3,3.1),(1.3,6.2)]:
            m.box(x,top+.30,z,1.65,.48,1.21,'jamb')
            m.box(x,top+.56,z,1.72,.066,1.28,'metal')
    for x,z in tanks:
        m.tube((x,top+.048,z),(x,top+.14,z),.65,.63,'frame',18)
        m.tube((x,top+.14,z),(x,top+1.46,z),.58,.58,'metal',18)
        for dy in [.18,.83,1.43]:
            m.tube((x,top+dy,z),(x,top+dy+.040,z),.597,.597,'frame',18)
            m.tube((x,top+dy+.040,z),(x,top+dy+.062,z),.592,.584,'pipe',18)
        m.tube((x,top+1.47,z),(x,top+1.59,z),.58,.31,'metal',18)
        m.tube((x+.07,top+1.59,z+.03),(x+.07,top+1.64,z+.03),.19,.17,'frame',12)
        m.box(x+.07,top+1.68,z+.03,.13,.042,.036,'pipe')
        m.box(x,top+.79,z-.582,.021,1.23,.016,'frame')
    for x,z,c in [(-4.8,-.6,0),(-4.9,-1.2,1),(4,-.9,2),(3.9,-1.38,3)]:
        produce_crate(m,x,0,z,filled=c!=0)
        if c==0:produce_crate(m,x+.04,.46,z,.51,.42,.35,True)
    for x in [-4.25,2.0,4.55]:
        z=-1.05;r=.24;m.tube((x,.04,z),(x,.46,z),r*.65,r,'terra')
        for i in range(8):
            a=i*2.4;y=.52+rng.random()*.43
            m.tube((x,.36,z),(x+cos(a)*.23,y,z+sin(a)*.23),.075,.045,'leaf'+str(i%4),5)
    # Tufts break the wall-ground joint. Keep all street doors and walking space clear.
    verge = random.Random(407 if shop else 981)
    for x,z in [(-w/2-.14,.48),(-w/2-.18,3.15),(-w/2-.12,6.35),
                (-w/2-.23,d-1.13),(w/2+.13,2.23),(-4.48,-.28),(3.61,-.21)]:
        for stem in range(verge.randint(13,22)):
            a=verge.random()*2*pi
            radius=verge.uniform(.02,.26)
            bx,bz=x+cos(a)*radius,z+sin(a)*radius
            height=verge.uniform(.10,.37); spread=verge.uniform(.04,.13)
            tip=(bx+spread*cos(a),height,bz+spread*sin(a))
            side_x,side_z=.035*sin(a),-.035*cos(a)
            blade=[(bx+side_x,.015,bz+side_z),(bx-side_x,.015,bz-side_z),tip]
            leaf='leaf'+str(verge.choice([0,1,1,2,3]))
            m.face(blade,leaf);m.face(blade[::-1],leaf)
    if not shop:
        # A repair bench's spare boxes and a planter sit against the blind end of the wall.
        for x,z,base in [(-5.14,d-2.13,0),(-5.17,d-1.42,0),(-5.11,d-1.45,.53)]:
            produce_crate(m,x,base,z,.56,.63,.53,base>.1)
        produce_crate(m,-5.14,1.06,d-1.45,.51,.57,.39,True)
        x,z=-5.19,2.60
        m.tube((x,.01,z),(x,.41,z),.20,.30,'terra',10)
        m.tube((x,.38,z),(x,.44,z),.31,.31,'wood',10)
        for k in range(18):
            a=2*pi*k/7.3
            base=(x+.04*cos(a),.39,z+.04*sin(a))
            tip=(x+verge.uniform(.23,.39)*cos(a),verge.uniform(.57,.89),z+.32*sin(a))
            wing=(x-.085*sin(a),.55,z+.085*cos(a))
            blade=[base,wing,tip]
            leaf='leaf'+str(1+k%3)
            m.face(blade,leaf);m.face(blade[::-1],leaf)
    return m

def tree():
    m=Model()
    # Taper the base into the buttresses and continue the same bark strip up each fork.
    def branch(sections, sides):
        rings=[]
        for i,(x,y,z,r) in enumerate(sections):
            ring=[]
            for j in range(sides):
                angle=2*pi*j/sides
                radial=r*(1+.08*cos(3*angle+.46*i))
                ring.append((x+radial*cos(angle), y, z+radial*sin(angle)))
            rings.append(ring)
        for i,(lo,hi) in enumerate(zip(rings,rings[1:])):
            for j in range(sides):
                k=(j+1)%sides
                middle=2*pi*(j+.5)/sides
                m.face([lo[j],lo[k],hi[k],hi[j]],'bark',(cos(middle),0,sin(middle)),
                       (j/sides,i/(len(rings)-1),(j+1)/sides,(i+1)/(len(rings)-1)))
    branch([(0,.01,0,.38),(-.06,.29,.03,.26),(-.09,.79,.05,.20),
            (.01,1.19,.05,.18),(.10,1.61,.03,.16),(.03,2.03,0,.135),
            (-.25,2.42,-.17,.086),(-.52,2.76,-.25,.042)],11)
    branch([(.05,1.40,.035,.146),(.28,1.72,.12,.133),(.49,2.04,.26,.102),
            (.55,2.43,.39,.071),(.77,2.81,.51,.031)],9)
    branch([(-.03,1.18,.05,.12),(-.30,1.50,.15,.112),(-.51,1.86,.28,.095),
            (-.56,2.22,.39,.06),(-.82,2.52,.43,.034)],8)
    branch([(.07,1.67,0,.109),(.24,2.05,-.28,.088),(.17,2.36,-.53,.068),(.04,2.85,-.77,.035)],8)
    for j in range(22):
        a=rng.random()*pi*2;r=rng.random()*.70
        x,z=cos(a)*r,sin(a)*r
        h=.09+rng.random()*.15
        m.face([(x-.033,.03,z),(x+.031,.03,z),(x+.12,h,z+.055)],'leaf'+str(j%4),(0,1,0))
    brush = random.Random(791)
    def patch(x,z,height,rx,rz,material,n):
        ring=[]
        for j in range(n):
            a = 2*pi*j/n
            irregular = brush.uniform(.72,1.12)
            ring.append((x+cos(a)*rx*irregular,height,z+sin(a)*rz*irregular))
        for j in range(n):
            m.face([(x,height,z),ring[j],ring[(j+1)%n]],material,(0,1,0))
    # Allow the existing lawn to show through the broken outer edge.
    for j in range(14):
        a = 2*pi*j/14
        r = brush.uniform(.53,.86)
        patch(.09+r*cos(a),-.04+r*sin(a),.012+j*.0004,
              brush.uniform(.18,.33),brush.uniform(.17,.37),'turf',8)
    patch(.08,-.02,.020,.96,.74,'turf',17)
    patch(.06,-.06,.025,.71,.52,'drysoil',15)
    patch(.02,-.02,.030,.45,.36,'humus',13)
    for j in range(5):
        a=2*pi*j/5+.38
        m.tube((.02,.30,0),(.49*cos(a),.045,.49*sin(a)),.114,.032,'bark',6)
    for cx,cz in [(-.68,-.32),(.38,-.61),(.77,.19),(-.22,.48),(-.81,.17)]:
        for j in range(7):
            x,z=cx+brush.uniform(-.13,.13),cz+brush.uniform(-.12,.12)
            a=brush.random()*2*pi
            s=brush.uniform(.075,.12); tall=brush.uniform(.13,.35)
            wing = (cos(a)*s,sin(a)*s)
            left=(x-wing[0],.037,z-wing[1]);right=(x+wing[0],.037,z+wing[1])
            tip=(x+sin(a)*.14,tall,z-cos(a)*.14)
            mid=(x,.11,z)
            for pts,mat in [([left,mid,tip],'leaf1'),([mid,right,tip],'leaf2')]:
                m.face(pts,mat)
                m.face(pts[::-1],mat)
    # Curving paired leaf faces catch different amounts of sun. The soil is visible between plants.
    for cx,cz,rise,spread in [(-.57,-.31,.40,.40),(-.34,-.53,.28,.26),(.52,.28,.34,.33)]:
        for shoot in range(8):
            a=2*pi*shoot/8+brush.uniform(-.22,.22)
            dx,dz=cos(a),sin(a)
            h=rise*brush.uniform(.65,1.18); reach=spread*brush.uniform(.66,1.14)
            x,z=cx+dx*.07,cz+dz*.07
            w=brush.uniform(.034,.062)
            base=(x,.045,z); tip=(x+dx*reach,h*.73,z+dz*reach)
            ridge=(x+dx*reach*.54,h,z+dz*reach*.54)
            left=(ridge[0]-dz*w,h*.78,ridge[2]+dx*w)
            right=(ridge[0]+dz*w,h*.83,ridge[2]-dx*w)
            for face,mat in [([base,left,ridge,tip],'leaf1'),([base,ridge,right],'leaf3'),
                             ([ridge,tip,right],'leaf2')]:
                m.face(face,mat,(0,1,0))
                m.face(face[::-1],mat,(0,-1,0))
    for j in range(29):
        a = brush.random()*2*pi
        r = brush.uniform(.35,1.19)
        patch(cos(a)*r,sin(a)*r*.78,.037+j*.0001,brush.uniform(.023,.060),
              brush.uniform(.019,.044),'straw' if j%3 else 'drysoil',5)
    return m

def bark_strip():
    # Continuous circumferential UVs carry these tapering, interrupted strokes through each fork.
    yy,xx=np.indices((S,S),dtype=float)
    u,v=xx/S,yy/S
    drift=u+.019*np.sin(6.2*v+7*u)+.012*np.sin(13*v+23*u)
    broad=.55*np.cos(2*pi*drift*4)+.34*np.sin(2*pi*drift*7+2*v)
    groove=np.maximum(0,np.cos(2*pi*drift*9+2*v))**7
    value=(16*broad-15*groove)*(1-.20*v)
    rgb=np.array([119,85,52])+value[:,:,None]*np.array([1,.78,.53])
    return Image.fromarray(np.uint8(np.clip(rgb,0,255)))

bark_slot=slots['bark']
atlas.paste(bark_strip(),((bark_slot%8)*S,(bark_slot//8)*S))
building().save('Walkup');building(True).save('Shopfront');tree().save('CourtyardTree')
# The old quarter also has occupied three-storey plots. Give this one its own
# roofline and shutter pattern instead of using the corner workshop for every plot.
building(neighbor=True).save('YardWalkup')


# Twin posts, porcelain insulators and loose service drops in the public verge.
def wires():
    m=Model()
    for x,h in [(-8.4,5.85),(9.5,6.15),(17.9,6.37)]:
        z=-1.55
        m.tube((x,0,z),(x,h,z),.105,.078,'pipe',12)
        m.tube((x,.12,z),(x,.64,z),.118,.114,'metal',12)
        m.box(x,h-.32,z,.12,.115,1.06,'wood')
        for o in [-.43,.43]:
            m.tube((x,h-.29,z+o),(x,h-.04,z+o),.039,.048,'sill',10)
        m.box(x+.14,3.44,z,.22,.45,.28,'metal')
    def wire(a,b,dip,radius):
        prev=np.array(a)
        for k in range(1,13):
            t=k/12;p=np.array(a)*(1-t)+np.array(b)*t;p[1]-=4*dip*t*(1-t)
            m.tube(prev,p,radius,radius,'ink',5);prev=p
    for o, radius in [(-.43,.022),(.43,.016)]:
        wire((-8.4,5.81,-1.55+o),(9.5,6.11,-1.55+o),.84,radius)
        wire((9.5,6.11,-1.55+o),(17.9,6.33,-1.55+o),.56,radius)
    wire((9.5,5.87,-1.55),(4.8,5.63,.8),.39,.018)
    return m
wires().save('ServiceLines')

def station():
    m=Model()
    m.box(0,2.70,3.8,7.7,.32,7.5,'sageplaster')
    m.box(0,2.91,3.8,7.37,.12,7.18,'sand')
    m.box(0,1.30,7.4,7.3,2.6,.25,'plaster')
    for x in [-3.4,3.4]:
        m.box(x,1.32,.48,.35,2.6,.35,'sill')
        m.box(x,.19,.48,.55,.38,.55,'metal')
    m.box(0,2.59,-.05,4.1,.50,.13,'awning')
    m.box(-2.3,3.20,4.1,1.1,.47,1.65,'sageplaster')
    m.box(-2.3,3.45,4.1,1.20,.09,1.75,'metal')
    return m
station().save('Station')

def annex():
    m=Model();width,depth,h=9.65,9.65,3.7
    m.box(0,h/2,depth/2,width,h,depth,'yardplaster')
    m.box(0,h+.09,depth/2,width+.24,.20,depth+.24,'sill')
    m.box(0,h+.21,depth/2,width-.28,.10,depth-.28,'sand')
    m.box(0,h-.11,depth/2,width+.17,.15,depth+.17,'green')
    m.box(0,h-.22,depth/2,width+.10,.057,depth+.10,'ink')
    for i,mat in enumerate(['shutter','panel','store']):
        a=(i-1)*3.15
        m.face([(a-1.53,0,-.012),(a+1.53,0,-.012),(a+1.53,2.9,-.012),(a-1.53,2.9,-.012)],mat,(0,0,-1))
    for z in [2.4,6.8]:
        a=-(width/2+.013)
        m.face([(a,1,z-1.0),(a,1,z+1.0),(a,2.8,z+1.0),(a,2.8,z-1.0)],'side20',(-1,0,0))
    service_start = len(m.faces)
    # Lay out a workshop wall in front elevation, then turn it onto the long side.
    for x,z,wid,ht,mat in [(-3.08,.05,2.35,2.61,'shutter'),(-.53,.05,1.48,2.49,'panel'),
                          (1.33,.99,1.35,1.30,'side20'),(3.20,.99,1.32,1.30,'side10')]:
        m.box(x,z+ht/2,depth+.025,wid+.13,ht+.14,.075,'frame')
        pts=[(x+wid/2,z,depth+.073),(x-wid/2,z,depth+.073),
             (x-wid/2,z+ht,depth+.073),(x+wid/2,z+ht,depth+.073)]
        m.face(pts,mat,(0,0,1))
        m.box(x,z+ht+.085,depth+.084,wid+.22,.09,.20,'concrete')
        if z>.5:
            m.box(x,z-.051,depth+.07,wid+.16,.060,.18,'concrete')
    for x in (-4.56,4.53):
        m.tube((x,.12,depth+.10),(x,h-.21,depth+.10),.057,.057,'pipe',8)
    for x,z in [(1.7,depth+.39),(2.35,depth+.43),(2.29,depth+.95)]:
        produce_crate(m,x,0,z,filled=x>2)
    m.tube((-.03,.035,depth+.48),(-.03,.64,depth+.48),.24,.30,'water',10)
    m.tube((-.03,.64,depth+.48),(-.03,.68,depth+.48),.31,.31,'frame',10)
    for j in range(service_start,len(m.faces)):
        points,material,uv=m.faces[j]
        turned=np.column_stack((width/2+points[:,2]-depth,points[:,1],depth/2-points[:,0]))
        m.faces[j]=(turned,material,uv)
    for x in (-2.9,2.25):
        m.box(x,1.75,depth+.035,1.49,1.28,.08,'frame')
        pts=[(x+.68,1.16,depth+.083),(x-.68,1.16,depth+.083),
             (x-.68,2.33,depth+.083),(x+.68,2.33,depth+.083)]
        m.face(pts,'side20',(0,0,1))
    for x,z,a,b,ht in [(1.5,4.4,1.7,2.8,.77),(-1.6,6.3,1.2,1.8,.59)]:
        m.box(x,h+.30,z,a+.43,.12,b+.45,'green')
        m.box(x,h+.31+ht/2,z,a,ht,b,'frame')
        m.box(x,h+.34+ht,z,a+.10,.08,b+.10,'metal')
        for level in (.25,.43,.61,.79):
            m.box(x,h+.31+ht*level,z+b/2+.01,a*.74,.023,.018,'ink')
    m.tube((1.45,h+.31,2.50),(1.45,h+.94,2.50),.12,.11,'pipe',9)
    m.tube((1.45,h+.94,2.50),(1.45,h+1.04,2.50),.18,.18,'frame',9)
    return m
annex().save('LaneHouse')
glow=Image.new('L',atlas.size,0);dd=ImageDraw.Draw(glow)
for key in ['win10','win21','side12','side20']:
    ix=slots[key];x=ix%8*S; y=ix//8*S
    for a,b in [(53,77),(85,106),(114,131)]:
        for top,bt in [(62,82),(89,106)]:
            dd.rectangle((x+a,y+top,x+b,y+bt),fill=125 if top==62 else 81)
glow.save(dest/'WindowLight.png')
for index,name in enumerate(('CrownA','CrownB')):
    uv=[(.5*index,0),(.5*(index+1),0),(.5*(index+1),1),(.5*index,1)]
    lines=['o '+name,'v 0.5 -0.5 0','v -0.5 -0.5 0','v -0.5 0.5 0','v 0.5 0.5 0']
    lines += [f'vt {u} {v}' for u,v in uv] + ['f 1/1 2/2 3/3 4/4']
    (dest/(name+'.obj')).write_text('\n'.join(lines)+'\n',encoding='ascii')
bake_surfaces(painted,dest,atlas,slots,ref)
