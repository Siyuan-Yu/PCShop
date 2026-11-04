using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed class DistrictTask
    {
        public string Id, Title, PersonId, PlaceId, Reply;
        public string[] Requires;
    }

    public sealed partial class GameSession
    {
        private static DistrictTask DTask(string id,string title,string person,string place,string reply,params string[] requires)
            =>new DistrictTask{Id=id,Title=title,PersonId=person,PlaceId=place,Reply=reply,Requires=requires};

        // These are durable action receipts. Merely displaying a task does not record evidence.
        public bool HasDistrictTask(string id)=>_district.TaskEvidence.Contains(id);
        public DistrictTask NearbyDistrictTask => DistrictTasks.FirstOrDefault(t=>t.PersonId==null&&!HasDistrictTask(t.Id)&&DistrictTaskBlocker(t.Id)==null);
        public IEnumerable<DistrictTask> DistrictTasks
        {
            get
            {
                switch(_district.NodeId)
                {
                    case "park.change":
                        yield return DTask("fair.walk","查看服务站入口与卸货通道",null,"life_service","入口窄，若把高展架摆在门边，会遮住后排小店。侧门能卸货，但必须留出通行空间。");
                        yield return DTask("fair.tenant","问袁岚：原先答应的位置意味着什么","life_tenant",null,"袁岚：我带的都是小件，客人要停下来才看得清。换位可以谈，但不能到场才告诉我，也别拿一个看不见的角落顶替原来的位置。");
                        yield return DTask("fair.curator","问任帆：大展位为什么要挪动","life_curator",null,"任帆：他们的展架比申报的大。不是小店不重要，是原来的图纸没算进去。我们可以调整展架，或者安排分时展示。");
                        yield return DTask("fair.agreement","与袁岚确认最终布置","life_tenant",null,DistrictChoice("park.change")=="consent"?
                            "袁岚：我同意换到侧边，入口加一张共同导览，轮到我的展示时段要有人领客。我会按这个安排准备。":"袁岚：原位置保留，卸货改走侧门，大展架缩到线内。这样小店也有被看见的机会。","fair.walk","fair.tenant","fair.curator");break;
                    case "park.delivery":
                        yield return DTask("fair.signs","现场核对导览与展示动线",null,"life_service",DistrictChoice("park.change")=="consent"?"你把袁岚的摊位写进入口导览，核对分时展示安排；侧门的卸货通道已经留出来。":"你按协商图核对展架边界，保留原摊位，确认侧门仍能通行。");
                        yield return DTask("fair.handover","把现场交接说清楚","life_curator",null,"任帆：开场由我接待，袁岚负责展示，你帮忙照看入口。遇到新变动先叫大家一起确认，别各改各的。","fair.signs");break;
                    case "model.clues":
                        if(DistrictChoice("model.clues")=="supplier")
                        {
                            yield return DTask("model.batch","向周岑核对批次","life_model_supplier",null,"周岑：量产第二批删掉了小凳子，包装看着几乎一样。初版展样底座写着C-01，别只凭盒子判断。");
                            yield return DTask("model.catalogue","检查市场留存的旧展册",null,"model_market","展册里出现C-01：窗凳、旧站牌、手写底座。制作协助栏写着程弈，但没有零售货号。","model.batch");
                            yield return DTask("model.archive","请任帆核对展样去向","life_curator",null,"任帆：那件样品撤展后还给程弈了，搬运时底座磕裂过。你找到他以后，先问愿不愿意转让，别替他答应。","model.catalogue");
                        }
                        else
                        {
                            yield return DTask("model.introduction","向袁岚询问旧展的作者","life_tenant",null,"袁岚：陆栀帮我做过旧街展示，那个小凳子不是批发件。你拿孩子画的细节问她，比说最贵的那款有用。");
                            yield return DTask("model.drawing","和陆栀对照孩子的描述","life_creator",null,"陆栀：没错，初稿有窗凳和站牌，后来为了量产删了。程弈参与做展样，先去市场的旧展册核对底座编号。","model.introduction");
                            yield return DTask("model.catalogue","检查市场留存的旧展册",null,"model_market","旧展册把手绘细节与C-01对应起来：并非普通再版，制作协助人是程弈。你现在有了具体的版本依据。","model.drawing");
                        }break;
                    case "model.repair":
                        if(!_district.RepairPaid)
                        {
                            yield return DTask("model.inspect","现场检查编号、窗凳和裂纹",null,"life_market","你对照旧展册，底座C-01、窗凳、站牌都一致。底座有旧裂纹，原件仍在；重新喷涂会盖掉部分手写署名。");
                            yield return DTask("model.maker","听程弈说明修复取舍","life_maker",null,"程弈：结构能加固，裂纹可以淡化，但没必要假装从未修过。转让1500，修复300，两小时后交件，作者署名保留。","model.inspect");
                            yield return DTask("model.permission","先让沈远确认原状和费用","life_enterprise",null,DistrictChoice("model.repair")=="disclose"?"沈远看过原状照片：我和孩子说清楚了。他想保留那道细纹。就按加固、保留原貌来做，1800元交付时报销。":"沈远：修复记录一起给我，别做成号称全新未拆。1800元我确认承担，按约修好再交付。","model.maker");
                        }
                        else yield return DTask("model.receipt","修好后现场核对实物与修复单",null,"life_market","你核对了底座加固、保留的署名、窗凳和站牌；修复单清楚注明原先的裂纹。实物和来源记录能一起交给沈远。");
                        break;
                    case "model.delivery":
                        yield return DTask("model.child","让沈晓认一认窗边的小凳子",CityLifeCatalog.ShenXiao,null,"沈晓：我记得这个凳子！原来不是我画错了。裂纹留着也没关系，它像真的走过很远的路。");
                        yield return DTask("model.explain","向沈远交代来源和修复记录","life_enterprise",null,"沈远：版本、转让、修复都讲清楚了。谢谢你没有拿相似的新款糊弄过去。我们按说好的实报实销。","model.child");break;
                    case "garden.proposal":
                        yield return DTask("garden.risk","听袁岚算一遍能承担的成本","life_tenant",null,"袁岚：原店的租金和员工工资不能停。我能拿出时间试做，不能把全家的周转金押进去。你先别替我答应长期合同。");
                        yield return DTask("garden.standard","问梁敬：为什么要求长期保证","story_liangjing",null,"梁敬：撤店会让整条街承担空铺和退单。我需要可追溯的负责人，但不一定非要一次押住所有资金。你可以拿具体的试点方案来谈。");
                        yield return DTask("garden.site","到已选试点店核对实际经营条件",null,_district.ProjectShopId,"你确认这是一家已经布置的公园门店。试点沿用它的真实商品和经营，不假设袁岚已经搬入花园，也不把一份意向当作开业。","garden.risk","garden.standard");break;
                    case "garden.terms":
                        string choice=DistrictChoice("garden.terms");
                        if(choice=="respect")
                        {
                            yield return DTask("garden.release","向袁岚确认不参加也不影响关系","life_tenant",null,"袁岚：我先守好原店。你没有因为我退出就说我没眼光，我很感谢。别把我的名字留在担保人那一栏。");
                            yield return DTask("garden.partner","听宋棉自己确认合作意愿","life_clothier",null,"宋棉：我愿意参加这次试点，但只承担约定的展示与经营协作。原店继续营业，新项目不能擅自替我借钱。","garden.release");
                        }
                        else yield return DTask("garden.partner","和袁岚确认能接受的边界","life_tenant",null,choice=="limited"?"袁岚：5万元是你这次支持的上限，别变成以后每笔亏损都由你兜底。我负责约定的协作，不签无限担保。":"袁岚：先试一天、一起看实际记录，再决定续不续。我愿意做这一步，但不预签一整年的承诺。");
                        yield return DTask("garden.exit","与梁敬写清费用、退出和责任上限","story_liangjing",null,choice=="limited"?"梁敬：支持限额5万元，追加投入另行确认；已发生费用据实结算，不自动扩大担保。":"梁敬：本次只约定试点，退出需交接未结事项；不自动续成长租。原经营者不同意的责任不写进合同。","garden.partner");break;
                    case "garden.fulfilment":
                        string partner=_district.ProjectPartnerId??"life_tenant";
                        yield return DTask("garden.problem","听合作方说开业后的交接问题",partner,null,"合作方：客人问后续售后找谁，原店和试点的承诺说法不一致。生意做出一单还不够，这件事要交代清楚。");
                        yield return DTask("garden.followup","回试点店检查成交记录与责任说明",null,_district.ProjectShopId,"你按真实成交记录核对试点门店，把该店负责的销售和售后与合作方的协作分开。没有把借款或注资当营业成绩。","garden.problem");
                        yield return DTask("garden.resolution","和合作方完成交接复盘",partner,null,DistrictChoice("garden.terms")=="respect"?"宋棉：这份交接只写我的实际工作，袁岚不在合同里，也不承担责任。遇到新情况，我们重新确认。":"袁岚：售后由实际销售的店负责，展示协作由我交接。先把这次做好，再决定是否扩大，不会自动追加你的投入。","garden.followup");break;
                }
            }
        }
        public string DistrictTasksBlocker => DistrictTasks.FirstOrDefault(item=>!HasDistrictTask(item.Id)) is DistrictTask task?"还需完成："+task.Title:null;
        public string DistrictTaskBlocker(string taskId,bool remote=false)
        {
            var t=DistrictTasks.FirstOrDefault(x=>x.Id==taskId);
            if(t==null)return "这项调查不属于当前任务。";
            if(HasDistrictTask(t.Id))return null;
            if(_district.MeetingNodeId!=_district.NodeId)return "先确认本次约见和处理方案，再开展调查。";
            if(IsGameOver||IsWorking||CityActivityExclusive||_sleeping)return "请先结束当前工作或活动。";
            if(t.Requires.Any(id=>!HasDistrictTask(id)))return "先完成前面的线索或沟通。";
            if(t.PersonId!=null)return !remote&&CitySamePlace(t.PersonId)?null:"请找到本人，当面交谈。";
            if(t.PlaceId==null||!City.HasPlace(t.PlaceId))return "请先选择实际试点门店。";
            var p=City.GetPlace(t.PlaceId);
            if(Trip!=null||!(CurrentPlace==p||CurrentPlace==null&&DistanceCity(PlayerPosition,StandOf(p))<=5))return "请亲自前往"+p.Name+"进行现场核对。";
            if(DistrictAccessBlocker(p)!=null)return DistrictAccessBlocker(p);
            if(t.Id=="model.receipt"&&Clock.Now.TotalMinutes<_district.RepairReadyAt)return "还在修复中，请在约定工期结束后检查。";
            if(t.Id=="garden.site"||t.Id=="garden.followup")
            {
                var shop=ShopAt(t.PlaceId);
                if(shop==null||shop.Sim.NotFitted||DistrictTierOf(shop.Place)!=2)return "约定店铺必须仍是已布置的公园自营店。";
                if(t.Id=="garden.followup"&&!DistrictProjectHasSales())return "先等试点店产生签约后的真实营业成交，再核对记录。";
            }
            return null;
        }
        public string PerformDistrictTask(string taskId,bool remote=false)
        {
            string why=DistrictTaskBlocker(taskId,remote);if(why!=null)return why;
            var task=DistrictTasks.First(t=>t.Id==taskId);
            if(!HasDistrictTask(taskId))
            {
                _district.TaskEvidence=_district.TaskEvidence.Concat(new[]{taskId}).ToArray();
                if(task.PersonId!=null)RememberSocial(task.PersonId,task.Title+"："+task.Reply);
                Say("主线记录："+task.Title);
            }
            string reply=task.Reply;
            if(DistrictTasksBlocker==null&&(_district.NodeId=="park.change"||_district.NodeId=="model.clues"||_district.NodeId=="garden.proposal"))
            {
                CompleteDistrictNode();
                reply+="\n\n下一步："+(CurrentDistrictNode?.Title??"本段主线已完成。");
            }
            return reply;
        }
        private bool DistrictProjectHasSales()
        {
            int start=0;int.TryParse(Flags.FirstOrDefault(f=>f.StartsWith("district:project:sales-start:"))?.Substring("district:project:sales-start:".Length),out start);
            return _district.ProjectStartedAt>0&&Ledger.Entries.Skip(start).Any(e=>e.ShopId==_district.ProjectShopId&&e.Amount.IsPositive
                &&(e.Category==PCShopSell.Core.Shop.ShopSimulation.SalesCategory||e.Category==PCShopSell.Core.Shop.ShopSimulation.BuildCategory||e.Category=="餐饮"));
        }
    }
}
