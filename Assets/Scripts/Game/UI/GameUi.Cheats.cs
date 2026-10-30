#if UNITY_EDITOR
using UnityEngine;

namespace PCShopSell.Game.UI
{
    /// <summary>作弊工具：只在 Unity 编辑器里出现（左上角“作弊”按钮），打包时整个文件不编译。验收用的快捷操作都放这里。</summary>
    public sealed partial class GameUi
    {
        partial void AddCheatButton(Transform row)
        {
            Ui.Size(Ui.Button(row, "作弊", OpenCheatPanel, 16, Ui.Warn), 56).Tip("<b>作弊工具</b>（只在编辑器里有）\n验收用：加钱、跳过找铺子、完成任务、加交情、拨时间。");
        }

        private void OpenCheatPanel()
        {
            OpenWindow("作弊工具（仅编辑器）", 560, 640, w =>
            {
                RectTransform money = Ui.Row(w.Content, 44, 8);
                Ui.Size(Ui.Button(money, "现金 +1 万", () => S.DebugAddCash(10000), 17), -1, -1, 1);
                Ui.Size(Ui.Button(money, "现金 +10 万", () => S.DebugAddCash(100000), 17), -1, -1, 1);

                Ui.Size(Ui.Button(w.Content, S.HasShop ? "跳过找铺子（已经有店了）" : "跳过找铺子：直接租下街尾小店", () =>
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
                Ui.Size(Ui.Button(rep, "知名度 +5", () => S.DebugAddReputation(5), 17, Ui.ButtonBg, S.HasShop), -1, -1, 1);
                Ui.Size(Ui.Button(rep, "知名度 +20", () => S.DebugAddReputation(20), 17, Ui.ButtonBg, S.HasShop), -1, -1, 1);

                Ui.Size(Ui.Button(w.Content, "所有人交情 / 熟络 +20（并都认识）", () => S.DebugRaiseAllRelations(20), 17), -1, 44);

                RectTransform time = Ui.Row(w.Content, 44, 8);
                Ui.Size(Ui.Button(time, "时间 +1 小时", () => S.DebugSkipHours(1), 17, Ui.ButtonBg, S.Trip == null), -1, -1, 1);
                Ui.Size(Ui.Button(time, "时间 +6 小时", () => S.DebugSkipHours(6), 17, Ui.ButtonBg, S.Trip == null), -1, -1, 1);

                Ui.Size(Ui.Button(w.Content, "打开经营总览（不管有没有解锁）", () =>
                {
                    w.Close();
                    DebugOpenBusiness(0);
                }, 17), -1, 44);

                Line(w.Content, "这些按钮只在 Unity 编辑器里有，打包出来的游戏里没有。", 15, Ui.Dim);
            });
        }
    }
}
#endif
