using System;
using System.Linq;
using PCShopSell.Core.Session;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private int _lanternTier,_lanternStyle;
        private int _neighborhoodTab;
        public void OpenNeighborhoodPanel(int tab){_neighborhoodTab=tab;OpenLifeEventPanel(6);}
        private void DrawNeighborhood(Window w)
        {
            var sections=Ui.Row(w.Content,40,6);
            for(int i=0;i<4;i++){int tab=i;Ui.Size(Ui.Button(sections,new[]{"献血车","找家人","漏水维修","花灯会"}[i],()=>{_neighborhoodTab=tab;w.Rebuild();},17,_neighborhoodTab==i?Ui.ButtonHot:Ui.ButtonBg),-1,38,1);}
            void Action(string info,string label,Action run,bool enabled=true)
            {var row=Ui.Row(w.Content,44,8);Ui.Size(Ui.Label(row,info,17),-1,42,1);Ui.Size(Ui.Button(row,label,run,17,Ui.ButtonBg,enabled),140,38);}
            void Go(string place){CloseAllWindows();_ctrl.GoToPlace(S.City.GetPlace(place));}
            void Start(string kind)
            {
                var t=S.LifeTasks.FirstOrDefault(x=>x.Kind==kind);
                if(t!=null&&S.StartLifeTask(t.Id)){CloseWindowSilently(w);S.Time.SetPaused(false);}else {Toast("请到现场，等待对应人物及开放时段。");w.Rebuild();}
            }
            var blood=S.BloodDonation;
            if(_neighborhoodTab==0&&blood!=null)
            {
                Line(w.Content,"献血车",20,Ui.Accent);Line(w.Content,blood.Message,17);
                if(blood.Stage is "invited" or "retry")Action("09:00–17:00 · 检查10分钟","登记检查",()=>{S.BeginBloodCheck();Start("blood-check");});
                foreach(var t in S.LifeTasks.Where(t=>t.Kind.StartsWith("blood-")))
                    Action((t.Kind=="blood-check"?"检查":t.Kind=="blood-donate"?"献血":"留观")+" · 剩"+t.Remaining+"分钟",t.Started?"收起继续":"继续",()=>{if(t.Started){CloseWindowSilently(w);S.Time.SetPaused(false);}else Start(t.Kind);});
                if(blood.Stage!="done")Action("社区服务站前","前往献血车",()=>Go(GameSession.LifeServicePlace));
            }
            if(_neighborhoodTab==0&&S.Backpack.GiftCount(NeighborhoodGoods.Nutrition.Id)>0)Action("营养包 ×"+S.Backpack.GiftCount(NeighborhoodGoods.Nutrition.Id),"食用一份",()=>{S.EatNeighborhoodNutrition();w.Rebuild();});
            var child=S.LostChild;
            if(_neighborhoodTab==1&&child?.Stage=="done")Line(w.Content,child.Message,18);
            if(_neighborhoodTab==1&&child!=null&&child.Stage!="done")
            {
                Line(w.Content,"帮孩子找家人",20,Ui.Accent);Line(w.Content,child.Message,17);
                if(child.Stage!="following")Action("孩子在"+S.City.GetPlace(child.Place).Name,"前往",()=>Go(child.Place));
                if(!child.Asked)Action("到孩子身边问清线索","询问孩子",()=>{if(!S.AskLostChild())Toast("请先到孩子身边。");w.Rebuild();});
                if(child.Asked&&child.Helper==null)
                {
                    Action("陪孩子去"+S.City.GetPlace(child.CluePlace).Name,"按线索找家人",()=>{if(S.EscortLostChild())Go(child.CluePlace);else Toast("先到孩子身边再陪同。");});
                    Action("当面确认后交接5分钟","与家人确认",()=>{if(S.ReuniteLostChild())Start("child-reunion");else {Toast("需先询问，并陪孩子到实际家人身边。");w.Rebuild();}});
                }
                if(child.Helper==null)
                {
                    Action("陪孩子前往服务站","去服务站",()=>{if(S.EscortLostChild())Go(GameSession.LifeServicePlace);else Toast("请先到孩子身边。");});
                    Action(child.Stage=="following"?"到服务站后联系值守员":"工作人员实际赶到孩子身边","联系工作人员",()=>{S.ContactChildHelper();w.Rebuild();});
                }
                else Action(child.Helper.Route!=null?"工作人员正在路上":"工作人员已到场，陪同交接15分钟","开始交接",()=>Start("child-handoff"));
            }
            var leak=S.RoofLeak;
            if(_neighborhoodTab==2&&leak?.Fixed==true)Line(w.Content,leak.Message,18);
            if(_neighborhoodTab==2&&leak!=null&&!leak.Fixed)
            {
                Line(w.Content,S.City.GetPlace(leak.Shop).Name+" · 漏水",20,Ui.Accent);Line(w.Content,leak.Message+(leak.Water>=3?" 积水使到店客流减少20%。":""),17);
                Action("到原门店处理","前往",()=>Go(leak.Shop));
                if(!leak.Bucket)Action("5分钟放桶，临时接水","放接水桶",()=>{S.PlaceLeakBucket();Start("leak-bucket");});
                if(leak.Worker==null)Action("¥2,000 · 师傅到店后修60分钟","请师傅维修",()=>{S.RequestLeakRepair();w.Rebuild();});
                else Line(w.Content,leak.Worker.Route!=null?"师傅正在路上":"师傅已到店，维修进度见待办；可离店继续经营。",17);
            }
            var festival=S.LanternFestival;
            if(_neighborhoodTab==3&&festival!=null)
            {
                Line(w.Content,"社区花灯会 · 三晚18:00–22:00",20,Ui.Accent);Line(w.Content,festival.Message,17);
                if(festival.Active)
                {
                    Action(S.FestivalOpen?"正在举办":"尚未开场，时间到后再来","前往花灯会",()=>Go(GameSession.LifeServicePlace));
                    var row=Ui.Row(w.Content,40,6);
                    for(int tier=0;tier<2;tier++){int n=tier;Ui.Size(Ui.Button(row,tier==0?"纸艺 ¥300":"精制 ¥1,000",()=>{_lanternTier=n;w.Rebuild();},17,_lanternTier==tier?Ui.ButtonHot:Ui.ButtonBg),-1,38,1);}
                    var shapes=Ui.Row(w.Content,40,6);for(int i=0;i<3;i++){int style=i;Ui.Size(Ui.Button(shapes,new[]{"圆月","莲花","宫灯"}[i],()=>{_lanternStyle=style;w.Rebuild();},17,_lanternStyle==i?Ui.ButtonHot:Ui.ButtonBg),-1,38,1);}
                    Action("现场制作10分钟，开始时付款","开始做灯",()=>{if(S.BeginLanternCraft(_lanternTier,_lanternStyle))Start("lantern-craft");else Toast("请在花灯会开放时到现场，先完成已有制作。");});
                    foreach(var light in NeighborhoodGoods.Lanterns.Where(x=>S.Backpack.GiftCount(x.Id)>0))
                    {
                        var item=light;var slots=Ui.Row(w.Content,40,6);Ui.Size(Ui.Label(slots,item.Name+" ×"+S.Backpack.GiftCount(item.Id),17),-1,38,1);
                        for(int i=0;i<3;i++){int slot=i;Ui.Size(Ui.Button(slots,"挂到"+(i+1)+"号位",()=>{S.HangFestivalLantern(item.Id,slot);w.Rebuild();},16,Ui.ButtonBg,festival.Hung[i]<0),125,38);}
                    }
                    if(festival.PendingRiddle>=0)
                    {
                        int i=festival.PendingRiddle;Line(w.Content,GameSession.FestivalRiddles[i],18,Ui.Accent);
                        for(int j=0;j<3;j++){int answer=j;Action(GameSession.FestivalAnswers[i][j],"选这个",()=>{S.AnswerFestivalRiddle(answer);w.Rebuild();});}
                    }
                    else for(int i=0;i<3;i++){int index=i;if((festival.RiddleMask&(1<<i))==0)Action("第"+(i+1)+"盏灯谜 · 观察5分钟","看看灯谜",()=>{if(S.BeginFestivalRiddle(index))Start("lantern-riddle");else Toast("请在花灯会现场，完成当前灯谜后再选。");});}
                    Action("沿小摊逛10分钟，可选购挂灯制作","逛逛小摊",()=>{if(S.BrowseFestivalStall())Start("lantern-browse");else Toast("请在开放时到场，本次同行或独自游览只结算一次。");});
                    foreach(var p in S.CityPeople.Where(p=>S.IsMet(p.PersonId)).OrderByDescending(p=>S.Relation(p.PersonId)).Take(5))
                    {string person=p.PersonId;Action("约"+S.CityPerson(person)?.Name+"一起逛60分钟","邀请同行",()=>{if(!S.InviteFestivalFriend(person))Toast("已有邀约、本次已一起逛过或暂无共同空闲晚间。");w.Rebuild();});}
                }
            }
            if(blood==null&&child==null&&leak==null&&festival==null)Line(w.Content,"遇到街坊事件后，可在这里查看现场和进度。",18);
        }
    }
}
