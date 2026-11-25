from pathlib import Path
p=Path(__file__).with_name('make_modules.py')
s=p.read_text(encoding='utf-8-sig').replace("p[:,2].max()<10.82","p[:,2].max()<10.82 and k!='92806b' and k not in ('steel','79817b')")
k=s.index('# Preserve original atlas')
s=s[:k]+'''copy_piece('PowerPole','UtilityLines',lambda p,k:np.max(abs(p[:,0]+7.70))<.25 and k not in ('514c42','5b5142'),(7.70,2.24,-.105))
begin('PowerSpan')
for dy,dz in [(-.42,0),(0,.02),(.42,-.02)]:
 cable((0,dy,dz),(12,dy,dz),.44,.016,'514c42',14)
begin('SideWindow')
shopwindow(0,0,1.36,1.05,'curtain')
for j in range(5):box(-.54+.27*j,-.255,.04,.022,.026,.98,'675d50')
box(0,-.257,.45,1.34,.027,.036,'675d50')
begin('Condenser')
box(0,0,.08,.82,.48,.52,'ad9e80',front='AC')
for side in (-1,1):box(.28*side,.015,0,.084,.52,.09,'756c5b')
begin('PlantPot')
pot(0,0,1.06)
for j in range(14):
 a=j*2.39996; h=.31+(j%4)*.059;reach=.19+j%3*.08
 base=np.array((0,0,.53));mid=np.array((cos(a)*reach*.59,sin(a)*reach*.59,.53+h))
 tip=np.array((cos(a)*reach,sin(a)*reach,.53+h-.11));across=np.array((-sin(a),cos(a),0))*.077
 c=['899083','698173','80715c'][j%3]
 for poly in ([base,mid-across,mid],[base,mid,mid+across],[mid-across,tip,mid],[mid,tip,mid+across]):
  face(poly,c,(.1,.2,1));face(list(reversed(poly)),c,(-.1,-.2,-1))
begin('CargoPallet')
for x in (-.42,0,.42):box(x,0,0,.09,.76,.11,'wood')
for y in (-.32,-.16,0,.16,.32):box(0,y,.11,1.16,.13,.055,'wood')
for x,y,z,w,d,h in [(-.25,.12,.165,.56,.44,.42),(.28,.14,.165,.48,.45,.32),(-.24,.11,.585,.48,.37,.24)]:
 box(x,y,z,w,d,h,'a18a66');box(x,y,z+h,.061,d+.005,.007,'c1ad8c')
 box(x,y-d/2-.002,z+h*.53,w*.26,.009,h*.29,'c5baa0')
begin('CargoBay')
shopwindow(0,.16,3.30,2.54,'blind')
for j in range(9):box(0,-.24,.31+j*.265,3.12,.025,.018,'675d50')
box(0,-.26,.44,.38,.056,.054,'79817b')
begin('StoreRack')
for x in (-.74,.74):
 for y in (-.24,.24):box(x,y,.01,.06,.07,1.00,'wood')
for z in (.18,.62):
 box(0,0,z,1.58,.56,.071,'wood')
 for j in range(5):
  x=-.6+j*.30
  box(x,.08,z+.071,.26,.23,.31-j%2*.055,'a18a66',front='goods')
begin('WashLine')
for x in (-1.35,1.35):rod((x,0,0),(x,0,1.83),.035,'7b6c54')
cable((-1.35,0,1.76),(1.35,0,1.76),.10,.014,'675d50',8)
for j,c in enumerate(['b39583','a7aca0','c5baa0']):
 x=-1.11+j*.73
 for a in range(6):
  x0=x+a*.092;x1=x0+.092
  def v(xx,t):return (xx,.027*sin(27*xx)+.035*t,1.68-t*(.68+.04*cos(12*xx)))
  poly=[v(x0,0),v(x1,0),v(x1,1),v(x0,1)]
  face(poly,c,(0,-1,0));face(poly[::-1],c,(0,1,0))
''' + s[k:]
s=s.replace("# Preserve original atlas allocation; generate only vertex buffers.","""# Map new swatches to the nearest flat pigment from the reference atlas.
flat=[k for k in original_keys if len(k)==6 and all(c in 'abcdef0123456789' for c in k)]
def rgb(k):return np.array([int(k[i:i+2],16) for i in (0,2,4)])
for ls in templates.values():
 for i,(ps,n,k,uv) in enumerate(ls):
  if k not in original_keys:ls[i]=(ps,n,min(flat,key=lambda c:np.linalg.norm(rgb(c)-rgb(k))),uv)""")
p.write_text(s,encoding='utf8')
