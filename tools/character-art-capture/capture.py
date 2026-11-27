"""同步当前资源到已有复拍工程并启动 Unity Play Mode 截图，不生成发布包。"""
from pathlib import Path
import argparse,os,shutil,subprocess,sys
sys.stdout.reconfigure(encoding='utf-8')

parser=argparse.ArgumentParser()
parser.add_argument('--project',required=True)
parser.add_argument('--output',required=True)
parser.add_argument('--stage',choices=['static','animation'],default='static')
parser.add_argument('--unity',default='D:/UnityEditor/2022.3.6f1/Editor/Unity.exe')
args=parser.parse_args()
root=Path(__file__).resolve().parents[2];project=Path(args.project).resolve();output=Path(args.output).resolve()
if project==root:raise ValueError('复拍必须用独立工程，避免改变正在编辑的主场景。')
if not (project/'ProjectSettings/ProjectVersion.txt').exists():raise ValueError('请传入已创建的 Unity 复拍工程。')
output.mkdir(parents=True,exist_ok=True)
for folder in ['Scripts','Resources','Art','Editor']:
    src=root/'Assets'/folder
    for file in src.rglob('*'):
        if not file.is_file():continue
        target=project/'Assets'/folder/file.relative_to(src)
        if target.exists() and target.stat().st_size==file.stat().st_size and abs(target.stat().st_mtime-file.stat().st_mtime)<.01:continue
        target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(file,target)
shutil.copy2(Path(__file__).with_name('CharacterArtCapture.cs'),project/'Assets/Editor/CharacterArtCapture.cs')
cmd=[args.unity,'-batchmode','-force-d3d11','-projectPath',str(project),'-executeMethod','CharacterArtCapture.Run','-logFile',str(output/'unity.log')]
p=subprocess.Popen(cmd,env=dict(os.environ,PCSHOP_CHARACTER_SHOTS=str(output),PCSHOP_CHARACTER_STAGE=args.stage),creationflags=subprocess.CREATE_NO_WINDOW,
    stdin=subprocess.DEVNULL,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
print('Unity 截图进程',p.pid,'输出',output)
