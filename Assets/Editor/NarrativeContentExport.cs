using System;
using System.IO;
using System.Linq;
using System.Text;
using PCShopSell.Core.Session;
using UnityEditor;
using UnityEngine;

public static class NarrativeContentExport
{
    [MenuItem("PCShop/Export NPC and narrative catalogue")]
    public static void Export()
    {
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../docs"));
        Directory.CreateDirectory(root);
        var b=new StringBuilder();
        b.AppendLine("# 城市人物完整档案（120人）\n\n版本："+DistrictNarrativeCatalog.ContentVersion+"。由游戏实际配置导出。\n\n人物身份固定，普通城市居民使用已撰写的种子档案，重进场景不会重抽姓名和关系。120人不含验收人物小游、不含后续动态招聘产生的额外员工。20名关键人物、43名既有配角及57名新增居民。每人三段个人经历，随交情和交谈天数逐步听取；这里是制作档案，含未发现的信息。\n\n常驻人物有工作/学习地点、住宅和闲暇地点。工作地点表示日常活动位置，不自动赋予该建筑所有权或凭空新增可经营业态。私人人物关系仍通过游戏中的交谈逐步认识。\n");
        foreach(var p in CityLifeCatalog.People.Where(p=>p.Id!=CityLifeCatalog.InvitationTestPersonId))
        {
            b.AppendLine($"## {p.Name} · {p.Role}\n\n- ID：`{p.Id}`；{p.Age}岁；{(p.KeyCharacter?"关键人物":"城市居民")}；{p.Circle}\n- 住处：`{p.HomeId}`；工作/学习：`{p.WorkId}`\n- 日常时段：{p.WorkStart/60:00}:{p.WorkStart%60:00}—{p.WorkEnd/60:00}:{p.WorkEnd%60:00}；跨午夜时结束时间属于次日\n- 闲暇地点：{string.Join("、",p.LeisurePlaces??Array.Empty<string>())}\n- 初识：{p.FirstEncounter}\n- 兴趣：{p.Interest}\n- 想做的事：{p.Goal}\n\n**{p.StoryTitle}**\n");
            if(NpcNames.Alias(p.Id) is string alias)b.AppendLine("熟人称呼／旧显示名："+alias+"；正式姓名："+p.Name+"。\n");
            for(int i=0;i<p.Stories.Length;i++)b.AppendLine($"{i+1}. {p.Stories[i]}\n");
            b.AppendLine("日常话题：\n");foreach(var topic in p.Topics)b.AppendLine("- "+topic);
            b.AppendLine("\n关系：\n");
            foreach(var tie in CityLifeCatalog.Relations.Where(e=>e.From==p.Id&&e.To!=CityLifeCatalog.InvitationTestPersonId).GroupBy(e=>e.To))
                b.AppendLine("- "+(CityLifeCatalog.Person(tie.Key)?.Name??tie.Key)+"（`"+tie.Key+"`）："+string.Join("；",tie.Select(e=>e.Kind).Distinct()));
            b.AppendLine();
        }
        File.WriteAllText(Path.Combine(root,"47-city-npc-dossiers.md"),b.ToString(),new UTF8Encoding(false));
        b.Clear();b.AppendLine("# 成长主线与关键支线节点配置\n\n版本："+DistrictNarrativeCatalog.ContentVersion+"\n\n"+DistrictNarrativeCatalog.IntegrationStatus+"\n\n这里记录内容配置，不代表所有节点已经能在游戏中完整触发。所有行业共用主线，行业教学另行处理。接入任务执行器时，应从真实交易、赴约、物品交付与经营结算生成凭据；查看配置、点开对话或单纯加好感不得伪造履约。\n\n## 当前阶段分配\n");
        foreach(var c in DistrictNarrativeCatalog.CreatePlan())
        {b.AppendLine("### "+c.Title+"\n\n阶段ID：`"+c.Id+"`；模块："+string.Join("、",c.Modules)+"\n");foreach(var g in c.Gates)b.AppendLine("- "+g.Description+"（`"+g.Fact+"` / `"+g.Scope+"`）");b.AppendLine("\n完成后："+c.Reward+"\n");}
        b.AppendLine("## 正式主线规则\n\n每阶段只保留一条路线：三页寻图、品牌展会与订单交付、企业竞标与三期履约。未达门槛不会启动其他替代剧情。\n\n## 主线节点\n");
        foreach(var n in DistrictNarrativeCatalog.Nodes)
        {
            b.AppendLine($"### {n.Title}\n\n- ID：`{n.Id}`；模块：`{n.Module}`\n- 前置节点：{string.Join("、",n.Prerequisites)}\n- 地点：`{n.PlaceId}`\n- 人物：{string.Join("、",n.People.Select(id=>CityLifeCatalog.Person(id)?.Name??id))}\n- 出现条件：{n.Entry}\n- 玩家目标：{n.Objective}\n\n{n.Dialogue}\n");
            foreach(var choice in n.Choices)b.AppendLine($"- **{choice.Label}**：{choice.Consequence}（分支 `{choice.Id}`；凭据 `{choice.Evidence}`）");
            b.AppendLine($"\n推进凭据：`{n.Evidence}`\n\n结果：{n.Result}\n\n再尝试：{n.Retry}\n");
        }
        b.AppendLine("## 20条关键人物支线\n\n下列为起因、冲突、结局方向的内容配置，尚非完整可执行支线。与主线重复的事实应共享记录，不重复结算。\n");
        foreach(var a in DistrictNarrativeCatalog.SideStories)b.AppendLine($"### {a.Title} · {CityLifeCatalog.Person(a.PersonId)?.Name}\n\nID：`{a.Id}`\n\n起因：{a.Premise}\n\n冲突：{a.Conflict}\n\n收束：{a.Resolution}\n\n相关人物：{string.Join("、",a.RelatedPeople.Select(id=>CityLifeCatalog.Person(id)?.Name??id))}\n");
        File.WriteAllText(Path.Combine(root,"48-district-narrative-nodes.md"),b.ToString(),new UTF8Encoding(false));
        Debug.Log("Narrative dossiers and nodes exported to "+root);
    }
}
