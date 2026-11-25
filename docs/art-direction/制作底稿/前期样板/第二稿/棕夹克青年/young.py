from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw,ImageFont,ImageFilter
import math,sys
folder=Path(__file__).resolve().parent
# Use the previous standalone shop renderer and layout, without executing its scene capture.
src=(folder.parent/'render2.py').read_text('utf8').split('\nenvironment()')[0]
exec(src.replace("out=Path(__file__).parent","out=Path(__file__).resolve().parent.parent"))
ink=np.asarray(Image.open(folder/'三向原画.png').convert('RGB').resize((1536,1024)))
pieces=[]
for i in range(3):
    im=ink[:,512*i:512*(i+1)].copy()
    val=im.astype(int); valid=~((val[:,:,1]>val[:,:,0]+37)&(val[:,:,1]>val[:,:,2]+30))
    # Extrapolate pigment past the cut-out to keep filtering free of chroma spill.
    for y in range(1024):
        ids=np.flatnonzero(valid[y])
        if len(ids)<6:
            im[y]=[54,44,36]
            continue
        ids=ids[2:-2]; pos=np.arange(512); ii=np.searchsorted(ids,pos).clip(0,len(ids)-1); jj=np.maximum(ii-1,0)
        nearest=np.where(abs(ids[jj]-pos)<abs(ids[ii]-pos),ids[jj],ids[ii]);im[y]=im[y,nearest]
    pieces.append(im)
# World coordinates use the orthographic source in units of roughly 520 painted pixels per metre.
def xyz(x,y,t):
    return np.stack(((x-304)/520,(y-755)/520,(959-t)/520),axis=-1)
actor_parts={}
def surface(key,rows,cap=False,groove=False):
    # Artist-set horizontal sections for each volume. Width and depth can vary independently.
    A=np.array(rows,float); N=32; R=max(12,len(rows)*3); theta=np.linspace(-np.pi,np.pi,N+1)
    def shape(v,theta):
        vv,th=np.broadcast_arrays(v,theta); sn=np.sin(th);co=np.cos(th)
        q=[np.interp(vv,np.linspace(0,1,len(A)),A[:,i]) for i in range(5)]
        t,l,r,near,far=q; x=(r+l)/2+(r-l)*.5*sn
        d=(far+near)/2-(far-near)*.5*co
        if cap:
            # Sculpt an asymmetric fringe, sideburns and longer hair at the nape.
            front=np.clip(co,0,1); side=np.abs(sn)
            lower=252-79*front**1.2-17*side+8*np.sin(3*th)+9*np.sin(7*th)*front
            t=t+(lower-218)*vv**2.6
            wave=(2.4*np.sin(th*5+vv*5)+1.6*np.sin(th*9-vv*4))*np.sin(np.pi*vv)
            x+=wave*sn;d-=wave*co
        if groove:
            d+=np.maximum(co,0)*4*(1-np.exp(-(sn/.20)**2))*np.exp(-((t-240)/12)**2)
        return x,d,t
    nr,nc=430,426
    vv=np.linspace(0,1,nr)[:,None]; th=np.linspace(-np.pi,np.pi,nc)[None,:]
    X,D,T=shape(vv,th); a=np.cos(th)
    def lookup(idx,u,v):
        return pieces[idx][np.clip(v.astype(int),0,1023),np.clip(u.astype(int),0,511)].astype(float)
    front=lookup(0,X,T);side=lookup(1,D-512,T);back=lookup(2,1223-(X-304)-1024,T)
    if key in ('leftSleeve','rightSleeve','leftHand','rightHand'):
        # Side sheet's hands, sleeves sit forward of the torso.
        side=lookup(1,np.clip(D,733,798)-512,T)
    if key=='face':
        # Select the entire facial drawing in the frontal arc.
        fw=np.clip((a+.015)/.30,0,1)
    else:fw=np.clip((a-.05)/.53,0,1)
    bw=np.clip((-a-.03)/.52,0,1);sw=1-fw-bw
    albedo=front*fw[:,:,None]+side*sw[:,:,None]+back*bw[:,:,None]
    if cap:
        # The hair mass has its own ink silhouette, including on the crown.
        skin=(albedo[:,:,0]>105)&(albedo[:,:,0]>albedo[:,:,2]+33)
        albedo[skin]=[60,46,36]
    if key=='face':
        # Blend the near-black fringe artwork into the actual overlying geometry.
        pass
    bank[key,'person']=np.clip(albedo*[.94,.93,.90],0,255).astype('uint8')
    rings=[]
    for v in np.linspace(0,1,R):
        x,d,t=shape(v,theta);rings.append(xyz(x,d,t))
    for k,(top,bottom) in enumerate(zip(rings,rings[1:])):
        for j in range(N):
            u=j/N; u1=(j+1)/N;v=k/(R-1);v1=(k+1)/(R-1)
            face([top[j],top[j+1],bottom[j+1],bottom[j]],(key,'person'),(top[:,0].mean(),top[:,1].mean(),top[0,2]),[(u,v),(u1,v),(u1,v1),(u,v1)])
    for row,v in [(rings[0],0),(rings[-1],1)]:
        for j in range(N):
            face([row[j],row[j+1],row.mean(0)],(key,'person'),row.mean(0)+[0,0,.08 if v else -.08],[(j/N,v),((j+1)/N,v),(.5,v)])
def actor(x=0,y=0,angle=0):
    st=len(mesh)
    surface('jacket',[(285,278,327,738,785),(301,252,355,718,800),(320,227,385,700,811),(354,214,396,689,820),(395,213,397,681,824),(448,217,393,676,831),(501,215,398,671,836),(541,212,398,670,838),(565,214,395,673,836)])
    surface('neck',[(257,278,327,726,775),(280,278,326,727,781),(311,278,329,733,779)])
    surface('hips',[(528,218,389,689,817),(560,215,392,685,823),(587,213,394,692,821),(614,218,392,697,818),(633,229,382,700,811)])
    for s,tag in [(-1,'left'),(1,'right')]:
        def mirror(rows):
            if s==-1:return rows
            return [(t,608-r,608-l,d+(0 if t<900 else 3),e+(0 if t<900 else 3)) for t,l,r,d,e in rows]
        surface(tag+'Leg',mirror([(565,217,303,689,818),(604,214,307,695,823),(660,212,300,702,820),(744,209,290,709,817),(812,207,283,714,814),(875,206,279,720,810),(893,212,273,721,806)]))
        surface(tag+'Shoe',mirror([(888,218,269,736,800),(903,212,273,708,804),(919,196,275,673,807),(938,185,276,656,809),(952,189,271,661,807),(957,197,264,670,802)]))
        surface(tag+'Sleeve',mirror([(319,228,246,746,789),(334,207,246,742,798),(367,190,235,736,807),(413,177,224,732,810),(468,162,216,728,811),(514,154,212,726,812),(538,156,213,727,810),(559,159,209,730,805)]))
        surface(tag+'Hand',mirror([(551,167,203,740,791),(570,161,209,734,793),(591,159,213,728,789),(609,162,212,729,786),(626,173,201,738,776),(632,183,196,747,764)]))
    surface('face',[(113,254,352,703,806),(147,223,379,675,826),(178,214,385,670,829),(203,215,384,669,825),(224,220,382,667,820),(238,225,379,661,813),(252,238,370,667,806),(269,250,357,671,792),(281,271,338,682,776),(291,288,322,694,752)],groove=True)
    surface('hair',[(66,277,295,695,714),(74,255,341,685,796),(83,242,367,677,819),(99,214,374,653,831),(117,211,395,645,848),(137,197,399,650,855),(158,190,408,644,870),(178,200,410,644,872),(197,197,404,659,853),(211,210,396,673,840),(218,217,388,678,824)],cap=True)
    rotate_from(st,angle,x,y)
    return len(mesh)-st
# Use a softly quantised diffuse term and little direct illumination already present in the pigments.
stock=Image.open(out/'stock.png').convert('RGB')
for i,name in enumerate(('gpu','cpu','board','power','ssd','shipping','leaf','case','mat')):
    d=stock.width/3;a,b=i%3,i//3
    tiles[name]=np.asarray(stock.crop((round(a*d)+3,round(b*d)+3,round((a+1)*d)-3,round((b+1)*d)-3)).resize((164,164),Image.Resampling.LANCZOS))
# Fine wood grain must be filtered at the game camera distance.
for name in ('wood','enamel','plaster','grass','paving','road'):tiles[name]=np.asarray(Image.fromarray(tiles[name]).resize((150,150),Image.Resampling.LANCZOS))
def interior():
    environment()
    for i in range(len(mesh)):
        p,n,(c,k),uv=mesh[i]
        if k in ('board','gpu') and p.mean(0)[2]>1.1:
            k=['cpu','ssd','power','board'][int((p.mean(0)[0]+5)*2)%4]
        mesh[i]=p,n,(c,k),uv
pos=(-1.23,-.95); cam=np.array([-6.4,-12,7.8]); aim=np.array([-.79,-.33,.86])
# Independent warm indoor colour-grade; diffuse highlights retain the cool navy of the trousers.
def grade(img):
    arr=np.asarray(img).astype(float)/255
    return Image.fromarray(np.clip((arr**1.035)*[.96,.89,.80]*255+np.array([3,2,3]),0,255).astype('uint8'))
interior(); ci=len(mesh);tri=actor(*pos,27)
world=grade(render(1280,882,aim,cam,6.9,'99917d',19))
world.save(folder/'01-店内镜头.png')
mesh.clear();actor()
def view(az,el,px=285):
    t=math.radians(az);d=[math.sin(t),-math.cos(t),el]
    return render(px,round(px*1.64),[0,0,.875],d,1.22,'e3ddcf',3)
board=Image.new('RGB',(1350,674),'#e3ddcf');dr=ImageDraw.Draw(board)
dr.text((48,25),'棕夹克青年',font=imgfont(31),fill='#443c31')
dr.text((47,79),'静态模型  /  同一套网格与贴图',font=imgfont(17),fill='#716656');dr.line((48,116,1302,116),fill='#c6bcaa')
for i,(az,el,txt) in enumerate([(0,.06,'正面'),(36,.18,'斜侧'),(152,.16,'背面'),(32,.62,'店内俯视角')]):
    fr=view(az,el,320).resize((278,456),Image.Resampling.LANCZOS)
    board.paste(fr,(47+i*320,143));dr.text((185+i*320,614),txt,font=imgfont(18),fill='#5c5448',anchor='ma')
board.save(folder/'02-棕夹克青年转面.png')
print(tri,flush=True)

