using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private int _lifeEventTab;
        public void OpenLifeEventPanel(int tab){_lifeEventTab=tab;OpenLifeEventPanel();}
        public void OpenLifeEventPanel()
        {
            OpenWindow("社区生活",900,780,w=>
            {
                var tabs=Ui.Row(w.Content,40,6);
                for(int i=0;i<7;i++){int tab=i;Ui.Size(Ui.Button(tabs,new[]{"待办","小狗","图书","夜校","集市","健身","街坊"}[i],()=>{_lifeEventTab=tab;w.Rebuild();},17,_lifeEventTab==i?Ui.ButtonHot:Ui.ButtonBg),-1,38,1);}
                if(_lifeEventTab==6)DrawNeighborhood(w);
                void Action(string info,string label,System.Action run,bool enabled=true)
                {var row=Ui.Row(w.Content,42,8);Ui.Size(Ui.Label(row,info,17),-1,40,1);Ui.Size(Ui.Button(row,label,run,17,Ui.ButtonBg,enabled),140,38);}
                foreach(var task in S.LifeTasks.Where(t=>_lifeEventTab==0))
                {
                    var t=task;string name=t.Kind switch{"blood-check"=>"献血检查","blood-donate"=>"献血","blood-observe"=>"献血留观","child-handoff"=>"孩子交接","child-reunion"=>"家人确认","leak-repair"=>"屋顶维修","leak-bucket"=>"放接水桶","lantern-craft"=>"制作花灯","lantern-riddle"=>"观察灯谜","lantern-browse"=>"逛花灯会","festival-visit"=>"花灯会同行","donation"=>"募书登记","disinfect"=>"到店消毒","rest"=>"在家休息","read"=>"阅读","chat"=>"手机聊天","gym"=>"健身训练","adopt"=>"收养登记","dog-walk"=>"遛狗","book-feedback"=>"募书反馈","lesson"=>"夜校课堂",_=>"朋友邀约"};
                    string place=t.Place!=null&&S.City.HasPlace(t.Place)?S.City.GetPlace(t.Place).Name:"随身进行";
                    Line(w.Content,name+" · "+place+" · "+(t.Activity!=null?"见活动日程":t.Kind=="book-feedback"?new GameTimestamp(t.At).ToString():"还需"+t.Remaining+"分钟"),18,Ui.Accent);
                    if(t.Place!=null&&S.City.HasPlace(t.Place)&&S.CurrentPlace?.Id!=t.Place)
                        Action("到场后继续","前往",()=>{CloseAllWindows();_ctrl.GoToPlace(S.City.GetPlace(t.Place));});
                    if(t.Activity!=null)Action("需与人物实际同场","查看预约",()=>OpenCityActivityDetail(t.Activity));
                    else if(t.Kind=="leak-repair")Line(w.Content,"师傅到场后自动施工，可离店；无需按开始。",17);
                    else if(t.Kind!="book-feedback")
                        Action(t.Fee>0?"开始时支付¥"+t.Fee:t.Kind=="dog-walk"?"需在街上实际走动20米以上":"实际时间推进，离开暂停",t.Started?"暂停":"开始",()=>
                        {if(t.Started){S.PauseLifeTask(t.Id);w.Rebuild();}else if(S.StartLifeTask(t.Id)){CloseWindowSilently(w);S.Time.SetPaused(false);}});
                }
                if(_lifeEventTab==0&&S.LifeDonationTotal>0)Line(w.Content,"募书累计登记¥"+S.LifeDonationTotal+" · 购书余款¥"+S.LifeLibraryUnspent,17);
                if(_lifeEventTab==0&&S.FluIllUntil>S.Clock.Now.TotalMinutes)
                {
                    Action("流感症状，对症药¥20，每天一次","吃药",()=>{S.TakeLifeFluMedicine();w.Rebuild();});
                    if(S.NearbyHospital()!=null)Action("到医院实际看诊","前往医院",()=>{CloseAllWindows();_ctrl.GoToPlace(S.NearbyHospital());});
                }
                if(_lifeEventTab==0&&S.FluProtectionUntil>S.Clock.Now.TotalMinutes)Line(w.Content,"店内防护至 "+new GameTimestamp(S.FluProtectionUntil),17);
                var dog=S.LifeDog;
                if(_lifeEventTab==1&&dog!=null&&dog.Stage!="rescued")
                {
                    Line(w.Content,"小狗 · "+(dog.Stage=="adopted"?"已收养":dog.Stage=="adopting"?"登记安置中":"等待救助")+" · 饱食度"+dog.Hunger+" · 亲近"+dog.Bond,20,Ui.Accent);
                    string site=dog.Stage=="stray"?dog.Place:dog.Stage=="adopting"?GameSession.LifeServicePlace:S.Home.Id;
                    if(S.City.HasPlace(site))Action(S.City.GetPlace(site).Name,"前往",()=>{CloseAllWindows();_ctrl.GoToPlace(S.City.GetPlace(site));});
                    Action("消耗一份实际犬粮，每天一次","喂食",()=>{if(!S.FeedLifeDog())Toast("需在狗旁、带犬粮，今天尚未喂过。");w.Rebuild();});
                    if(dog.Stage=="adopting")
                    {var task=S.LifeTasks.FirstOrDefault(t=>t.Kind=="adopt");if(task!=null)Action("到服务站完成收养登记","办理安置",()=>{if(S.StartLifeTask(task.Id)){CloseWindowSilently(w);S.Time.SetPaused(false);}});}
                    if(dog.Stage=="stray")
                    {Action("基础用品与登记¥800，之后到服务站办理","收养",()=>{S.AdoptLifeDog();w.Rebuild();});Action("与救助者现场交接","联系救助",()=>{S.RescueLifeDog();w.Rebuild();});}
                    if(dog.Stage=="adopted")Action("从家里带出门，实际步行10分钟","遛狗",()=>{if(S.WalkLifeDog()){var task=S.LifeTasks.First(t=>t.Kind=="dog-walk");if(S.StartLifeTask(task.Id)){CloseWindowSilently(w);S.Time.SetPaused(false);}}});
                    Action("服务站购买，¥40/份","买3份犬粮",()=>{if(!S.BuyLifeDogFood(3))Toast("请到营业中的社区服务站购买，并留够现金和载重。");w.Rebuild();});
                }
                if(_lifeEventTab==5&&S.LifeGymReady)
                {
                    Line(w.Content,"健身场 · 已完成"+S.LifeGymRounds+"/3轮",20,Ui.Accent);
                    foreach(var mode in new[]{0,1,2,3}){int m=mode;Action(new[]{"快走10分钟","器械10分钟","拉伸10分钟","休息5分钟"}[m],"选择",()=>{if(S.LifeGymRound(m)){var task=S.LifeTasks.First(t=>t.Kind=="gym");if(S.StartLifeTask(task.Id)){CloseWindowSilently(w);S.Time.SetPaused(false);}}});}
                    Action("公园健身场","前往",()=>{CloseAllWindows();_ctrl.GoToPlace(S.City.GetPlace(GameSession.LifeFitnessPlace));});
                }
                if(_lifeEventTab==2&&S.Flags.Contains("life:library-invited"))
                {
                    Line(w.Content,"社区图书角",20,Ui.Accent);
                    for(int b=0;b<3;b++)
                    {
                        int n=b;var book=LifeEventGoods.Books[b];string old=LifeEventGoods.Books.FirstOrDefault(x=>x.Id!=book.Id&&S.Backpack.GiftCount(x.Id)>0)?.Id;
                        Action(book.Name+" · 架上"+S.LifeBookStock[b]+"本"+(old!=null?" · 交出"+PCShopSell.Core.Home.Gifts.Get(old).Name:""),S.Flags.Contains("life:first-book")?"用随身书交换":"首次领一本",()=>{if(!S.BorrowLifeBook(n,old))Toast("请到服务站与叶书见面，并带书交换。");w.Rebuild();});
                    }
                }
                foreach(var book in LifeEventGoods.Books.Where(b=>_lifeEventTab==2&&S.Backpack.GiftCount(b.Id)>0))
                {string id=book.Id;Action(book.Name+" · 在家阅读30分钟","阅读",()=>{if(S.ReadLifeBook(id)&&S.CurrentPlace==S.Home){var task=S.LifeTasks.First(t=>t.Kind=="read");if(S.StartLifeTask(task.Id)){CloseWindowSilently(w);S.Time.SetPaused(false);return;}}_lifeEventTab=0;w.Rebuild();});}
                if(_lifeEventTab==3&&S.Flags.Contains("life:school-invited"))
                {
                    Line(w.Content,"社区夜校 · 19:00晚课",20,Ui.Accent);var c=S.LifeCourse;
                    if(c==null||c.Lessons>=4)
                        for(int n=0;n<3;n++){int course=n;Action(new[]{"经营记账","采购实务","沟通练习"}[n]+" · 四节¥1,000","报名",()=>{S.EnrollLifeCourse(course,false);w.Rebuild();});
                            if(S.IsMet("life_ning"))Action("与顾宁一起上这门课","同行报名",()=>{S.EnrollLifeCourse(course,true);w.Rebuild();});}
                    else
                    {
                        Line(w.Content,"已上"+c.Lessons+"/4节 · 正确练习"+c.Score+"次",18);
                        bool active=S.CityActivities.Any(a=>a.Id==c.Activity&&a.Active);
                        Action(active?"本节已预约，完成后可约下节":"约下节或补未完成的课，不再交学费","预约下一节",()=>{S.BookLifeLesson();w.Rebuild();},!active);
                        if(c.Activity!=null)
                        {
                            Action(S.LifeLessonQuestion,"查看课堂",()=>OpenCityActivityDetail(c.Activity));
                            var answers=S.LifeLessonAnswers;
                            for(int a=0;a<answers.Length;a++){int answer=a;Action(answers[a],"作答",()=>{if(!S.AnswerLifeLesson(answer))Toast("请在课堂到场30分钟后作答，每节一次。");w.Rebuild();});}
                        }
                    }
                }
                var music=S.LifeTasks.FirstOrDefault(t=>t.Kind=="music");
                if(_lifeEventTab==0&&music!=null)
                {Action("到场听歌后可打赏","打赏¥200",()=>{S.TipLifeMusic(200);w.Rebuild();});Action("自愿支持表演","打赏¥1,000",()=>{S.TipLifeMusic(1000);w.Rebuild();});Action("唱到中段可试合唱，魅力45以下必失败","参与合唱",()=>{S.SingLifeMusic();w.Rebuild();});}
                if(_lifeEventTab==4&&S.Flags.Contains("life:fair-invited"))
                {
                    Line(w.Content,"周末禾田集市",20,Ui.Accent);var stall=S.LifeStall;
                    if(stall!=null&&!stall.Done)
                    {Line(w.Content,stall.Name+" · 余"+stall.Remaining+"件 · 已售"+stall.Sold+"件 · 单价"+Money.FromCents(stall.Price),18);Line(w.Content,stall.Message??"等待顾客",17);Action("四小时租期，到场领回未售物品",stall.Ended?"领回未售":"收摊",()=>{S.EndLifeFair();w.Rebuild();});}
                    else foreach(var item in S.EventIdleItems)
                    {
                        var i=item;var original=i.Furniture?PCShopSell.Core.Home.FurnitureCatalog.Get(i.Id).Price:PCShopSell.Core.Home.Gifts.Get(i.Id).Price;Line(w.Content,i.Name+" ×"+i.Count+" · 寄售1件，摊位费¥200",18);
                        foreach(int p in new[]{100,90,70}){int price=p;Action("单价 "+original.Scale(price/100.0)+"（原价"+p+"%）","摆摊1件",()=>{if(S.StartLifeFair(i.Id,i.Furniture,1,price)){CloseWindowSilently(w);S.Time.SetPaused(false);}else Toast("请在周末17点前到禾田集市，并留足摊位费。");});}
                        if(i.Count>1)Action("全部"+i.Count+"件，原价九折","全部寄售",()=>{if(S.StartLifeFair(i.Id,i.Furniture,i.Count,90)){CloseWindowSilently(w);S.Time.SetPaused(false);}});
                    }
                    Action("禾田集市","前往",()=>{CloseAllWindows();_ctrl.GoToPlace(S.City.GetPlace(GameSession.LifeFairPlace));});
                }
                if(S.RunningCityActivity!=null||S.LifeTasks.Any(t=>t.Started))Action("时间随实际世界推进","收起，继续",()=>{CloseWindowSilently(w);S.Time.SetPaused(false);});
                Action("服务站有图书角、晚课与主粮","前往服务站",()=>{CloseAllWindows();_ctrl.GoToPlace(S.City.GetPlace(GameSession.LifeServicePlace));});
            });
        }
    }
}
