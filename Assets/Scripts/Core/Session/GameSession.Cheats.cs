using PCShopSell.Core.Common;
using System.Linq;

namespace PCShopSell.Core.Session
{
    /// <summary>验收用的快捷操作（只有编辑器里的作弊工具会调用）。</summary>
    public sealed partial class GameSession
    {
        private const string InvitationTestEnabledFlag="debug:invitation-test-person";
        private const string HomeWorkbenchTestFlag="debug:home-workbench";
        public bool DebugHomeWorkbenchEnabled
        {
            get
            {
#if UNITY_EDITOR
                return Flags.Contains(HomeWorkbenchTestFlag);
#else
                return false;
#endif
            }
        }
        public void DebugSetHomeWorkbench(bool enabled)
        {
#if UNITY_EDITOR
            if(enabled)Flags.Add(HomeWorkbenchTestFlag);else Flags.Remove(HomeWorkbenchTestFlag);
#endif
        }
        public bool DebugInvitationPersonEnabled
        {
            get
            {
#if UNITY_EDITOR
                return Flags.Contains(InvitationTestEnabledFlag);
#else
                return false;
#endif
            }
        }

        public void DebugSetInvitationPerson(bool visible)
        {
            if(visible)Flags.Add(InvitationTestEnabledFlag); else Flags.Remove(InvitationTestEnabledFlag);
            InitializeCityPeople();
            Say(DebugInvitationPersonEnabled?"小游已出现在安居房产中介附近，好感已满，可用于邀约与聊天验收。":"小游已隐藏；相关验收邀约已结束，历史记录保留。");
        }

        private void RemoveInvitationTestPerson()
        {
            string id=CityLifeCatalog.InvitationTestPersonId;
            foreach(var activity in _cityActivities.Where(a=>a.Active && a.Participants.Any(p=>p.PersonId==id)).ToArray())
                EndCityActivity(activity,CityActivityPhase.Cancelled,"已通过作弊工具移除验收伙伴，结束本次验收安排",Clock.Now.TotalMinutes);
            _cityPeople.Remove(id);
            _relations.Remove(id);
            Flags.Remove(MetFlag(id)); Flags.Remove("greeted:"+id); Flags.Remove("heard:"+id);
        }

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
            Say($"作弊：任务「{q.Title}」直接完成，奖励：{q.RewardText(this)}。");
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
