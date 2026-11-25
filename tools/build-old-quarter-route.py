"""Build the street's UV sheets and meshes from the registered location paintings."""
import argparse
from math import cos, pi, sin
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

cli = argparse.ArgumentParser(description=__doc__)
cli.add_argument('project', type=Path)
cli.add_argument('output', type=Path)
args = cli.parse_args()
src = args.project / 'docs/art-direction/concept/old-quarter/route'
out = args.output
out.mkdir(parents=True, exist_ok=True)
paint = {k: Image.open(src/(k+'.jpg')).convert('RGB') for k in ('station','grocery','east-shop','homes','overview')}
atlas = Image.new('RGB',(2048,2048))
cells = {}

def tile(name, image, correction=(0,0,0)):
    i=len(cells)
    assert i<32
    cells[name]=i
    im=image.resize((496,240),Image.Resampling.LANCZOS)
    rgb=np.asarray(im,dtype=float)
    weight=np.clip((rgb.mean(axis=2)-32)/115,0,1)[:,:,None]
    rgb=np.uint8(np.clip(rgb+weight*np.array(correction),0,255))
    atlas.paste(Image.fromarray(np.pad(rgb,((8,8),(8,8),(0,0)),mode='edge')),(i%4*512,i//4*256))

def crop(site,quad):
    tl,tr,br,bl=quad
    return paint[site].transform((496,240),Image.Transform.QUAD,
        tuple(n for p in (tl,bl,br,tr) for n in p),Image.Resampling.BICUBIC)

def panel(key,site,quad,shift=(0,0,0)):
    tile(key,crop(site,quad),shift)

# Front and end elevations keep their brushwork at the full scale of the facade.
ivory=(8,16,27)
panel('blueEnd','east-shop',[(449,308),(691,400),(691,622),(449,531)],ivory)
panel('blueUpper','east-shop',[(695,401),(946,230),(946,323),(695,495)],ivory)
panel('redEnd','grocery',[(446,280),(688,377),(688,635),(446,540)],ivory)
panel('redUpper','grocery',[(707,376),(945,209),(945,325),(707,488)],ivory)
panel('plaster','east-shop',[(453,422),(577,470),(577,522),(453,474)],ivory)
panel('gable','homes',[(154,504),(476,462),(476,615),(154,655)],(8,16,24))
panel('roofBlue','east-shop',[(654,215),(754,253),(710,314),(610,276)],(2,7,12))
def pigment(image,base,broad=24,contrast=1):
    rgb=np.asarray(image,dtype=float)
    low=np.asarray(image.filter(ImageFilter.GaussianBlur(broad)),dtype=float)
    grain=(rgb-low)@np.array([.27,.57,.16])
    return Image.fromarray(np.uint8(np.clip(base+grain[:,:,None]*contrast,0,255)))

terracotta=crop('homes',[(1363,136),(1407,225),(1475,233),(1431,144)])
tile('roofRed',pigment(terracotta,np.array([167,100,71]),25,.89))
panel('shelf','east-shop',[(865,450),(914,417),(914,463),(865,496)],(4,7,10))
panel('goods','grocery',[(884,454),(933,421),(933,457),(884,490)],(4,8,12))
panel('door','east-shop',[(806,486),(846,459),(846,526),(806,553)],(2,6,10))
panel('darkWindow','east-shop',[(605,398),(645,414),(645,453),(605,437)],(2,5,9))
panel('litWindow','grocery',[(575,372),(637,396),(637,447),(575,422)],(0,3,6))
panel('wood','station',[(514,493),(586,472),(586,481),(514,502)],(0,4,7))
panel('stationRoof','station',[(607,241),(815,320),(713,394),(505,315)],(9,16,25))
panel('timeTable','station',[(462,453),(494,444),(494,486),(462,494)],(3,8,12))
panel('stone','station',[(686,508),(705,514),(705,589),(686,583)],(8,15,23))
stone=crop('station',[(607,241),(815,320),(713,394),(505,315)])
tile('ground',pigment(stone,np.array([192,178,155]),18,.64))
panel('houseGray','homes',[(147,488),(478,447),(478,620),(147,659)],(5,14,23))
# The small passerby in the concept belongs to the simulation. Restore the jamb,
# threshold and adjacent plaster from either side before rectifying the elevation.
homes=paint['homes']
homes.paste(homes.crop((862,539,871,577)),(871,537))
homes.paste(homes.crop((886,534,893,572)),(880,536))
panel('houseGreen','homes',[(725,443),(976,410),(976,548),(725,582)],(4,14,21))
panel('houseCream','homes',[(1250,344),(1525,308),(1525,460),(1250,498)],(7,16,25))
# Neutral side panels, joinery and shade colors come from the wide daylight key.
for name,col in {'ink':(56,57,55),'rim':(91,83,70),'cream':(214,204,180),'sage':(91,119,111),
        'canvasBlue':(77,108,116),'linen':(220,209,177),'canvasRed':(172,90,63),
        'iron':(73,79,78),'leaf':(121,145,72),'fruit':(183,170,75),'soil':(98,74,52)}.items():
    grain=np.asarray(paint['station'].crop((700,299,754,330)).resize((496,240)),float).mean(2)
    tile(name,Image.fromarray(np.uint8(np.clip(np.array(col)+(grain-grain.mean())[:,:,None]*.40,0,255))))
atlas.save(out/'RouteAtlas.png')

class Mesh:
    def __init__(self):
        self.faces=[]
    def face(self,corners,mat,normal,uv=None,glass=False):
        pts=np.asarray(corners,float)
        if uv is None: uv=[(0,0),(1,0),(1,1),(0,1)] if len(pts)==4 else [(0,0),(1,0),(.5,1)]
        if np.dot(np.cross(pts[1]-pts[0],pts[2]-pts[0]),normal)<0: pts,uv=pts[::-1],uv[::-1]
        self.faces.append((pts,mat,uv,glass))
    def box(self,x,y,z,w,h,d,mat):
        l,r=x-w/2,x+w/2;b,t=y-h/2,y+h/2;f,k=z-d/2,z+d/2
        for coords,n in [([(l,b,f),(r,b,f),(r,t,f),(l,t,f)],(0,0,-1)), ([(r,b,k),(l,b,k),(l,t,k),(r,t,k)],(0,0,1)),
                         ([(l,b,k),(l,b,f),(l,t,f),(l,t,k)],(-1,0,0)), ([(r,b,f),(r,b,k),(r,t,k),(r,t,f)],(1,0,0)),
                         ([(l,t,f),(r,t,f),(r,t,k),(l,t,k)],(0,1,0)), ([(l,b,k),(r,b,k),(r,b,f),(l,b,f)],(0,-1,0))]:
            self.face(coords,mat,n)
    def tube(self,a,b,r,mat,sides=8):
        a,b=np.array(a,float),np.array(b,float);delta=b-a; direction=delta/np.linalg.norm(delta)
        u=np.cross(direction,[1,0,0] if abs(direction[0])<.8 else [0,1,0]);u/=np.linalg.norm(u)
        v=np.cross(direction,u)
        ring=[r*(u*cos(i*2*pi/sides)+v*sin(i*2*pi/sides)) for i in range(sides)]
        for i,n in enumerate(ring):
            nxt=ring[(i+1)%sides];self.face([a+n,a+nxt,b+nxt,b+n],mat,n)
            self.face([b+n,b+nxt,b],mat,direction)
    def write(self,name):
        lines=['o '+name]; idx=1
        for pts,mat,uv,glass in self.faces:
            i=cells[mat];col,row=i%4,i//4
            lines.extend(f'v {-x:.5f} {y:.5f} {z:.5f}' for x,y,z in pts)
            lines.extend(f'vt {(col*512+8+x*496)/2048+int(glass):.7f} {1-(row*256+248-y*240)/2048:.7f}' for x,y in uv)
            lines.append('f '+' '.join(f'{j}/{j}' for j in reversed(range(idx,idx+len(pts)))))
            idx+=len(pts)
        (out/(name+'.obj')).write_text('\n'.join(lines)+'\n',encoding='ascii')

def wall(m,start,end,bottom,top,mat,windows=()):
    """Cut real recesses in a registered painting. Adjacent strips share continuous UVs."""
    start,end=np.array(start,float),np.array(end,float)
    along=end-start
    normal=np.array([along[2],0,-along[0]])/np.linalg.norm(along)
    def point(u,v,offset):
        return start+along*u+np.array([0,bottom+(top-bottom)*v,0])+normal*offset
    def patch(l,b,r,t,depth):
        uv=[(l,b),(r,b),(r,t),(l,t)]
        m.face([point(u,v,depth) for u,v in uv],mat,normal,uv,glass=depth<0)
    xx=sorted({0,1,*(u for l,b,r,t in windows for u in (l,r))})
    yy=sorted({0,1,*(v for l,b,r,t in windows for v in (b,t))})
    for l,r in zip(xx,xx[1:]):
        for b,t in zip(yy,yy[1:]):
            recessed=any(x1<(l+r)/2<x2 and y1<(b+t)/2<y2 for x1,y1,x2,y2 in windows)
            patch(l,b,r,t,-.10 if recessed else 0)
    for l,b,r,t in windows:
        edge=[(l,b),(r,b),(r,t),(l,t)]
        for i,(a,c) in enumerate(zip(edge,edge[1:]+edge[:1])):
            verts=[point(*a,0),point(*c,0),point(*c,-.10),point(*a,-.10)]
            # Both jamb faces have their own normals and can cast a shadow at a grazing angle.
            n=np.cross(verts[1]-verts[0],verts[2]-verts[0])
            m.face(verts,'rim',n);m.face(verts[::-1],'rim',-n)
        a=point(l-.012,b-.014,.055);c=point(r+.012,b-.014,.055)
        m.tube(a,c,.039,'cream',4)

def roof(m,w,d,h,rise,blue=False,hip=False):
    x=w/2+.25; z0=-.26;z1=d+.26
    ridge0,ridge1=(z0+(z1-z0)*.24,z1-(z1-z0)*.24) if hip else (z0,z1)
    peak=h+rise;mat='roofBlue' if blue else 'roofRed'
    # Individual tile courses maintain a constant period even on the hip triangles.
    for sign in (-1,1):
        def at(u,v,lip=0):
            return (sign*x*(1-v), h+rise*v+.016+lip, (z0+(ridge0-z0)*v)*(1-u)+(z1+(ridge1-z1)*v)*u)
        for j in range(8):
            for i in range(2):
                l,r=i/2,(i+1)/2;b,t=j/8,(j+1)/8
                v0,v1=(j%4)/4,(j%4+1)/4
                m.face([at(l,b,.022),at(r,b,.022),at(r,t),at(l,t)],mat,(sign,.8,0),[(0,v0),(1,v0),(1,v1),(0,v1)])
                m.face([at(l,b),at(r,b),at(r,b,.022),at(l,b,.022)],mat,(sign,0,0),[(0,v0),(1,v0),(1,v0+.012),(0,v0+.012)])
        m.box(sign*x,h-.052,(z0+z1)/2,.095,.13,z1-z0,'rim')
    for z,rz,face in [(z0,ridge0,-1),(z1,ridge1,1)]:
        if hip:
            for sign in (-1,1):
                for j in range(8):
                    b,t=j/8,(j+1)/8
                    v0,v1=(j%4)/4,(j%4+1)/4
                    def end_at(s,v):
                        return (s*sign*x*(1-v),h+rise*v+.015,z+(rz-z)*v)
                    points=[end_at(0,b),end_at(1,b),end_at(1,t)]
                    uvs=[(0,v0),(1-b,v0),(1-t,v1)]
                    if j<7:
                        points.append(end_at(0,t));uvs.append((0,v1))
                    m.face(points,mat,(0,1,face),uvs)
            for sign in (-1,1):m.tube((sign*x,h+.044,z),(0,peak+.044,rz),.072,mat,6)
        else:
            m.face([(-w/2,h,z+.028* -face),(w/2,h,z+.028* -face),(0,peak,z+.028* -face)],'plaster',(0,0,face))
            for sign in (-1,1):m.tube((sign*x,h,z),(0,peak,rz),.084,'rim',4)
    m.tube((0,peak+.05,ridge0),(0,peak+.05,ridge1),.103,mat,10)

def crate(m,x,z):
    w,d=.92,.64
    m.box(x,.20,z,w,.36,d,'wood')
    for sign in (-1,1):
        for y in (.11,.30,.46):
            m.box(x,y,z+sign*d*.50,w+.03,.115,.043,'wood')
            m.box(x+sign*w*.50,y,z,.042,.115,d+.04,'wood')
    for j in range(2):
        for i in range(4):
            c=(x+(i-1.48)*.20+.025*sin(i+j),.40,z+(j-.5)*.25)
            m.tube(c,(c[0]+.028,.53+.035*sin(i*2+j),c[2]),.12,'leaf' if (i+j)%3 else 'fruit',7)

def paving(m,width,front,back,top):
    nx,nz=round(width/.87),round((back-front)/.87)
    for j in range(nz):
        for i in range(nx):
            l=-width/2+i*width/nx+.009
            r=l+width/nx-.018
            f=front+j*(back-front)/nz+.009
            k=f+(back-front)/nz-.018
            u=.14*((i*3+j)%4)
            v=.16*((j*3+i)%4)
            m.face([(l,top,f),(r,top,f),(r,top,k),(l,top,k)],'ground',(0,1,0),[(u,v),(u+.48,v),(u+.48,v+.48),(u,v+.48)])

def shop(name,grocery):
    m=Mesh();w,d=8,6;h=6.15 if grocery else 5.56;half=w/2; band=3.12 if grocery else 3.20
    paving(m,8.55,-3.38,.10,.128)
    front='redUpper' if grocery else 'blueUpper';side='redEnd' if grocery else 'blueEnd'
    wins=[(.11,.20,.41,.78),(.59,.20,.90,.78)] if grocery else [(a,.17,a+.117,.76) for a in (.09,.34,.57,.80)]
    wall(m,(-half,0,0),(half,0,0),band,h,front,wins)
    sideWins=[(.09,.52,.36,.80),(.55,.52,.83,.80)] if grocery else [(.15,.52,.35,.80),(.64,.52,.83,.80)]
    wall(m,(-half,0,d),(-half,0,0),0,h,side,sideWins)
    wall(m,(half,0,0),(half,0,d),0,h,side,sideWins)
    wall(m,(half,0,d),(-half,0,d),0,h,side,sideWins)
    wall(m,(-half,0,.011),(half,0,.011),0,band,'plaster')
    m.box(0,.10,d/2,w+.14,.2,d+.14,'stone')
    # Recessed timber/glass bays and a separately slung fabric canopy keep their depth around a corner.
    for x,ww,mat in [(-2.51,2.36,'shelf'),(0,1.49,'door'),(2.44,2.48,'goods' if grocery else 'shelf')]:
        m.box(x,1.44,-.014,ww+.13,2.73,.14,'rim')
        m.face([(x-ww/2,.16,-.094),(x+ww/2,.16,-.094),(x+ww/2,2.73,-.094),(x-ww/2,2.73,-.094)],mat,(0,0,-1))
        for sg in (-1,1):m.box(x+sg*ww/2,1.43,-.116,.067,2.64,.075,'wood')
    m.box(0,band-.11,-.055,w-.06,.48,.16,'canvasRed' if grocery else 'sage')
    for i in range(12):
        x0=-half+.045+(w-.09)*i/12;x1=x0+(w-.09)/12
        mat=('canvasRed' if grocery else 'canvasBlue') if i%2==0 else 'linen'
        def edge(a):
            sag=sin(pi*a)
            return (x0+(x1-x0)*a,band-.60-.049*sag,-1.00-.035*sag)
        for j in range(4):
            a,b=j/4,(j+1)/4
            p,q=edge(a),edge(b)
            surface=[(p[0],band-.29,-.09),(q[0],band-.29,-.09),q,p]
            m.face(surface,mat,(0,1,-.3));m.face(surface[::-1],mat,(0,-1,0))
            hem=[(p[0],p[1]-.13-.065*sin(pi*a),p[2]),(q[0],q[1]-.13-.065*sin(pi*b),q[2]),q,p]
            m.face(hem,mat,(0,0,-1));m.face(hem[::-1],mat,(0,0,1))
    for x in (-half+.12,0,half-.12):
        m.tube((x,band-.95,-.09),(x,band-.62,-.98),.026,'iron',6)
    for x in (-2.7,2.9):
        crate(m,x,-.65)
        if grocery:crate(m,x+(.77 if x<0 else -.78),-.59)
    for x in (-half+.06,half-.06):m.tube((x,.02,-.065),(x,h-.09,-.065),.060,'iron',8)
    rise=1.13 if grocery else 1.50
    roof(m,w,d,h,rise,blue=not grocery,hip=grocery)
    if not grocery:
        m.box(-1.55,h+1.07,d*.61,.66,.97,.60,'stone')
        m.box(-1.55,h+1.58,d*.61,.76,.13,.71,'iron')
    else:
        m.tube((-2,h+.50,d*.68),(-2,h+2.17,d*.68),.16,'iron')
        m.box(-2,h+2.18,d*.68,.44,.12,.44,'iron')
        for x,z,y,ww,dd in [(1.15,4.48,1.13,1.34,1.13),(-1.7,1.42,.71,.91,.66),(2.35,2.48,.61,1.04,.68)]:
            m.box(x,h+y+.22,z,ww,.48,dd,'stone')
            m.box(x,h+y+.48,z,ww+.09,.075,dd+.09,'iron')
    m.write(name)

def shelter():
    m=Mesh()
    paving(m,7.86,-3.45,7.32,.081)
    m.box(0,2.72,3.80,7.70,.27,7.50,'sage')
    m.box(0,2.895,3.80,7.65,.10,7.46,'cream')
    m.face([(-3.75,2.95,7.46),(3.75,2.95,7.46),(3.75,2.95,.14),(-3.75,2.95,.14)],'stationRoof',(0,1,0))
    m.box(0,1.32,7.40,7.26,2.64,.24,'plaster')
    for x in (-3.4,3.4):
        m.box(x,1.35,.48,.34,2.7,.34,'stone');m.box(x,.16,.48,.54,.32,.54,'iron')
    m.box(-2.3,3.15,4.10,1.1,.44,1.65,'stone')
    m.box(-2.3,3.40,4.10,1.20,.095,1.75,'iron')
    m.box(0,2.62,-.01,4.15,.43,.13,'sage')
    for z in (5.75,5.92,6.09):m.box(-1.2,.57,z,3.45,.10,.153,'wood')
    for h in (.77,.94,1.11):m.box(-1.2,h,6.19,3.45,.14,.085,'wood')
    for x in (-2.65,.25):
        for z in (5.77,6.13):m.box(x,.27,z,.10,.54,.10,'iron')
    m.box(-2.72,1.52,7.236,1.0,1.04,.072,'wood')
    m.face([(-3.17,1.07,7.19),(-2.27,1.07,7.19),(-2.27,1.97,7.19),(-3.17,1.97,7.19)],'timeTable',(0,0,-1))
    m.write('RouteStation')

def house(name,front,canopy):
    m=Mesh();w,d,h=8,7.8,5.4
    windowRects=[(.17,.17,.30,.32),(.66,.17,.79,.32),(.17,.67,.30,.80),(.66,.67,.79,.80)]
    wall(m,(-w/2,0,0),(w/2,0,0),0,h,front,windowRects)
    wall(m,(w/2,0,d),(-w/2,0,d),0,h,'plaster')
    for sign in (-1,1):
        start,end=((-w/2,0,d),(-w/2,0,0)) if sign<0 else ((w/2,0,0),(w/2,0,d))
        wall(m,start,end,0,h,'plaster')
        for z in (d*.25,d*.74):
            m.box(sign*(w/2+.018),h*.70,z,.09,1.0,1.25,'rim')
            x=sign*(w/2+.073)
            m.face([(x,h*.70-.43,z-.55),(x,h*.70-.43,z+.55),(x,h*.70+.43,z+.55),(x,h*.70+.43,z-.55)],'darkWindow',(sign,0,0))
        m.box(sign*(w/2+.011),.115,d/2,.09,.23,d,'stone')
    for x in (-w*.24,w*.24):
        m.box(x,h*.70,d+.035,1.13,.83,.10,'rim')
        m.face([(x+.51,h*.70-.35,d+.10),(x-.51,h*.70-.35,d+.10),(x-.51,h*.70+.35,d+.10),(x+.51,h*.70+.35,d+.10)],'darkWindow',(0,0,1))
    m.box(0,.085,-.23,1.32,.17,.50,'stone')
    if canopy:m.box(0,2.09,-.32,1.76,.09,.65,'sage')
    roof(m,w,d,h,1.22)
    m.write(name)

shop('RouteBlueShop',False)
shop('RouteGrocery',True)
shelter()
for n,t,c in [('RouteGrayHouse','houseGray',False),('RouteSageHouse','houseGreen',False),('RouteCreamHouse','houseCream',True)]:
    house(n,t,c)

# Two pairs of individually masked crowns from the approved residential paintings.
crowns=Image.open(src.parent/'residential-crowns.png').convert('RGBA')
half=crowns.height//2
for row,name in enumerate(('RouteLeaves','RouteBlossom')):
    crowns.crop((0,row*half,crowns.width,(row+1)*half)).save(out/(name+'.png'))
Image.open(src.parent/'residential-turf.png').convert('RGB').save(out/'RouteGrass.png')
