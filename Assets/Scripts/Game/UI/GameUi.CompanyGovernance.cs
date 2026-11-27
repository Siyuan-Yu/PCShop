using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Window _governanceWindow;
        private string _governanceTicker, _governanceInvestor;
        private int _governancePage;
        private bool _governanceBuying = true;
        private string _governanceUnits = "1000";
        private string _governanceUiStamp;
        private void UpdateGovernanceWindows()
        {
            string stamp=string.Join("|",S.GovernanceCompanies.Select(g=>g.Ticker+":"+g.TotalShares+":"+g.Trust+":"+string.Join(",",g.Holders.Select(h=>h.OwnerId+"="+h.Shares))+":"+g.ControllerId+":"+g.ManagerId+":"+g.HandoverAt+":"+g.Resolutions.Count(r=>!r.Closed)));
            if(stamp==_governanceUiStamp)return;_governanceUiStamp=stamp;
            if(_governanceWindow!=null&&!_governanceWindow.Closed)_governanceWindow.RequestRebuild();
            if(_stockWindow!=null&&!_stockWindow.Closed)_stockWindow.RequestRebuild();
            if(_managementWindow!=null&&!_managementWindow.Closed&&_managementTab==2)_managementWindow.RequestRebuild();
            if(_homeWorkbench!=null&&!_homeWorkbench.Closed&&_workbenchTab==0)_homeWorkbench.RequestRebuild();
        }
        private static readonly Color[] ShareColors={new Color(.61f,.43f,.20f),new Color(.35f,.46f,.39f),new Color(.45f,.44f,.54f),new Color(.64f,.35f,.29f),new Color(.54f,.55f,.45f),new Color(.72f,.67f,.57f)};
        private void OpenCompanyGovernance(string ticker=null)
        {
            _governanceTicker=ticker??_governanceTicker??S.GovernanceCompanies.FirstOrDefault()?.Ticker;
            if(_governanceWindow!=null&&!_governanceWindow.Closed){_governanceWindow.Rebuild();return;}
            _governanceWindow=OpenWindow("公司股权与治理",1320,920,w=>BuildGovernanceWorkspace(w.Content,w,_governanceTicker,true),scrollContent:false);
        }
        private void BuildGovernanceWorkspace(Transform parent,Window w,string ticker,bool selector)
        {
            var companies=S.GovernanceCompanies.ToArray();var current=companies.FirstOrDefault(g=>g.Ticker==ticker)??companies.FirstOrDefault();
            if(current==null){Line(parent,"暂无公司",20,Ui.Dim);return;}
            RectTransform body;
            if(selector)
            {
                var split=Ui.Row(parent,-1,12);Ui.Size(split).GetComponent<LayoutElement>().flexibleHeight=1;
                var listShell=CompanyCard(split,"公司",.58f);var list=PhoneFlexibleScroll(listShell,"GovernanceCompanyList");
                foreach(var company in companies)
                {
                    var entry=company;
                    Ui.Size(Ui.Button(list,S.GovernanceName(entry.Ticker),()=>{_governanceTicker=entry.Ticker;_governanceInvestor=null;w.Rebuild();},15,Ui.SelectedButton(entry==current)),-1,36);
                }
                body=Ui.Rect(split,"GovernanceBody");Ui.VList(body,7,0);Ui.Size(body,0,-1,2.42f);
            }
            else{body=Ui.Rect(parent,"GovernanceBody");Ui.VList(body,7,0);Ui.Size(body).GetComponent<LayoutElement>().flexibleHeight=1;}
            if(selector)Line(body,S.GovernanceName(current.Ticker),23,Ui.Accent);
            Line(body,$"持股 {S.ShareholderShares(current,GameSession.PlayerShareholderId):N0} / {current.TotalShares:N0} · {S.ShareholderRatio(current,GameSession.PlayerShareholderId):P2}　信任 {current.Trust}　{(S.CompanyBoardMember(current)?"董事":S.ShareholderShares(current,GameSession.PlayerShareholderId)>0?"股东":"未持股")}",16);
            BuildShareDistribution(body,current);
            var tabs=Ui.Row(body,36,7);string[] names={"股权与股东","董事会","控制权与门店"};
            for(int i=0;i<names.Length;i++){int page=i;Ui.Tab(tabs,names[i],()=>{_governancePage=page;w.Rebuild();},_governancePage==i);}
            if(S.CompanyGovernanceGate!=null)Line(body,S.CompanyGovernanceGate,16,Ui.Warn);
            var area=Ui.Rect(body,"GovernancePage");Ui.VList(area,7,0);Ui.Size(area).GetComponent<LayoutElement>().flexibleHeight=1;
            if(_governancePage==1)BuildGovernanceBoard(area,w,current);
            else if(_governancePage==2)BuildGovernanceControl(area,w,current);
            else BuildGovernanceShares(area,w,current);
        }
        private void BuildShareDistribution(Transform parent,CompanyGovernance company)
        {
            var bar=Ui.Row(parent,16,0);int index=0;
            foreach(var shareholder in company.Holders.Where(h=>h.Shares>0).OrderByDescending(h=>h.Shares))
            {
                var part=Ui.Panel(bar,"Share_"+shareholder.OwnerId,ShareColors[index++%ShareColors.Length]);
                Ui.Size(part,0,16,(float)shareholder.Shares/company.TotalShares);
                part.Tip(S.ShareholderName(shareholder.OwnerId)+" "+S.ShareholderRatio(company,shareholder.OwnerId).ToString("P2"));
            }
        }
        private void BuildGovernanceShares(Transform parent,Window w,CompanyGovernance company)
        {
            var row=Ui.Row(parent,-1,12);Ui.Size(row).GetComponent<LayoutElement>().flexibleHeight=1;
            var rosterShell=CompanyCard(row,"实际股东名册",1);var roster=PhoneFlexibleScroll(rosterShell,"ShareholderRoster");
            var holders=company.Holders.Where(h=>h.Shares>0).OrderByDescending(h=>h.Shares).ToArray();int index=0;
            foreach(var holder in holders)
            {
                string owner=holder.OwnerId;var entry=Ui.Row(roster,62,7);
                var mark=Ui.Panel(entry,"HolderBadge",ShareColors[index++%ShareColors.Length]);Ui.Size(mark,36,36);
                Ui.Label(mark.transform,S.ShareholderName(owner).Substring(0,1),18,TextAnchor.MiddleCenter,Color.white).rectTransform.Fill();
                var info=Ui.Rect(entry,"Holder");Ui.VList(info,2,0);Ui.Size(info,0,62,1);
                bool named=owner!=GameSession.PlayerShareholderId&&owner!=GameSession.PublicShareholderId;
                if(named)Ui.Size(Ui.Button(info,S.ShareholderName(owner),()=>{_governanceInvestor=owner;w.Rebuild();},15,Ui.SelectedButton(_governanceInvestor==owner)),-1,30);
                else Line(info,S.ShareholderName(owner),16);
                Line(info,$"{holder.Shares:N0} 股 · {S.ShareholderRatio(company,owner):P2}",14,Ui.Dim);
            }
            var trade=CompanyCard(row,"股份交易",1.1f);
            Line(trade,"市场可卖 "+S.StockPublicShares(company.Ticker).ToString("N0")+" 股",16,Ui.Dim);
            CompanyButton(trade,"普通买卖 / 行情",()=>{_stockExpanded=company.Ticker;OpenCityInvestments();});
            Line(trade,"大宗交易",19,Ui.Accent);
            var counterpart=S.CompanyInvestors.Where(i=>!i.Id.StartsWith("public:",StringComparison.Ordinal)&&(i.Id.StartsWith("capital_",StringComparison.Ordinal)||S.ShareholderShares(company,i.Id)>0)).ToArray();
            var investor=counterpart.FirstOrDefault(i=>i.Id==_governanceInvestor)??counterpart.FirstOrDefault(i=>S.ShareholderShares(company,i.Id)>0)??counterpart.FirstOrDefault();
            if(investor==null){Line(trade,"暂无具名交易对手",16,Ui.Dim);return;}_governanceInvestor=investor.Id;
            CompanyButton(trade,S.ShareholderName(investor.Id)+" ▾",()=>OpenWindow("选择交易对手",650,610,p=>
            {
                var list=PhoneFlexibleScroll(p.Content,"Counterparties");
                foreach(var person in counterpart){var pick=person;CompanyButton(list,pick.Name+$" · {S.ShareholderShares(company,pick.Id):N0} 股",()=>{_governanceInvestor=pick.Id;p.Close();w.Rebuild();});}
            },scrollContent:false));
            Line(trade,"对手资金 "+Money.FromCents(investor.CashCents),14,Ui.Dim);
            var modes=Ui.Row(trade,34,6);
            Ui.Tab(modes,"受让股份",()=>{_governanceBuying=true;w.Rebuild();},_governanceBuying);
            Ui.Tab(modes,"转让股份",()=>{_governanceBuying=false;w.Rebuild();},!_governanceBuying);
            var quantity=Ui.TextInput(trade,_governanceUnits,"股数",s=>_governanceUnits=s);Ui.Size(quantity,-1,34);quantity.contentType=InputField.ContentType.IntegerNumber;
            var estimate=Ui.Size(Ui.Label(trade,"",15,color:Ui.Dim),-1,54);var issue=Ui.Size(Ui.Label(trade,"",14,color:Ui.Warn),-1,42);
            var execute=Ui.Size(Ui.Button(trade,_governanceBuying?"确认受让…":"确认转让…",()=>
            {
                if(!long.TryParse(quantity.text,out long amount)||amount<=0){Toast("请输入有效股数");return;}
                string why=S.CompanyBlockTradeBlocker(company.Ticker,investor.Id,amount,_governanceBuying);if(why!=null){Toast(why);return;}
                bool buy=_governanceBuying;var gross=S.CompanyBlockPrice(company.Ticker,amount,buy);var fee=CityInvestments.Fee(gross);
                double ratio=(S.ShareholderShares(company,GameSession.PlayerShareholderId)+(buy?amount:-amount))/(double)company.TotalShares;
                ConfirmAction(buy?"受让公司股份":"转让公司股份",$"{S.ShareholderName(investor.Id)} · {amount:N0} 股\n{(buy?"支付":"收到")} {(buy?gross+fee:gross-fee)}（含手续费 {fee}）\n成交后持股 {ratio:P2}。"+(!buy&&ratio<=.5?"你可能失去控制权；公司门店需交接，个人门店保留。":"达到过半表决权后可发起接管。"),buy?"确认受让":"确认转让",()=>{if(S.TradeCompanyBlock(company.Ticker,investor.Id,amount,buy))w.Rebuild();},danger:!buy&&ratio<=.5);
            },16,Ui.ButtonHot),-1,36);
            void Refresh(string text)
            {
                _governanceUnits=text;long.TryParse(text,out long amount);string why=S.CompanyBlockTradeBlocker(company.Ticker,investor.Id,amount,_governanceBuying);
                var gross=amount>0&&amount<=company.TotalShares?S.CompanyBlockPrice(company.Ticker,amount,_governanceBuying):Money.Zero;
                var fee=gross.IsPositive?CityInvestments.Fee(gross):Money.Zero;
                estimate.text=$"{(_governanceBuying?"需支付":"可收到")} {(_governanceBuying?gross+fee:gross-fee)}\n成交后持股 {Math.Max(0,S.ShareholderShares(company,GameSession.PlayerShareholderId)+(_governanceBuying?amount:-amount))/(double)company.TotalShares:P2}";
                issue.text=why??"";execute.interactable=why==null;
            }
            quantity.onValueChanged.AddListener(Refresh);Refresh(quantity.text);
            Line(trade,_governanceBuying?"大宗受让按现价溢价 10% 报价。":"大宗转让按现价 95% 报价。",13,Ui.Dim);
        }
        private void BuildGovernanceBoard(Transform parent,Window w,CompanyGovernance company)
        {
            var top=CompanyCard(parent,"会议与权限");
            Line(top,S.CompanyBoardMember(company)?"已拥有董事权限":$"参会 ≥1% · 董事提名 ≥5% 且信任 ≥60 · 当前信任 {company.Trust}",16);
            if(!S.CompanyBoardMember(company))CompanyButton(top,"申请董事席位…",()=>ConfirmAction("董事提名","按表决结束时的真实持股计票，支持票超过总股本 50% 才能获得席位。","提交提名",()=>{if(S.ProposeCompanyResolution(company.Ticker,CompanyResolutionKind.Director,0))w.Rebuild();}),S.CompanyNominationBlocker(company),true);
            var columns=Ui.Row(parent,-1,12);Ui.Size(columns).GetComponent<LayoutElement>().flexibleHeight=1;
            var actions=CompanyCard(columns,"经营议案",1);
            bool internalAccess=S.CompanyBoardMember(company);
            Line(actions,"公司现金　"+(internalAccess?S.GovernanceCash(company).ToString():"获董事席位后可见"),17);
            Line(actions,"每日预算　"+(internalAccess?Money.FromCents(company.DailyBudgetCents).ToString():"—"),16,Ui.Dim);
            Line(actions,"总经理　"+S.ShareholderName(company.ManagerId),16);
            foreach(var kind in new[]{CompanyResolutionKind.Manager,CompanyResolutionKind.Budget,CompanyResolutionKind.Expansion,CompanyResolutionKind.Promotion,CompanyResolutionKind.Dividend,CompanyResolutionKind.Issue})
            {var pick=kind;CompanyButton(actions,GameSession.CompanyResolutionName(kind)+"…",()=>OpenGovernanceProposal(w,company,pick),S.CompanyBoardBlocker(company));}
            var decisionsShell=CompanyCard(columns,"表决与记录",1.1f);var decisions=PhoneFlexibleScroll(decisionsShell,"CompanyResolutions");
            if(company.Resolutions.Length==0)Line(decisions,"暂无议案",16,Ui.Dim);
            foreach(var resolution in company.Resolutions.Reverse())
            {
                var card=CompanyCard(decisions,GameSession.CompanyResolutionName(resolution.Kind));
                var votes=resolution.Closed?(resolution.VotesFor,resolution.VotesAgainst):S.CompanyVotePreview(company,resolution);
                long votingShares=resolution.Closed&&resolution.VotingShares>0?resolution.VotingShares:company.TotalShares;
                Line(card,$"{(resolution.Closed?"支持":"当前意向：支持")} {votes.Item1/(double)votingShares:P1} · 反对 {votes.Item2/(double)votingShares:P1}",15);
                Line(card,resolution.Closed?resolution.Result:"表决截止 "+new GameTimestamp(resolution.DueAt),14,resolution.Closed?(resolution.Passed?Ui.Good:Ui.Dim):Ui.Accent);
                if(!resolution.Closed)
                {
                    var choice=Ui.Row(card,32,5);
                    foreach(var vote in new[]{CompanyVote.Support,CompanyVote.Oppose,CompanyVote.Abstain}){var value=vote;Ui.Size(Ui.Button(choice,vote==CompanyVote.Support?"支持":vote==CompanyVote.Oppose?"反对":"弃权",()=>{S.VoteCompanyResolution(company.Ticker,resolution.Id,value);w.Rebuild();},14,Ui.SelectedButton(resolution.PlayerVote==vote),S.CompanyMeetingBlocker(company)==null),0,32,1);}
                }
            }
        }
        private void OpenGovernanceProposal(Window parent,CompanyGovernance company,CompanyResolutionKind kind)
        {
            string candidate=GameSession.PlayerShareholderId;long value=kind==CompanyResolutionKind.Budget?company.DailyBudgetCents:kind==CompanyResolutionKind.Issue?Math.Max(1,company.TotalShares/10):1000000;
            var own=S.ListedCompanyOf(company.Ticker)??(company.Ticker.StartsWith("listed:",StringComparison.Ordinal)?S.Company(company.Ticker.Substring(7)):null);
            OpenWindow(GameSession.CompanyResolutionName(kind),840,720,w=>
            {
                Line(w.Content,S.GovernanceName(company.Ticker),22,Ui.Accent);
                var choices=Ui.Rect(w.Content,"ProposalChoice");Ui.VList(choices,5,0);
                if(kind==CompanyResolutionKind.Manager||kind==CompanyResolutionKind.Issue)
                {
                    Line(choices,kind==CompanyResolutionKind.Issue?"认购人":"管理人",17);
                    foreach(string id in new[]{GameSession.PlayerShareholderId}.Concat(kind==CompanyResolutionKind.Manager?new[]{company.ManagerId}:Array.Empty<string>()).Concat(S.CompanyInvestors.Where(i=>i.Id.StartsWith("capital_",StringComparison.Ordinal)).Select(i=>i.Id)).Distinct())
                    {string pick=id;Ui.Size(Ui.Button(choices,S.ShareholderName(id),()=>{candidate=pick;w.Rebuild();},16,Ui.SelectedButton(candidate==id)),-1,34);}
                }
                if(kind==CompanyResolutionKind.Expansion&&own!=null)
                {
                    var options=S.MyShops.Where(s=>S.CompanyOfShop(s)==null&&s.RetailKind==S.CompanyBusinessKind(own)).ToArray();
                    if(!options.Any())Line(choices,"没有可划入的同业个人门店",16,Ui.Warn);
                    foreach(var shop in options)
                    {var pick=shop;CompanyButton(choices,S.ShopTitle(shop)+" · "+S.CompanyStoreTransferPrice(shop),()=>{candidate=pick.Place.Id;value=S.CompanyStoreTransferPrice(pick).Cents;w.Rebuild();},S.CompanyStoreTransferBlocker(own,shop));}
                    Line(choices,"选中门店　"+(S.ShopAt(candidate)==null?"—":S.ShopTitle(S.ShopAt(candidate))),16,Ui.Accent);
                }
                InputField input=null;
                if(kind!=CompanyResolutionKind.Manager&&!(kind==CompanyResolutionKind.Expansion&&own!=null))
                {
                    Line(w.Content,kind==CompanyResolutionKind.Issue?"新发股数（不超过当前股本 50%）":"金额（元）",17);
                    input=Ui.TextInput(w.Content,(kind==CompanyResolutionKind.Issue?value:value/100).ToString(),"数量",_=>{});Ui.Size(input,-1,36);input.contentType=InputField.ContentType.IntegerNumber;
                }
                var preview=Ui.Size(Ui.Label(w.Content,"",16,color:Ui.Dim),-1,100);var issue=Ui.Size(Ui.Label(w.Content,"",15,color:Ui.Warn),-1,45);Button submit=null;
                void Refresh()
                {
                    if(input!=null){long.TryParse(input.text,out long raw);value=raw<0||raw>1000000000?0:kind==CompanyResolutionKind.Issue?raw:raw*100;}
                    string why=S.CompanyResolutionBlocker(company.Ticker,kind,value,candidate);
                    preview.text=kind==CompanyResolutionKind.Issue?$"认购款 {S.CityInvestmentPrice(company.Ticker)*Math.Max(0,value)} 全部进入公司\n发行后总股本 {company.TotalShares+value:N0} 股\n你的持股 {S.ShareholderShares(company,GameSession.PlayerShareholderId)+(candidate==GameSession.PlayerShareholderId?value:0):N0} 股 · {(S.ShareholderShares(company,GameSession.PlayerShareholderId)+(candidate==GameSession.PlayerShareholderId?value:0))/(double)Math.Max(1,company.TotalShares+value):P2}":kind==CompanyResolutionKind.Dividend?$"公司支付 {Money.FromCents(Math.Max(0,value))}\n按实际总股本分配，周转资金须保留；派息同步除息。":kind==CompanyResolutionKind.Manager?"总经理获得公司经营和人事权限。股东身份与董事席位保留。":"公司资金支付，不从个人现金扣款。门店划入保留员工、库存、订单和采购；房屋产权保留。";
                    issue.text=why??"表决通过后执行；若资金或标的变化，会重新核对。";
                    if(submit!=null)submit.interactable=why==null;
                }
                if(input!=null)input.onValueChanged.AddListener(_=>Refresh());Refresh();
                submit=Ui.Size(Ui.Button(w.Content,"提交议案…",()=>
                {
                    Refresh();string why=S.CompanyResolutionBlocker(company.Ticker,kind,value,candidate);if(why!=null){Toast(why);return;}
                    ConfirmAction("提交"+GameSession.CompanyResolutionName(kind),preview.text+"\n下一游戏日同一时间按实际持股表决。","确认提交",()=>{if(S.ProposeCompanyResolution(company.Ticker,kind,value,candidate)){w.Close();parent.Rebuild();}},danger:kind==CompanyResolutionKind.Issue);
                },16,Ui.ButtonHot),-1,36);Refresh();
            });
        }
        private void BuildGovernanceControl(Transform parent,Window w,CompanyGovernance company)
        {
            var control=CompanyCard(parent,"控制权与管理层");Line(control,S.CompanyControlStatus(company),18,Ui.Accent);
            Line(control,"控股须超过 50% 表决权；刚好 50% 仍需争取其他股东。",15,Ui.Dim);
            if(company.HandoverAt>=0)
            {
                Line(control,"交接时间　"+new GameTimestamp(company.HandoverAt),17,Ui.Warn);
                if(company.RetentionOfferShopId!=null)
                {
                    Line(control,"留任邀请　"+S.ShopTitle(S.ShopAt(company.RetentionOfferShopId))+"店长 · 日薪 ¥200",16);
                    var decisions=Ui.Row(control,34,8);
                    Ui.Size(Ui.Button(decisions,company.RetentionAccepted?"已接受留任":"接受留任",()=>{S.SetCompanyRetention(company.Ticker,true);w.Rebuild();},15,company.RetentionAccepted?Ui.ButtonHot:Ui.ButtonSecondary),0,34,1);
                    Ui.Size(Ui.Button(decisions,"交接全部公司门店",()=>{S.SetCompanyRetention(company.Ticker,false);w.Rebuild();},15,Ui.ButtonSecondary),0,34,1);
                }
            }
            else if(S.MajorityShareholder(company)==GameSession.PlayerShareholderId&&company.ControllerId!=GameSession.PlayerShareholderId)
                CompanyButton(control,"发起接管…",()=>OpenCompanyTakeover(w,company),S.CompanyTakeoverBlocker(company),true);
            if(company.RetainedShopIds.Length>0)CompanyButton(control,"结束门店留任…",()=>ConfirmAction("结束留任","公司将接回门店管理。员工与业务继续保留，你仍是股东。","结束留任",()=>{S.LeaveCompanyManagement(company.Ticker);w.Rebuild();},danger:true));
            var row=Ui.Row(parent,-1,12);Ui.Size(row).GetComponent<LayoutElement>().flexibleHeight=1;
            var owned=S.ListedCompanyOf(company.Ticker)??S.Company(company.Ticker.StartsWith("listed:",StringComparison.Ordinal)?company.Ticker.Substring(7):"");
            var storesShell=CompanyCard(row,"公司门店与业务交接",1.3f);var stores=PhoneFlexibleScroll(storesShell,"HandoverCompanyStores");
            if(owned==null){Line(stores,"同行公司采用现有经营规模模型",17);Line(stores,$"经营规模 {S.StockState(company.Ticker)?.BusinessScale:0.00} · 总经理 {S.ShareholderName(company.ManagerId)}",16,Ui.Dim);Line(stores,"可通过董事会调整预算、扩张、推广及分红。",15,Ui.Dim);}
            else foreach(var shop in S.CompanyShops(owned))BuildGovernanceStore(stores,shop);
            var personalShell=CompanyCard(row,"继续由你经营的个人门店",1);var personal=PhoneFlexibleScroll(personalShell,"HandoverPersonalStores");
            foreach(var shop in S.MyShops.Where(s=>S.CompanyOfShop(s)==null))Line(personal,S.ShopTitle(shop)+" · 个人经营",16,Ui.Good);
            if(!S.MyShops.Any(s=>S.CompanyOfShop(s)==null))Line(personal,"暂无个人门店",16,Ui.Dim);
        }
        private void BuildGovernanceStore(Transform parent,OwnedShop shop)
        {
            var box=CompanyCard(parent,S.ShopTitle(shop));
            Line(box,S.PlayerManagesShop(shop)?"你可以经营":"公司管理层经营",16,S.PlayerManagesShop(shop)?Ui.Good:Ui.Dim);
            Line(box,$"员工 {S.EmployeesForShop(shop).Length} 人 · 订单 {shop.Sim.Orders.Count()} · 在途采购 {S.EmployeePurchaseTrips.Count(t=>t.ShopId==shop.Place.Id)}",14,Ui.Dim);
            Line(box,"库存、员工满意度、合同与待交付业务保留",14,Ui.Dim);
        }
        private void OpenCompanyTakeover(Window parent,CompanyGovernance company)
        {
            string manager=company.ManagerId;
            OpenWindow("接管公司",840,700,w=>
            {
                Line(w.Content,S.GovernanceName(company.Ticker)+" · 持股 "+S.ShareholderRatio(company,GameSession.PlayerShareholderId).ToString("P2"),23,Ui.Accent);
                Line(w.Content,"总经理",18);
                foreach(string id in new[]{company.ManagerId,GameSession.PlayerShareholderId}.Concat(S.CompanyInvestors.Where(i=>i.Id.StartsWith("capital_",StringComparison.Ordinal)).Select(i=>i.Id)).Distinct())
                {string pick=id;Ui.Size(Ui.Button(w.Content,S.ShareholderName(id)+(id==company.ManagerId?" · 原经理":""),()=>{manager=pick;w.Rebuild();},16,Ui.SelectedButton(manager==id)),-1,34);}
                Line(w.Content,"每日自主开支预算（元）",17);
                var budget=Ui.TextInput(w.Content,(company.DailyBudgetCents/100).ToString(),"元 / 日",_=>{});Ui.Size(budget,-1,36);budget.contentType=InputField.ContentType.IntegerNumber;
                Line(w.Content,"下一营业结算点交接。原员工、库存、订单、合同和采购继续执行。",16,Ui.Dim);
                CompanyButton(w.Content,"确认接管…",()=>
                {
                    if(!long.TryParse(budget.text,out long yuan)||yuan<100||yuan>10000000){Toast("每日预算须为 ¥100—¥10,000,000");return;}
                    ConfirmAction("确认公司接管",$"总经理 {S.ShareholderName(manager)}\n每日自主开支预算 {Money.FromYuan(yuan)}\n个人门店与公司门店的经营归属分开处理。","确认接管",()=>{if(S.ReorganizeControlledCompany(company.Ticker,manager,yuan*100)){w.Close();parent.Rebuild();}});
                },S.CompanyTakeoverBlocker(company),true);
            });
        }
    }
}
