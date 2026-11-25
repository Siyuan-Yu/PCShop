from pathlib import Path
base=Path(__file__).parent
p=base/'refine.py';s=p.read_text(encoding='utf-8-sig')
s=s.replace("soft((1.0-np.abs(th))/.28)", "soft((.89-np.abs(th))/.25)")
s=s.replace("pic=blend(hue,pic,w)","""pic=blend(hue,pic,w)
   # Preserve brows and mouth while fading the residual drawn sideburn near the eye.
   if lady:
    blemish=soft((np.abs(th)-.47)/.14)*soft((v-.31)/.12)
    pic=blend(pic,hue,blemish)""")
s=s.replace("  elif k in ('jacket','cardigan'):", """  elif k=='skirt' or k=='hips' or ('leg' in k and not lady):
   # The source image has hands drawn on the trousers and skirt, and sleeves at their waist.
   if k=='skirt':
    folds=.96+.056*np.cos(15*th+.4*np.sin(v*3))+.014*np.cos(29*th)
    cloth=folds[...,None]*np.array([107,73,59])
    pic=blend(cloth,pic,soft((v-.42)/.16)*.82)
   else:
    cloth=np.array([57,63,66])*(.98+.045*np.cos(4*th+.7*v)+.007*np.sin(171*v+98*th))[...,None]
    w=soft((v-.31)/.14) if k!='hips' else np.zeros_like(v)
    pic=blend(cloth,pic,w)
  elif k in ('jacket','cardigan'):""")
s=s.replace("sign*.15-.006,1.448,-.005","sign*.172-.006,1.449,-.079").replace("[.028,.048,.029]","[.030,.048,.030]").replace("back*h**2*(.066","back*h**2*(.049")
s=s.replace("w=soft((c-.63)/.27)*(.80 if k in ('hair','crown') else 0)","w=soft((c-.63)/.27)*(.80 if k in ('hair','crown') else 0)")
s=s.replace("atlas.paste(Image.fromarray(tile.astype('uint8')),(px,py))","""if k=='face':
    # Inner pinna on both sides of the small separate ear volume.
    px-=75
    curl=np.exp(-((np.sin(xx*2*np.pi)*1.85)**2+((yy-.49)/.18)**2)*1.6)
    tile-=curl[...,None]*np.array([13,27,23])
   atlas.paste(Image.fromarray(tile.astype('uint8')),(px,py))""")
p.write_text(s,encoding='utf-8')
