"""Add the permanent community MCN to the authoritative lake-city source."""
import json
from pathlib import Path
root=Path(__file__).resolve().parents[1]
path=root/'tools/data/city-layout-v8.json'
data=json.loads(path.read_text(encoding='utf-8-sig'))
if not any(p['id']=='life_mcn' for p in data['places']):
    data['places'].append(dict(id='life_mcn',name='澄光传媒 MCN',street='s_services',node='life_mcn_door',business='mcn',
        kind='shop',size='small',explicitEntrance=True,entranceX=0,entranceY=6,width=22,depth=16,heading=0,
        tradable=False,openMinute=540,closeMinute=1080,model='office',tier=1))
    data['nodes'].append(dict(id='life_mcn_door',x=80,y=80))
path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
