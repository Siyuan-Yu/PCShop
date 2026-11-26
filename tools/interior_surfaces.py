"""重新绘制室内可平铺面材和隔窗远景。依赖 Pillow、NumPy。"""
from pathlib import Path
import numpy as np
from PIL import Image

OUT = Path(__file__).resolve().parents[1] / "Assets/Resources/OldTown/Surfaces"
N = 512
y, x = np.indices((N, N)) / N
rng = np.random.default_rng(2626)
grain = rng.normal(0, 1, (N, N))
def color(name, rgb, delta):
    Image.fromarray(np.clip(np.array(rgb) + delta[:, :, None], 0, 255).astype("uint8")).save(OUT / (name + ".png"))
def periodic(i, j):
    return np.sin((i % 16) * 12.9898 + (j % 16)*78.233)*593.5 % 1

for name, rgb, grid, depth in [("terraTile",(190,166,143),7,22),("creamTile",(205,198,183),6,23),
                                ("interiorPorcelain",(217,210,201),2,10),("officeSlate",(121,120,115),3,11),
                                ("officeCarpet",(135,135,128),6,9)]:
    xx, yy = x*grid, y*grid
    seam = np.minimum(np.minimum(xx%1,1-xx%1),np.minimum(yy%1,1-yy%1))
    patch = (periodic(np.floor(xx),np.floor(yy))-.5)*3
    variation = grain*(1.15 if name!="terraTile" else 3.2) + patch - depth*np.maximum(0,1-seam/(grid*.0035))
    if name in ("interiorPorcelain","officeSlate"):
        vein=np.sin(2*np.pi*(2*x+y+.19*np.sin(2*np.pi*y)+.03*np.sin(2*np.pi*(5*x+2*y))))
        variation-=np.exp(-abs(vein)*45)*5
    if name=="officeCarpet":
        variation+=((periodic(np.floor(xx),np.floor(yy))-.5)*10)
        variation+=2.7*np.sin((np.where((np.floor(xx)+np.floor(yy))%2==0,x,y))*2*np.pi*160)
    color(name,rgb,variation)
u=(x+y)*16
v=(y-x)*16
a,b=np.floor(u),np.floor(v)
hor=(a-b)%8<4
along=np.where(hor,(a-b)%8+u%1,(b-a-1)%8+v%1)
across=np.where(hor,v%1,u%1)
code=periodic(np.where(hor,a-(a-b)%8,a),np.where(hor,b,b-(b-a-1)%8))
seam=np.minimum.reduce([across,1-across,along,4-along])
wood=12*(code-.5)+2.7*np.sin(2*np.pi*(across*8+.13*np.sin(along*2)+code)) + grain*.95
color("oakParquet",(183,146,111),wood-22*np.maximum(0,1-seam/.055))

# Physical surface relief: joints are recessed, limewash is almost flat, oak follows the grain.
lime = 1.5*np.sin(2*np.pi*(3*x+y+.17*np.sin(2*np.pi*x))) + grain*1.9
color("roomLimewash",(231,228,218),lime)
# Mineral patches are continuous across the old plaster. Their edges taper into the paint.
mottle=np.zeros((N,N))
for fx,fy,amp,phase in [(1,2,6,.1),(3,-2,4,.7),(5,5,2,.4),(11,-7,1.3,.9),(17,12,.9,.3)]:
    mottle+=amp*np.sin(2*np.pi*(fx*x+fy*y+phase))
patina=np.maximum(0,(mottle-3)*1.9)+7*np.maximum(0,np.sin(2*np.pi*(y+.06*np.sin(x*2*np.pi))))**8
color("wornLimewash",(209,202,179),mottle*.32-patina+grain*2.1)
for surface,amount in [("wornLimewash",.48),("roomLimewash",.34),("terraTile",.70),("creamTile",.52),("oakParquet",.56),
                       ("interiorPorcelain",.25),("officeCarpet",.90),("officeSlate",.55)]:
    luminance=np.asarray(Image.open(OUT/(surface+".png")).convert("L"),dtype=float)
    dx=(np.roll(luminance,-1,axis=1)-np.roll(luminance,1,axis=1))*amount/35
    dy=(np.roll(luminance,-1,axis=0)-np.roll(luminance,1,axis=0))*amount/35
    normal=np.stack([-dx,dy,np.ones_like(dx)],axis=-1)
    normal/=np.linalg.norm(normal,axis=-1,keepdims=True)
    Image.fromarray(np.uint8((normal*.5+.5)*255)).save(OUT/(surface+"Relief.png"))

# Latlong studio environment. The cubemap importer convolves this for rough timber and polished stone.
height,width=256,512
yy,xx=np.indices((height,width))
az=(xx+.5)/width*2*np.pi-np.pi
el=np.pi/2-(yy+.5)/height*np.pi
ground=np.clip(-el/.86,0,1)
room=np.full((height,width,3),(149,137,120),dtype=float)*(1-ground[:,:,None]*.38)
for center in [-1.1,.46]:
    a=(az-center+np.pi)%(2*np.pi)-np.pi
    window=(abs(a)<.62)&(el>-.30)&(el<.47)
    frame=(np.mod((a+.62)*4.15,1)<.08)
    outside=np.stack([169+51*el,188+40*el,173+83*el],axis=-1)
    room[window]=outside[window]
    room[window&frame]*=.43
    strip=(abs(a)<.82)&(abs(el-.59)<.024)
    room[strip]=[255,231,189]
Image.fromarray(np.uint8(np.clip(room,0,255))).save(OUT/"roomReflection.png")
