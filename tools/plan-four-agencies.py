"""Apply the approved four horizontal agency jurisdictions, preserving legacy aliases."""
import json
from pathlib import Path

def apply(data):
    districts=[dict(id='outskirts',name='城郊片区',agencyId='agency',streets=['s_oldhomes','s_start','s_electronics']),
      dict(id='community',name='社区片区',agencyId='agency_s_services',streets=['s_services','s_market']),
      dict(id='park',name='公园片区',agencyId='agency_s_sports',streets=['s_sports','s_creative','s_expo']),
      dict(id='garden',name='花园片区',agencyId='agency_s_night',streets=['s_newtown','s_night'])]
    owner={street:d['agencyId'] for d in districts for street in d['streets']}
    aliases={x['from']:x['to'] for x in data.get('placeAliases',[])}
    original={p['id']:p for p in data['places']}
    if 'agency_s_oldhomes' in original:original['agency']['node']=original['agency_s_oldhomes']['node']
    for p in data['places']:
        if p['kind']=='agency' and p['id']!=owner[p['street']]:aliases[p['id']]=owner[p['street']]
    keep={d['agencyId']:d for d in districts}
    data['places']=[p for p in data['places'] if p['kind']!='agency' or p['id'] in keep]
    for p in data['places']:
        if p['kind']=='agency':p['name']=keep[p['id']]['name'].replace('片区','中介')
    data.update(layoutVersion=8,agencyDistricts=districts,placeAliases=[dict(from_=a,to=b) for a,b in aliases.items()])
    for alias in data['placeAliases']:alias['from']=alias.pop('from_')
    return data

if __name__=='__main__':
    import runpy
    path=Path(__file__).resolve().parents[1]/'Assets/Resources/Data/city_layout.json'
    data=apply(json.loads(path.read_text(encoding='utf-8-sig')))
    data=runpy.run_path(str(Path(__file__).with_name('densify-city.py')))['populate'](data)
    path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('4 agencies, 10 covered streets, legacy agency aliases:',len(data['placeAliases']))
