"""从实际 Unity 复拍档案生成全员索引与来源记录，不运行游戏测试。"""
from pathlib import Path
import argparse,csv,hashlib,html,json
from collections import defaultdict

p=argparse.ArgumentParser();p.add_argument('archive');a=p.parse_args()
root=Path(__file__).resolve().parents[2];archive=Path(a.archive)
catalog=json.loads((root/'Assets/Resources/Characters/NpcArtCatalog.json').read_text(encoding='utf-8'))['profiles']
profiles={r['id']:r for r in catalog}
portraits={f.stem for f in (root/'Assets/Resources/PortraitsAnime').glob('*.png')}-{'default'}
groups=sorted(d for d in archive.iterdir() if d.is_dir() and (d/'网格记录.tsv').exists())
census={r['Id']:r for r in csv.DictReader((groups[0]/'人口清单.tsv').open(encoding='utf-8-sig'),delimiter='\t')}
seen={};hash_differences=[];line_endings=[];mesh_issues=[];batches=[];all_meshes=[]
for folder in groups:
    ids=(folder/'角色列表.txt').read_text(encoding='utf-8-sig').splitlines()
    records=list(csv.DictReader((folder/'网格记录.tsv').open(encoding='utf-8-sig'),delimiter='\t'))
    by_id=defaultdict(list)
    for r in records:by_id[r['角色']].append(r)
    for ident in ids:
        if len(by_id[ident])!=3:mesh_issues.append([folder.name,ident,'Renderer 数量不是 3'])
        if any(r['材质']!='PCShop/PlayerPainted' or int(r['顶点'])<=0 for r in by_id[ident]):mesh_issues.append([folder.name,ident,'材质或网格异常'])
    for page in sorted(folder.glob('*-名单.txt')):
        for ident in page.read_text(encoding='utf-8-sig').splitlines():
            seen.setdefault(ident,(folder.name,page.name[:2],by_id[ident]))
    manifest=json.loads((folder/'复拍来源.json').read_text(encoding='utf-8'))['sourceSha256']
    for path,digest in manifest.items():
        file=root/path
        if path=='tools/character-art-capture/NpcPopulationCapture.cs':file=folder/'复拍脚本.cs.txt'
        if not file.exists():hash_differences.append([folder.name,path]);continue
        raw=file.read_bytes()
        if hashlib.sha256(raw).hexdigest()!=digest:
            lf=raw.replace(b'\r\n',b'\n')
            if file.suffix in ('.cs','.txt','.shader','.json') and any(hashlib.sha256(v).hexdigest()==digest for v in (lf,lf.replace(b'\n',b'\r\n'))):line_endings.append([folder.name,path])
            else:hash_differences.append([folder.name,path])
    batches.append({'批次':folder.name,'人物数':len(ids),'分组数':len(list(folder.glob('*-名单.txt'))),'原始静态截图':len(list(folder.glob('*.png'))),'动作GIF':len(list(folder.glob('*.gif'))),'来源文件数':len(manifest)})
    all_meshes+=records
missing_resources=[]
rows=[]
extra_names={'service_officer':'警务人员','service_repair':'维修工','CommunityRoofWorker':'屋顶维修工','CommunityLostChild':'走失儿童','tailor_garden_1':'现场裁缝甲','tailor_garden_2':'现场裁缝乙','CommunityBloodNurse':'献血工作人员','CommunityChildHelper':'寻人协助者','CommunityLanternHost':'灯会摊主','quarter_entry_neighbor':'旧居街坊','quarter_entry_visitor':'旧居访客','quarter_shop_customer':'店前顾客','quarter_stop_commuter':'候车街坊','pond_walk_0':'公园散步者甲','pond_walk_1':'公园散步者乙'}
for ident,profile in profiles.items():
    for key,kind in [('head','Head'),('cut','Top'),('bottom','Bottom')]:
        path=root/'Assets/Resources/Characters'/f'PlayerB-Npc{kind}-{profile[key]}.bytes'
        if not path.exists() or path.read_bytes()[:4]!=b'PBM2':missing_resources.append(str(path.relative_to(root)))
    group,page,meshes=seen.get(ident,('缺失','',[]))
    person=census.get(ident,{})
    rows.append({'Id':ident,'姓名':person.get('姓名',extra_names.get(ident,ident)),'身份':person.get('身份','现场角色'),'原立绘':ident in portraits,'发型':profile['head'],'上衣':profile['cut'],'下装':profile['bottom'],'批次':group,'组':page,'模型顶点合计':sum(int(r['顶点']) for r in meshes),'三角面合计':sum(int(r['三角面']) for r in meshes),'材质槽合计':sum(int(r['子网格']) for r in meshes)})
summary={'日期':'2026-11-27','固定角色':len(census),'原立绘':len(portraits),'外观配置':len(profiles),'额外临时配置':len(profiles.keys()-census.keys()),'已复拍配置':len(profiles.keys()&seen.keys()),'固定角色未登记':sorted(census.keys()-profiles.keys()),'立绘未登记':sorted(portraits-profiles.keys()),'配置未复拍':sorted(profiles.keys()-seen.keys()),'资源缺失':missing_resources,'网格异常':mesh_issues,'来源哈希不一致':hash_differences,'批次':batches,'动态人物实际头型':sorted({r['模型'] for r in all_meshes if r['角色'].startswith('crowd:') and 'NpcHead-' in r['模型']}),'说明':'覆盖率与来源核对不等于视觉相似度分数；截图是原始 Unity 渲染，GIF 只编码实际动作帧。顶点和三角面含合并描边壳，材质槽不等于实际 DrawCall。'}
problems=summary['固定角色未登记']+summary['立绘未登记']+summary['配置未复拍']+missing_resources+mesh_issues+hash_differences
summary['仅文本行尾差异']=line_endings
if problems:raise ValueError(json.dumps(summary,ensure_ascii=False,indent=2))
with (archive/'全员覆盖清单.tsv').open('w',encoding='utf-8-sig',newline='') as f:
    writer=csv.DictWriter(f,fieldnames=list(rows[0]),delimiter='\t');writer.writeheader();writer.writerows(rows)
(archive/'归档核对记录.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
links=''.join(f'<li><a href="{html.escape(b["批次"])}/index.html">{html.escape(b["批次"])}</a>：{b["人物数"]} 人，{b["分组数"]} 组</li>' for b in batches)
table=''.join('<tr>'+''.join('<td>'+html.escape(str(r[k]))+'</td>' for k in ('姓名','身份','发型','上衣','下装'))+f'<td><a href="{r["批次"]}/{r["组"]}-立绘与模型.jpg">{r["批次"]} · {r["组"]}</a></td></tr>' for r in rows)
(archive/'index.html').write_text('<!doctype html><html lang="zh"><meta charset="utf-8"><title>全体 NPC 手绘三维档案</title><style>body{margin:32px auto;max-width:1500px;padding:20px;background:#f3f2ee;color:#293835;font:16px sans-serif;line-height:1.6}a{color:#306c63}table{border-collapse:collapse;width:100%;font-size:13px}td,th{padding:8px;border-bottom:1px solid #c8cec8;text-align:left}h1{font-size:28px}</style><h1>全体 NPC 手绘三维档案 · 2026-11-27</h1><p>179 位固定角色、15 位额外现场角色已逐人复拍。动态人物另检阅 9 个共享头型、4 条旧枚举入口及临时雇员和顾客。点击批次查看原立绘对照、正侧背、俯角、坐姿与行走序列。</p><ul>'+links+'</ul><p><a href="全员覆盖清单.tsv">完整角色与模型清单</a> · <a href="归档核对记录.json">来源与覆盖记录</a></p><p>身体比例以已确认的三人母版为准；原立绘用于身份和服装对照。没有把覆盖率写成 95% 美术相似度。所有 PNG 为 Unity 原始渲染；对照图只排版，动作 GIF 只编码实际帧。</p><table><thead><tr><th>姓名</th><th>身份</th><th>发型</th><th>上衣</th><th>下装</th><th>实机对照</th></tr></thead><tbody>'+table+'</tbody></table></html>',encoding='utf-8')
print(json.dumps(summary,ensure_ascii=False,indent=2))
