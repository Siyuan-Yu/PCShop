from pathlib import Path
stage=Path(__file__).resolve().parent
old=stage.parent/'ArtStudy-1795533921479'/'StreetWork'
code=(old/'street_model.py').read_text(encoding='utf8')
# Use the original paint atlas coordinates, including its sorted swatches and padding.
env={'__file__':str(old/'street_model.py')}
exec(code[:code.index('for name,flag,size in')],env)
globals().update({k:v for k,v in env.items() if not k.startswith('__')})
templates={}
original_keys=sorted({k for g,items in batches.items() if g not in ('Canopies','PottedLeaves') for _,_,k,_ in items})
def begin(name):
 env['group']=name
 templates[name]=batches[name]
def copy_piece(name,source,keep=lambda p,k:True,shift=(0,0,0)):
 begin(name)
 for p,n,k,u in batches[source]:
  if keep(p,k):templates[name].append((p+shift,n,k,u))
copy_piece('TileRoof','OldRoof',lambda p,k:p[:,2].max()<10.82 and k!='92806b' and k not in ('steel','79817b'),(0,0,-10.5))
copy_piece('WaterTanks','OldRoof',lambda p,k:p[:,2].min()>10.60 and p[:,0].min()>1.4 and p[:,0].max()<3.7 and p[:,1].min()>3 and p[:,1].max()<4.6,(-2.55,-3.9,-10.60))
copy_piece('BalconyRail','Balcony',shift=(-2.9,0,-6.03))
copy_piece('Cycle','Bicycle',shift=(4.65,-1.38,-.105))
copy_piece('Chalkboard','Doorstep',lambda p,k:p[:,0].min()>1.40 and p[:,0].max()<2.25,( -1.82,.65,-.105))
# A single floor of each original recessed elevation, preserving the glazing and ironwork.
for level in (1,2):
 lo=3.05+2.48*(level-1)
 copy_piece('Upper'+str(level),'UpperFront',lambda p,k: p[:,2].mean()>=lo-.065 and p[:,2].mean()<lo+2.48-.065,(0,0,-lo))
begin('WallShell')
# Separate face strips keep the plaster grain close to its authored scale.
for j in range(3):
 x=-4.35+2.90*j
 for y,norm in [(0,-1),(5.8,1)]:
  face([(x,y,0),(x+2.90,y,0),(x+2.90,y,3.05),(x,y,3.05)],'plaster',(0,norm,0))
for x,normal in [(-4.35,-1),(4.35,1)]:
 for j in range(2):
  y=2.90*j
  face([(x,y,0),(x,y+2.90,0),(x,y+2.90,3.05),(x,y,3.05)],'wall2',(normal,0,0))
for y in (0,5.8):box(0,y,.015,8.72,.084,.30,'brick')
for x in (-4.35,4.35):box(x,2.9,.015,.085,5.81,.30,'brick')
begin('DisplayFront')
for x in (-2.78,2.78):shopwindow(x,.43,2.35,1.68,'goods')
shopwindow(0,.11,1.54,2.08,'shopdoor')
for x in (-.12,.10):box(x,-.24,1.03,.026,.045,.25,'ab9463')
box(0,-.32,.025,1.85,.64,.09,'ac9b82',top='paving')
begin('ShutterFront')
for x in (-2.75,2.75):shopwindow(x,.25,2.53,1.9,'blind')
shopwindow(0,.11,1.5,2.10,'door')
begin('ResidenceFront')
shopwindow(0,.115,1.36,2.08,'door');box(0,-.34,.014,1.68,.71,.12,'a9987d')
for x in (-2.65,2.65):
 shopwindow(x,.89,1.54,1.13,'curtain')
 for k in range(6):rod((x-.62+k*.25,-.28,.93),(x-.62+k*.25,-.28,1.97),.015,'675d50')
 box(x,-.285,1.31,1.5,.04,.034,'675d50')
box(.97,-.12,1.68,.33,.08,.26,'899083')
begin('StripedCanopy');awning(0,7.8,0,1.01,'canvas')
begin('TinCanopy');awning(0,7.8,0,.88,'redtin')
begin('ServiceStack')
rod((0,-.10,.13),(0,-.10,3.04),.045,'9c9981',n=10)
for z in (.31,1.64,2.91):box(0,-.10,z,.16,.14,.048,'777767')
rod((0,-.10,.14),(0,-.22,.073),.045,'9c9981',n=10)
box(.44,-.08,1.44,.31,.14,.37,'838775');cable((.44,-.06,1.42),(.16,-.03,.32),.17,.016,'756c5b',6)
begin('Crates')
for x,y,z in [(-.35,0,.06),(.34,.10,.06),(-.33,0,.44)]:
 for dx in (-.27,.27):
  for dy in (-.17,.17):box(x+dx,y+dy,z,.052,.049,.37,'wood')
 for h in (.035,.175,.295):
  for dy in (-.19,.19):box(x,y+dy,z+h,.60,.040,.093,'wood')
  for dx in (-.29,.29):box(x+dx,y,z+h,.040,.40,.093,'wood')
 box(x,y,z,.57,.38,.036,'wood')
begin('CycleShelter')
for x in (-1.50,1.50):
 for y in (0,1.30):rod((x,y,0),(x,y,1.92+y*.18),.034,'79817b')
for j in range(3):
 x=-1.70+j*1.14
 face([(x,-.16,1.96),(x+1.14,-.16,1.96),(x+1.14,1.53,2.25),(x,1.53,2.25)],'blind',(0,-.18,1))
 box(x+.57,-.16,1.92,1.15,.05,.06,'79817b')
begin('Bench')
for x in (-.67,.67):
 box(x,0,0,.12,.41,.43,'a28d6f');box(x,.17,.38,.060,.070,.53,'79817b')
for y in (-.16,0,.16):box(0,y,.43,1.75,.14,.062,'wood')
for z in (.67,.83):box(0,.18,z,1.75,.068,.12,'wood')
begin('Drain')
box(0,0,0,.65,.32,.020,'514c42')
for j in range(10):box(-.285+j*.063,0,.019,.023,.294,.013,'79817b')
begin('BenchTools')
# Repair mat, parts tray, driver handle and steel shaft, and stacked manuals.
box(-.09,.01,0,.56,.34,.012,'698173')
box(.11,.035,.012,.17,.105,.018,'79817b')
for j in range(4):box(.049+.037*j,.035,.03,.019,.064,.014,'514c42')
rod((-.27,-.11,.035),(-.14,-.07,.035),.024,'ad8059');rod((-.14,-.07,.035),(-.055,-.04,.035),.007,'999786')
for j in range(2):box(.34,.07,j*.034,.15,.19,.032,'a18a66')
begin('DrawerFront')
for i in range(3):
 x=(i-1)*.56
 box(x,0,.12,.53,.037,.56,'wood')
 box(x,-.036,.55,.19,.033,.026,'999786')
begin('YardWall')
for j in range(3):
 x=-1.32+1.32*j
 box(x,0,.02,1.32,.16,.86,'wall2')
 box(x,0,.88,1.33,.22,.065,'a28d6f')
for x in (-1.96,1.96):box(x,0,.014,.24,.25,1.025,'brick')
copy_piece('PowerPole','UtilityLines',lambda p,k:np.max(abs(p[:,0]+7.70))<.25 and k not in ('514c42','5b5142'),(7.70,2.24,-.105))
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
# Map new swatches to the nearest flat pigment from the reference atlas.
flat=[k for k in original_keys if len(k)==6 and all(c in 'abcdef0123456789' for c in k)]
def rgb(k):return np.array([int(k[i:i+2],16) for i in (0,2,4)])
for ls in templates.values():
 for i,(ps,n,k,uv) in enumerate(ls):
  if k not in original_keys:ls[i]=(ps,n,min(flat,key=lambda c:np.linalg.norm(rgb(c)-rgb(k))),uv)
exp=code[code.index('def export('):code.index('for name,flag,size in')]
exp=exp.replace("atlas.save(root/(name+'Albedo.png'))","pass")
out=stage/'Modules';out.mkdir(exist_ok=True);env['work']=out
exec(exp,env);env['export']('Street',templates,original_keys,2048)

