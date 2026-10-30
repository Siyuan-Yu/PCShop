using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCShopSell.Core.Common;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public enum GoalState
    {
        Pending,
        Passed,
        Failed,
    }

    /// <summary>目标面板里的一行。</summary>
    public sealed class GoalLine
    {
        public string Id;
        public string Title;
        public int Day;
        public string Goal;
        public string Progress;
        public GoalState State;
    }

    public sealed partial class GameSession
    {
        private readonly Dictionary<string, bool> _milestoneResults = new Dictionary<string, bool>();

        /// <summary>没过检查点累计的涨租次数（最多 3 次）。</summary>
        public int RentHikes { get; private set; }

        /// <summary>三年结局的编号；还没到就是 null。到了也可以继续玩。</summary>
        public string EndingId { get; private set; }

        public static string SizeName(ShopSize size) => size == ShopSize.Large ? "大型店" : size == ShopSize.Medium ? "中型店" : "小店";

        /// <summary>现在每月实际交的店租：签约时的月租，再随涨租次数、房东交情变化；没租铺子时为 0。</summary>
        public Money CurrentShopRent
        {
            get
            {
                if (Lease == null) return Money.Zero;
                double mult = 1.0 + Campaign.RentHikePerFail * Math.Min(3, RentHikes);
                mult *= 1.0 - NpcPerks.RentDiscount(Relation(Npcs.Landlady));
                return Lease.Rent.Scale(mult);
            }
        }

        internal void AddRentHike() => RentHikes = Math.Min(3, RentHikes + 1);

        public bool MilestoneEvaluated(string id) => _milestoneResults.ContainsKey(id);

        public bool MilestonePassed(string id) => _milestoneResults.TryGetValue(id, out bool p) && p;

        /// <summary>所有目标（含第一个月）的当前状态，给界面画列表。</summary>
        public List<GoalLine> Goals()
        {
            var list = new List<GoalLine>
            {
                new GoalLine
                {
                    Id = "m1", Title = "第一个月", Day = MonthGoalDay,
                    Goal = $"第 {MonthGoalDay} 天：身家 ≥ {MonthGoalNetWorth}，并且雇过至少 1 名员工",
                    Progress = $"身家 {NetWorth()} / {MonthGoalNetWorth}　雇过人：{(Employee != null || EverHired ? "是" : "否")}",
                    State = !MonthGoalEvaluated ? GoalState.Pending : MonthGoalPassed ? GoalState.Passed : GoalState.Failed,
                },
            };
            foreach (MilestoneDef m in Campaign.Milestones)
            {
                list.Add(new GoalLine
                {
                    Id = m.Id, Title = m.Title, Day = m.Day, Goal = m.Goal, Progress = m.Progress(this),
                    State = !MilestoneEvaluated(m.Id) ? GoalState.Pending : MilestonePassed(m.Id) ? GoalState.Passed : GoalState.Failed,
                });
            }
            list.Add(new GoalLine
            {
                Id = "end", Title = "三年之后", Day = Campaign.TotalDays,
                Goal = $"第 {Campaign.TotalDays} 天：看你把店经营成什么样（身家、店面、知名度、人脉都会写进结局）",
                Progress = $"现在身家 {NetWorth()}　店面 {SizeName(PlayerShop.Size)}　知名度 {PlayerShop.Reputation:0}",
                State = EndingId == null ? GoalState.Pending : GoalState.Passed,
            });
            return list;
        }

        /// <summary>现在最近的未完成目标（没有则返回 null）。</summary>
        public GoalLine NextGoal() => Goals().FirstOrDefault(g => g.State == GoalState.Pending);

        // ---------- 评估 ----------

        private void EvaluateCampaign(GameDate today)
        {
            foreach (MilestoneDef m in Campaign.Milestones)
            {
                if (_milestoneResults.ContainsKey(m.Id) || today.TotalDays < m.Day) continue;
                bool ok = m.Check(this);
                _milestoneResults[m.Id] = ok;
                if (ok)
                {
                    PlayerShop.Reputation += Campaign.PassReputationBonus;
                    if (m.Id == "y1") GrantUpgrade(ShopUpgrades.SelfCheckout);
                    Popup?.Invoke($"{m.Title}：达标", $"{m.Progress(this)}\n\n{m.PassText}");
                }
                else
                {
                    RentHikes = Math.Min(3, RentHikes + 1);
                    Popup?.Invoke($"{m.Title}：未达标", $"{m.Progress(this)}\n\n{m.FailText}");
                }
            }

            if (EndingId == null && today.TotalDays >= Campaign.TotalDays) RaiseEnding();
        }

        private void RaiseEnding()
        {
            EndingDef e = Campaign.Evaluate(this);
            EndingId = e.Id;
            Popup?.Invoke("三年之后：" + e.Title, ComposeEpilogue(e));
        }

        /// <summary>结局正文：基调 + 数字 + 几个重要的人。</summary>
        public string ComposeEpilogue(EndingDef e)
        {
            var sb = new StringBuilder();
            sb.AppendLine(e.Summary);
            sb.AppendLine();
            sb.AppendLine($"身家 {NetWorth()}　店面 {SizeName(PlayerShop.Size)}　知名度 {PlayerShop.Reputation:0}");
            sb.AppendLine($"一年目标：{(MilestonePassed("y1") ? "达标" : "未达标")}　两年目标：{(MilestonePassed("y2") ? "达标" : "未达标")}");
            if (Player.Health < 50) sb.AppendLine("这些年熬夜太多，你的身体亏得厉害，医生让你多休息。");
            foreach (NpcDef n in Npcs.All)
            {
                int r = Relation(n.Id);
                if (r < 60) continue;
                sb.AppendLine(EpilogueLine(n.Id, r));
            }
            sb.AppendLine();
            sb.Append("游戏可以继续玩下去。");
            return sb.ToString();
        }

        private static string EpilogueLine(string npcId, int relation)
        {
            switch (npcId)
            {
                case Npcs.Zhang: return "老张带着一批又一批学生来店里配电脑，逢人就说“这是我看着长大的店”。";
                case Npcs.Landlady: return "李姐一直没怎么涨你的租，逢年过节还会上楼送碗汤圆。";
                case Npcs.Zhou: return "老周把压箱底的货源都介绍给了你，还说你比他年轻时有出息。";
                case Npcs.Jie: return "阿杰的店和你的店互相引流，两个人在街口喝酒到半夜。";
                case Npcs.Lin: return "小林还是那个小林，不管你富了穷了，他永远是第一个来的。";
                default: return "";
            }
        }

        // ---------- 存档 ----------

        private void SaveCampaign(SaveData d)
        {
            d.milestoneIds = _milestoneResults.Keys.ToArray();
            d.milestonePassed = d.milestoneIds.Select(k => _milestoneResults[k]).ToArray();
            d.rentHikes = RentHikes;
            d.endingId = EndingId;
            d.shopSize = (int)PlayerShop.Size;
        }

        private void RestoreCampaign(SaveData d)
        {
            _milestoneResults.Clear();
            int n = Math.Min(d.milestoneIds?.Length ?? 0, d.milestonePassed?.Length ?? 0);
            for (int i = 0; i < n; i++) _milestoneResults[d.milestoneIds[i]] = d.milestonePassed[i];
            RentHikes = Math.Max(0, Math.Min(3, d.rentHikes));
            EndingId = string.IsNullOrEmpty(d.endingId) ? null : d.endingId;
            PlayerShop.Size = Enum.IsDefined(typeof(ShopSize), d.shopSize) ? (ShopSize)d.shopSize : ShopSize.Small;
        }
    }
}
