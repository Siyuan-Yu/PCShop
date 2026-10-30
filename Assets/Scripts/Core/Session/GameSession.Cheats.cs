using PCShopSell.Core.Common;

namespace PCShopSell.Core.Session
{
    /// <summary>验收用的快捷操作（只有编辑器里的作弊工具会调用）。</summary>
    public sealed partial class GameSession
    {
        public void DebugAddCash(long yuan)
        {
            Ledger.Credit(Clock.Today, "作弊", Money.FromYuan(yuan), "作弊工具");
            Say($"作弊：现金 +¥{yuan:N0}。");
        }

        /// <summary>把当前任务直接记为完成，照常发奖励。</summary>
        public bool DebugCompleteQuest()
        {
            QuestDef q = CurrentQuest();
            if (q == null) return false;
            Flags.Add("tutorial:issued:" + q.Id);
            Flags.Add("tutorial:" + q.Id);
            q.Grant?.Invoke(this);
            Say($"作弊：任务「{q.Title}」直接完成，奖励：{q.Reward}。");
            return true;
        }

        public void DebugAddReputation(double delta)
        {
            if (!HasShop) return;
            PlayerShop.Reputation += delta;
            Say($"作弊：知名度 {PlayerShop.Reputation:0.0}。");
        }

        /// <summary>所有有交情的人都认识、交情 +delta；店员和街坊的熟络度也 +delta。</summary>
        public void DebugRaiseAllRelations(double delta)
        {
            foreach (NpcDef n in Npcs.All)
            {
                Meet(n.Id, 0);
                AddRelation(n.Id, delta);
            }
            foreach (StaffDef s in Rosters.All) AddStaffRapport(s.Id, delta);
            foreach (ResidentDef r in Residents.All) AddStaffRapport(r.Person.Id, delta);
            Say($"作弊：所有人交情 / 熟络 +{delta:0}。");
        }

        /// <summary>时间往后拨几个小时（照常结算营业、饥饿等）。</summary>
        public void DebugSkipHours(int hours)
        {
            if (hours > 0) AdvanceClock(hours * 60);
        }
    }
}
