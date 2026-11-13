#if UNITY_EDITOR
using System.Collections.Generic;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    /// <summary>作弊工具：只在 Unity 编辑器里出现（左上角“作弊”按钮），打包时整个文件不编译。验收用的快捷操作都放这里。</summary>
    public sealed partial class GameUi
    {
        private int _eventBatchNext, _eventBatchEnd, _eventBatchShown;
        private int _eventBatchCurrent;
        private GameSession _eventBatchSession;
        private readonly List<string> _eventBatchUnavailable = new List<string>();

        partial void AddCheatButton(Transform row)
        {
            Ui.Size(Ui.Button(row, "作弊", OpenCheatPanel, 16, Ui.Warn), 56).Tip("<b>作弊工具</b>（只在编辑器里有）\n验收用：加钱、跳过找铺子、完成任务、加交情、拨时间。");
        }

        private void OpenCheatPanel()
        {
            OpenWindow("作弊工具（仅编辑器）", 560, 640, w =>
            {
                Ui.Size(Ui.Button(w.Content, "随机事件批量验收", OpenEventAcceptance, 18, Ui.ButtonHot), -1, 48).name = "CheatEventAcceptance";
                Ui.Size(Ui.Button(w.Content, "城市生活验收预置（独立测试档）", OpenCityAcceptance, 18, Ui.Warn), -1, 48);
                Ui.Size(Ui.Button(w.Content, "社区资格与品牌展会验收", OpenExpoAcceptance, 18, Ui.ButtonHot), -1, 48).name = "CheatBrandExpo";
                Ui.Size(Ui.Button(w.Content,"KOL直播验收（独立测试档）",()=>
                {
                    CloseAllWindows();_ctrl.StartCityLifePreset("kol_live");_ctrl.Ui.OpenKolManagement();
                },18,Ui.ButtonHot),-1,48).name="CheatKolLive";
                Ui.Size(Ui.Button(w.Content,"四区车库 / 赛车 / 二手车行验收（独立测试档）",()=>{CloseAllWindows();_ctrl.StartCityLifePreset("racing");_ctrl.Ui.OnEnteredPlace(_ctrl.Session.CurrentPlace);},18,Ui.ButtonHot),-1,48).name="CheatRacing";
                Ui.Size(Ui.Button(w.Content, "人物与剧情配置（只读）", OpenNarrativeCatalogue, 18), -1, 44);
                Ui.Size(Ui.Button(w.Content, "区域主线验收：直接进入任意阶段", OpenDistrictAcceptance, 18, Ui.ButtonHot), -1, 48).name="CheatDistrictProgression";
                BuildCommunityConditionsCheat(w.Content,w.Rebuild);
                Ui.Size(Ui.Button(w.Content,S.DebugHomeWorkbenchEnabled?"关闭大屏（取消作弊接入）":"打开大屏（免购买接入）",()=>
                {
                    bool enabled=!S.DebugHomeWorkbenchEnabled;
                    S.DebugSetHomeWorkbench(enabled);
                    if(enabled)
                    {
                        w.Close();
                        if(S.CanUseHomeWorkbench)OpenHomeWorkbench();
                        else Toast("已开启大屏测试接入，回家后可打开居家工作台。");
                    }
                    else{_homeWorkbench?.Close();w.Rebuild();}
                },18,S.DebugHomeWorkbenchEnabled?Ui.Warn:Ui.ButtonHot),-1,44).name="CheatHomeWorkbench";
                Ui.Size(Ui.Button(w.Content,S.DebugInvitationPersonEnabled?"让小游消失（验收 NPC）":"让小游出现（满好感验收 NPC）",()=>
                {
                    S.DebugSetInvitationPerson(!S.DebugInvitationPersonEnabled);
                    w.Rebuild();
                },18,S.DebugInvitationPersonEnabled?Ui.Warn:Ui.ButtonHot),-1,48)
                    .Tip("小游仅用于验收。出现后在安居房产中介附近散步；隐藏会结束他的进行中邀约，保留历史记录。");
                RectTransform money = Ui.Row(w.Content, 44, 8);
                Ui.Size(Ui.Button(money, "现金 +1 万", () => S.DebugAddCash(10000), 17), -1, -1, 1);
                Ui.Size(Ui.Button(money, "现金 +10 万", () => S.DebugAddCash(100000), 17), -1, -1, 1);

                Ui.Size(Ui.Button(w.Content, S.HasShop ? "跳过找铺子（已经有店了）" : "跳过找铺子：直接开宠物用品店", () =>
                {
                    S.DebugRentStarterShop();
                    w.Rebuild();
                }, 17, Ui.ButtonBg, !S.HasShop), -1, 44);

                Ui.Size(Ui.Button(w.Content, $"[{(S.HasSmartphone ? "x" : " ")}] 拥有智能手机（所有 app 都装好）", () =>
                {
                    S.SetSmartphone(!S.HasSmartphone);
                    w.Rebuild();
                }, 17, S.HasSmartphone ? Ui.Warn : Ui.ButtonBg), -1, 44);

                var q = S.CurrentQuest();
                Ui.Size(Ui.Button(w.Content, q != null ? $"完成当前任务：{q.Title}" : "没有进行中的任务", () =>
                {
                    S.DebugCompleteQuest();
                    w.Rebuild();
                }, 17, Ui.ButtonBg, q != null), -1, 44);

                RectTransform rep = Ui.Row(w.Content, 44, 8);
                Ui.Size(Ui.Button(rep, "名望 +5", () => S.DebugAddReputation(5), 17, Ui.ButtonBg, S.HasShop), -1, -1, 1);
                Ui.Size(Ui.Button(rep, "名望 +20", () => S.DebugAddReputation(20), 17, Ui.ButtonBg, S.HasShop), -1, -1, 1);

                Ui.Size(Ui.Button(w.Content, "所有人交情 / 熟络 +20（并都认识）", () => S.DebugRaiseAllRelations(20), 17), -1, 44);

                RectTransform time = Ui.Row(w.Content, 44, 8);
                Ui.Size(Ui.Button(time, "时间 +1 小时", () => S.DebugSkipHours(1), 17, Ui.ButtonBg, S.Trip == null), -1, -1, 1);
                Ui.Size(Ui.Button(time, "时间 +6 小时", () => S.DebugSkipHours(6), 17, Ui.ButtonBg, S.Trip == null), -1, -1, 1);

                Ui.Size(Ui.Button(w.Content, "打开资产页", () =>
                {
                    w.Close();
                    DebugOpenBusiness(0);
                }, 17), -1, 44);

                Line(w.Content, "这些按钮只在 Unity 编辑器里有，打包出来的游戏里没有。", 15, Ui.Dim);
            });
        }

        private void OpenEventAcceptance()
        {
            OpenWindow("随机事件批量验收", 660, 600, w =>
            {
                Para(w.Content, "每批从独立测试档开始。选完当前事件并关闭结算后，会自动弹出下一项。", 17);
                if (_eventBatchNext > 0)
                {
                    Line(w.Content, $"下一项 #{_eventBatchNext:00} · 本批到 #{_eventBatchEnd:00}", 17, Ui.Accent);
                    Ui.Size(Ui.Button(w.Content, "结束当前批次", () => { _eventBatchNext = 0; w.Rebuild(); }, 17), -1, 40);
                }
                var list = Ui.Scroll(w.Content, 340);
                void RangeButton(Transform parent, int first, int last)
                {
                    Ui.Size(Ui.Button(parent, $"触发 {first:00}–{last:00}", () => StartEventBatch(first, last), 18,
                        first == 1 ? Ui.ButtonHot : Ui.ButtonBg), -1, 42, 1);
                }
                var firstRow = Ui.Row(list, 42, 8);
                RangeButton(firstRow, 1, 10);
                RangeButton(firstRow, 2, 20);
                for (int first = 11; first <= GameSession.DebugLastEventNumber; first += 20)
                {
                    var row = Ui.Row(list, 42, 8);
                    RangeButton(row, first, System.Math.Min(first + 9, GameSession.DebugLastEventNumber));
                    int next = first + 10;
                    if (next <= GameSession.DebugLastEventNumber)
                        RangeButton(row, next, System.Math.Min(next + 9, GameSession.DebugLastEventNumber));
                }
                if (S.DebugEventAcceptanceSession)
                    Ui.Size(Ui.Button(w.Content, "返回进入测试前的进度", () =>
                    {
                        _eventBatchNext = 0;
                        _ctrl.ReturnFromCityLifePreset();
                    }, 17), -1, 42);
            });
        }

        private void StartEventBatch(int first, int last)
        {
            if (!S.CanSave) { Toast("请先处理当前事件。"); return; }
            _ctrl.StartCityLifePreset("events");
            var ui = _ctrl.Ui;
            ui._eventBatchNext = first;
            ui._eventBatchEnd = last;
            ui._eventBatchShown = 0;
            ui._eventBatchSession = _ctrl.Session;
            ui._eventBatchUnavailable.Clear();
            ui.UpdateEventBatch();
        }

        partial void UpdateEventBatch()
        {
            if (_eventBatchNext == 0) return;
            if (_eventBatchSession != S)
            {
                _eventBatchNext = 0;
                _eventBatchSession = null;
                return;
            }
            if (AnyModalOpen || _ctrl.Placing || !S.DebugEventAcceptanceReady) return;
            while (_eventBatchNext <= _eventBatchEnd)
            {
                int number = _eventBatchNext++;
                var entry = GameSession.DebugNumberedEvent(number);
                _eventBatchCurrent = number;
                bool shown;
                try { shown = S.DebugRaiseNumberedEvent(number); }
                finally { _eventBatchCurrent = 0; }
                if (shown)
                {
                    _eventBatchShown++;
                    return;
                }
                _eventBatchUnavailable.Add($"#{number:00} " + (entry == null ? "已并入 #09" : entry.Title + " · 当前条件不适用"));
                if (AnyModalOpen || !S.DebugEventAcceptanceReady) return;
            }
            _eventBatchNext = 0;
            _eventBatchSession = null;
            string summary = $"本批已触发 {_eventBatchShown} 个事件。";
            if (_eventBatchUnavailable.Count > 0)
                summary += "\n\n未弹出的编号\n" + string.Join("\n", _eventBatchUnavailable);
            OpenWindow("事件验收完成", 650, 440, w =>
            {
                Para(Ui.Scroll(w.Content, 225), summary, 18);
                Ui.Size(Ui.Button(w.Content, "继续验收其他批次", () => { w.Close(); OpenEventAcceptance(); }, 18, Ui.ButtonHot), -1, 42);
                Ui.Size(Ui.Button(w.Content, "返回进入测试前的进度", _ctrl.ReturnFromCityLifePreset, 17), -1, 42);
            });
        }
    }
}
#endif
