from pathlib import Path
from PIL import Image
import numpy as np,struct
base=Path(__file__).parent
art=Path('Assets/Art/OldTownShopStudy');out=base/'Refined'
for actor,part in [('JacketYouth','face'),('Landlady','Face')]:
 f=base.parent/'ArtStudy-1795533921479/SourceData'/f'{actor}-{part}.bin'
 b=f.read_bytes();n,ix=struct.unpack_from('<ii',b);a=np.frombuffer(b,'<f4',count=n*8,offset=8).reshape(-1,8).copy()
 atlas=Image.open(art/(actor+'Albedo.png')).convert('RGB')
 q=a[:,6:]*[1,-1]+[0,1];x,y=(q.min(0)*atlas.size).astype(int)
 u=(a[:,6]*atlas.width-x-.5)/299;theta=np.abs((u-.5)*np.pi*2)
 # All vertices are on the existing manifold; no shell or silhouette appendage.
 lift=.0022*np.exp(-((theta-1.30)/.20)**2-((a[:,1]-1.441)/.031)**2)
 a[:,0]+=np.sign(a[:,0])*lift
 (out/f.name).write_bytes(struct.pack('<ii',n,ix)+a.astype('<f4').tobytes()+b[8+n*32:])
 v,u=np.mgrid[0:300,0:300]/299
 pic=np.array(atlas.crop((x,y,x+300,y+300)),dtype=float)
 # Texture height is parameterised by the fitted chin rings in these two different faces.
 rings=np.clip(np.round((1-a[:,7])*atlas.height-y-.5),0,299).astype(int)
 ys=np.full(300,np.nan)
 for j in np.unique(rings):ys[j]=np.median(a[rings==j,1])
 known=np.where(~np.isnan(ys))[0]
 z=np.interp(range(300),known,ys[known])[:,None]
 tx=(abs((u-.5)*2*np.pi)-1.29)/.15
 ty=(z-1.438)/.028
 radius=np.sqrt((tx+.15*ty)**2+ty**2)
 fold=np.exp(-((radius-.80)/.22)**2)*(np.clip(.8+.42*ty-.22*tx,0,1))
 inner=np.exp(-(tx**2*3+(ty+.08)**2*3))
 pic+=fold[...,None]*np.array([1.2,-6,-6.5])+inner[...,None]*np.array([1.4,-2,-3.5])
 atlas.paste(Image.fromarray(np.pad(np.uint8(np.clip(pic,0,255)),((7,7),(7,7),(0,0)),mode='edge')),(x-7,y-7))
 atlas.save(out/(actor+'Albedo.png'))
 print(actor,n,'vertices; maximum relief',round(max(lift)*1000,2),'mm')
