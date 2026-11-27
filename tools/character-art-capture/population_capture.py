"""全体人物复拍入口；只启动独立 Unity 编辑器，不运行测试或发布构建。"""
from pathlib import Path
import argparse,os,shutil,subprocess,json,hashlib
p=argparse.ArgumentParser();p.add_argument('--output',required=True);p.add_argument('--census',action='store_true');p.add_argument('--all-frames',action='store_true');p.add_argument('--context',action='store_true');p.add_argument('--project',default='D:/tmp/pcshop-suburb-day-mam5h57m/preview');a=p.parse_args()
root=Path(__file__).resolve().parents[2];project=Path(a.project).resolve();out=Path(a.output).resolve()
if project==root:raise ValueError('只能使用独立复拍工程')
out.mkdir(parents=True,exist_ok=True)
for folder in ('Scripts','Resources','Art','Editor'):
    for f in (root/'Assets'/folder).rglob('*'):
        if not f.is_file():continue
        target=project/'Assets'/folder/f.relative_to(root/'Assets'/folder)
        if target.exists() and target.stat().st_size==f.stat().st_size and abs(target.stat().st_mtime-f.stat().st_mtime)<.01:continue
        target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,target)
shutil.copy2(Path(__file__).with_name('NpcPopulationCapture.cs'),project/'Assets/Editor/NpcPopulationCapture.cs')
sources=list((root/'Assets/Resources/Characters').glob('*'))+[root/'Assets/Scripts/Game/Runtime'/n for n in ('Mats.cs','NaturalFigure.cs','NpcArtCatalog.cs','CityNpcClothing.cs','PlayerCharacterModel.cs','FigureMotion.cs')]+[Path(__file__).with_name('NpcPopulationCapture.cs')]
(out/'复拍来源.json').write_text(json.dumps({'说明':'独立 Unity 工程，正式人物入口，原始截图无补画；无自动测试或发布构建。','sourceSha256':{str(f.relative_to(root)).replace('\\','/'):hashlib.sha256(f.read_bytes()).hexdigest() for f in sources if f.is_file() and f.suffix!='.meta'}},ensure_ascii=False,indent=2),encoding='utf-8')
cmd=['D:/UnityEditor/2022.3.6f1/Editor/Unity.exe','-batchmode','-force-d3d11','-projectPath',str(project),'-executeMethod','NpcPopulationCapture.'+('Census' if a.census else 'Run'),'-logFile',str(out/'unity.log')]
child=subprocess.Popen(cmd,env=dict(os.environ,PCSHOP_CHARACTER_SHOTS=str(out),PCSHOP_NPC_CONTEXT='1' if a.context else '0',PCSHOP_NPC_ACTIONS='all' if a.all_frames else 'sample'),creationflags=subprocess.CREATE_NO_WINDOW,stdin=subprocess.DEVNULL,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
print(child.pid)
