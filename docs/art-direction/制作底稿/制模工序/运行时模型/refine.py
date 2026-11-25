from pathlib import Path
import struct, numpy as np, math
from PIL import Image
base=Path(__file__).parent
source=base.parent/'ArtStudy-1795533921479/SourceData'
out=base/'Refined';out.mkdir(exist_ok=True)
art=Path.cwd()/'Assets/Art/OldTownShopStudy'
def soft(x):
 x=np.clip(x,0,1);return x*x*(3-2*x)
def blend(a,b,w):return a+(b-a)*w[...,None]
for name in ['JacketYouth','Landlady']:
 lady=name=='Landlady'; atlas=Image.open(art/(name+'Albedo.png')).convert('RGB')
 for f in sorted(source.glob(name+'-*.bin')):
  part=f.stem.split('-')[1];k=part.lower()
  b=f.read_bytes();count,ix=struct.unpack_from('<ii',b)
  a=np.frombuffer(b,'<f4',count=count*8,offset=8).reshape(-1,8).copy(); inds=np.frombuffer(b,'<i4',count=ix,offset=8+count*32).tolist()
  xy=a[:,6:]*[1,-1]+[0,1];x,y=(xy.min(0)*atlas.size).astype(int)
  pic=np.array(atlas.crop((x,y,x+300,y+300))).astype(float); v,u=np.mgrid[0:300,0:300]/299
  th=(u-.5)*2*math.pi;c=np.cos(th);s=np.sin(th)
  skin=np.array([232,176,127] if lady else [223,169,118])
  hue=skin*(.97+.025*np.cos(th-.1)+.014*np.sin(v*3))[...,None]
  cheek=np.exp(-((np.abs(th)-.81)/.36)**2-((v-.67)/.18)**2)
  hue+=cheek[...,None]*np.array([3,-4,-4])
  if k=='face':
   # Eyes, brows, fringe and mouth have one frontal address. The remaining circumference is skin.
   w=soft((.89-np.abs(th))/.25)*soft((.965-v)/.075)
   pic=blend(hue,pic,w)
   # Preserve brows and mouth while fading the residual drawn sideburn near the eye.
   if lady:
    blemish=soft((np.abs(th)-.47)/.14)*soft((v-.31)/.12)
    pic=blend(pic,hue,blemish)
  elif k=='neck' or k=='creamcollar' or 'hand' in k or (lady and 'leg' in k):
   pic=hue.copy()
   if 'hand' in k: pic*= (.98+.025*np.cos(th+.55)*np.sin(np.pi*v))[...,None]
   if k in ('neck','creamcollar'):pic*= (.94+.045*v)[...,None]
  elif 'shoe' in k:
   basec=np.array([111,72,51] if lady else [102,78,57])
   pic=basec*(.84+.18*np.sin(np.pi*v)+.075*c)[...,None]
   pic*= (1-.32*soft((v-.76)/.11))[...,None]
   if not lady:
    for age in (.27,.42):pic+=np.exp(-((v-age-.07*s)/.022)**2)[...,None]*soft((c-.56)/.23)[...,None]*8
  elif k in ('hair','crown','bobback','leftwaves','rightwaves'):
   sweep=th*(7 if lady else 10)+1.6*v+1.1*np.sin(th*2-v*4)
   dune=.5+.5*np.cos(sweep);grain=.5+.5*np.cos(sweep*2.0+.15)
   pigment=np.array([133,72,54] if lady else [65,51,40])*(.78+.17*dune+.065*grain+.075*(1-v))[...,None]
   w=soft((c-.63)/.27)*(.80 if k in ('hair','crown') else 0)
   pic=blend(pigment,pic,w)
   if k in ('hair','crown'):
    q=(a[:,6]*atlas.width-x-.5)/299*2*np.pi-np.pi
    h=(1-a[:,7])*atlas.height;h=np.clip((h-y-.5)/299,0,1)
    back=soft((.36-np.cos(q))/.86);ridge=(.006*np.cos(q*8+h*4)+.003*np.cos(q*13-h*2))*np.sin(np.pi*h)*back
    a[:,0]+=ridge*np.sin(q);a[:,2]-=ridge*np.cos(q)
    a[:,1]-=back*h**2*(.049 if not lady else .014)
    a[:,2]-=back*h**3*(.023 if not lady else .008)
  elif k=='skirt' or k=='hips' or ('leg' in k and not lady):
   # The source image has hands drawn on the trousers and skirt, and sleeves at their waist.
   if k=='skirt':
    folds=.96+.056*np.cos(15*th+.4*np.sin(v*3))+.014*np.cos(29*th)
    cloth=folds[...,None]*np.array([107,73,59])
    pic=blend(cloth,pic,soft((v-.42)/.16)*.82)
   else:
    cloth=np.array([57,63,66])*(.98+.045*np.cos(4*th+.7*v)+.007*np.sin(171*v+98*th))[...,None]
    w=soft((v-.31)/.14) if k!='hips' else np.zeros_like(v)
    pic=blend(cloth,pic,w)
  elif k in ('jacket','cardigan'):
   # The side sheet includes the overlapping arm. Paint the underarm as cloth.
   side=soft((.68-np.abs(c))/.38)
   cloth=np.array([150,104,66] if not lady else [153,70,53])*(.95+.045*np.cos(th*5+v*2))[...,None]
   pic=blend(pic,cloth,side*.94)
  else:continue
  atlas.paste(Image.fromarray(np.pad(np.uint8(np.clip(pic,0,255)),((7,7),(7,7),(0,0)),mode='edge')),(x-7,y-7))
  # Model the pinna and thumb with a single continuous silhouette. Separate ear paint is a tiny swatch in unused atlas space.
  def oval(center,radii,patch,tilt=0):
   global a,inds
   rows=12;cols=20;add=[];ii=len(a)
   for j in range(rows+1):
    phi=np.pi*j/rows
    for i in range(cols+1):
     lon=2*np.pi*i/cols;n=np.array([np.sin(phi)*np.cos(lon),np.cos(phi),np.sin(phi)*np.sin(lon)])
     p=n*np.array(radii);p[0]+=tilt*p[1]
     normal=n/np.array(radii);normal[1]-=tilt*normal[0];normal/=np.linalg.norm(normal)
     add.append([*(center+p),*normal,(patch[0]+i/cols*60)/atlas.width,1-(patch[1]+j/rows*60)/atlas.height])
   for j in range(rows):
    for i in range(cols):
     n=ii+j*(cols+1)+i
     inds+= [n,n+1,n+cols+1,n+1,n+cols+2,n+cols+1]
   a=np.concatenate([a,np.array(add,dtype='f4')])
  if k=='face' or 'hand' in k:
   px=1820;py=atlas.height-85
   yy,xx=np.mgrid[0:64,0:64]/63
   tile=np.zeros((64,64,3))+skin;tile*= (.98+.04*np.sin(yy*np.pi))[...,None]
   if k=='face':
    # Inner pinna on both sides of the small separate ear volume.
    px-=75
    curl=np.exp(-((np.sin(xx*2*np.pi)*1.85)**2+((yy-.49)/.18)**2)*1.6)
    tile-=curl[...,None]*np.array([13,27,23])
   atlas.paste(Image.fromarray(tile.astype('uint8')),(px,py))
   if k=='face':
    for sign in [-1,1]:
     oval(np.array([sign*.172-.006,1.449,-.079]),[.030,.048,.030],(px+1,py+1),sign*.13)
   else:
    h=a[:,:3];middle=(h.min(0)+h.max(0))/2
    sign=-1 if 'left' in k or k.startswith('l') else 1
    oval(middle+[-sign*.021,.004,-.039],[.018,.036,.017],(px+1,py+1),sign*.23)
  (out/f.name).write_bytes(struct.pack('<ii',len(a),len(inds))+a.astype('<f4').tobytes()+np.array(inds,dtype='<i4').tobytes())
 atlas.save(out/(name+'Albedo.png'))
