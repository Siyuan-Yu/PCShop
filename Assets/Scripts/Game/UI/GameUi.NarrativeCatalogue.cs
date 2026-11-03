#if UNITY_EDITOR
using System.Linq;
using PCShopSell.Core.Session;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void OpenNarrativeCatalogue()
        {
            int tab=0,page=0;
            string query="";
            OpenWindow("人物与剧情配置 · 只读预览",1060,790,w=>
            {
                var tabs=Ui.Row(w.Content,42,6);
                string[] names={"人物 120","成长节点","关键支线","阶段条件"};
                for(int i=0;i<names.Length;i++)
                {
                    int t=i;Ui.Size(Ui.Button(tabs,names[i],()=>{tab=t;page=0;w.Rebuild();},18,tab==i?Ui.ButtonHot:Ui.ButtonBg),-1,42,1);
                }
                Para(w.Content,"此处包含尚未遇见的人物和剧情资料，只用于检查配置。查看不会认识人物、推进任务或改变存档。",15,Ui.Dim);
                if(tab==0)
                {
                    Ui.Size(Ui.TextInput(w.Content,query,"搜索姓名、身份或生活圈",value=>{query=value??"";page=0;w.RequestRebuild();}),-1,40);
                    var people=CityLifeCatalog.People.Where(p=>p.Id!=CityLifeCatalog.InvitationTestPersonId&& (p.Name+" "+p.Role+" "+p.Circle).Contains(query)).ToArray();
                    if(people.Length==0){Para(w.Content,"没有匹配人物。",18);return;}
                    page=System.Math.Min(page,people.Length-1);var p=people[page];
                    var pager=Ui.Row(w.Content,38,6);
                    Ui.Size(Ui.Button(pager,"上一位",()=>{page--;w.Rebuild();},17,Ui.ButtonBg,page>0),120,38);
                    Line(pager,$"{page+1}/{people.Length} · {(p.KeyCharacter?"关键人物":"城市居民")}",18);
                    Ui.Size(Ui.Button(pager,"下一位",()=>{page++;w.Rebuild();},17,Ui.ButtonBg,page+1<people.Length),120,38);
                    Line(w.Content,$"{p.Name} · {p.Age}岁 · {p.Role}",23,Ui.Accent);
                    Para(w.Content,p.Circle+" · "+p.StoryTitle+"\n"+p.FirstEncounter,17);
                    string Place(string id)=>id!=null&&S.City.HasPlace(id)?S.City.GetPlace(id).Name:id??"无";
                    Para(w.Content,$"住处：{Place(p.HomeId)}　工作/学习：{Place(p.WorkId)}\n{p.WorkStart/60:00}:{p.WorkStart%60:00}—{p.WorkEnd/60:00}:{p.WorkEnd%60:00}　兴趣：{p.Interest}\n目标：{p.Goal}",16,Ui.Dim);
                    foreach(var story in p.Stories)Para(w.Content,story,17);
                    var ties=CityLifeCatalog.Relations.Where(e=>e.From==p.Id&&e.To!=CityLifeCatalog.InvitationTestPersonId).Select(e=>(CityLifeCatalog.Person(e.To)?.Name??e.To)+"："+e.Kind).Distinct();
                    Para(w.Content,"关系\n"+string.Join("\n",ties),16,Ui.Good);
                }
                else if(tab==1)
                {
                    Para(w.Content,DistrictNarrativeCatalog.IntegrationStatus,16,Ui.Accent);
                    foreach(var n in DistrictNarrativeCatalog.Nodes)
                        Ui.Size(Ui.Button(w.Content,n.Title,()=>OpenNarrativeNode(n),18),-1,42);
                }
                else if(tab==2)
                {
                    Para(w.Content,"以下为支线的起因、冲突与收束配置；具体事件执行尚未接入。人物三段经历已经可以在正常聊天中听取。",16,Ui.Accent);
                    foreach(var arc in DistrictNarrativeCatalog.SideStories)
                    {
                        var a=arc;
                        Ui.Size(Ui.Button(w.Content,CityLifeCatalog.Person(a.PersonId).Name+" · "+a.Title,()=>OpenWindow(a.Title,800,600,v=>
                        {Para(v.Content,a.Premise,20);Para(v.Content,a.Conflict,19,Ui.Accent);Para(v.Content,a.Resolution,19);Para(v.Content,"相关人物："+string.Join("、",a.RelatedPeople.Select(id=>CityLifeCatalog.Person(id)?.Name??id)),17,Ui.Dim);}),18),-1,42);
                    }
                }
                else foreach(var chapter in DistrictNarrativeCatalog.CreatePlan())
                {
                    Line(w.Content,chapter.Title,23,Ui.Accent);
                    foreach(var gate in chapter.Gates)Para(w.Content,"• "+gate.Description,17);
                    Para(w.Content,chapter.Reward,17,Ui.Good);
                }
            });
        }
        private void OpenNarrativeNode(DistrictNarrativeNode n)=>OpenWindow(n.Title,900,740,w=>
        {
            Para(w.Content,"地点："+S.City.GetPlace(n.PlaceId).Name+"　人物："+string.Join("、",n.People.Select(id=>CityLifeCatalog.Person(id)?.Name??id)),18,Ui.Accent);
            Para(w.Content,"出现条件："+n.Entry+"\n目标："+n.Objective,18);
            Para(w.Content,n.Dialogue,19,Ui.Good);
            foreach(var c in n.Choices)Para(w.Content,c.Label+"\n"+c.Consequence,17);
            Para(w.Content,"推进凭据："+n.Evidence+"\n结果："+n.Result+"\n再次尝试："+n.Retry,16,Ui.Dim);
        });
    }
}
#endif
