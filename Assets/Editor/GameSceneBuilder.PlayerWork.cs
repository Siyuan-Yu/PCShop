using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void PlayerWorkFromBatch() => RunSmokeBatch(SmokePlayerWork);
        private static void SmokePlayerWork()
        {
            // This validation deliberately generates no screenshots.
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildRig(); _game = new GameObject("Game").AddComponent<GameController>();
            _game.Bootstrap(false, 7); _game.Ui.QuestPopups = false;
            var s = _game.Session; s.EventsEnabled = false;
            s.Clock.Restore(s.Clock.Today, 10 * 60);
            var farm = s.City.Places.First(p => p.Business == GameSession.FarmBusiness);
            Teleport(s, farm.Id); _game.Ui.OnEnteredPlace(farm); Frame(0);
            ClickGuideButton("在这里打工 · 农贸市场早班 · ¥145/天");
            ClickGuideButton("签一周合同（明天开始）");
            Check(s.HasWorkContract && s.WorkContract.StartDay == s.Clock.Today.TotalDays + 1, "店内按钮签一周，次日开始，无需找人聊天");
            Check(!ActiveButtons().Any(b => ButtonText(b).Contains("开始今天的工作")), "签约当天没有手动开始工作按钮");
            Check(_game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Text>()).Any(t => t.text.Contains("明天再来工作")), "签约当天明确提示明天再来工作");
            _game.Ui.CloseAllWindows();
            s.Clock.Restore(new GameDate(s.WorkContract.StartDay), s.WorkContract.ShiftStart);
            Teleport(s, farm.Id); _game.Ui.OnEnteredPlace(farm); Frame(0);
            Check(s.IsWorking && _game.Ui.WorkScreenVisible, "进入签约店后自动开始打工并显示全屏状态");
            Check(!s.Time.IsHeld, "打工状态不加暂停锁，世界继续运行");
            var screen = _game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Transform>()).First(t => t.name == "PlayerWorkScreen");
            Check(screen.GetComponentsInChildren<Button>().Length == 4, "打工界面仅提供四档时间速度，无离开或购物按钮");
            Check(!_game.Ui.TryHotkey(KeyCode.M) && !_game.Ui.TryHotkey(KeyCode.Escape), "工作中不能通过快捷键打开管理或菜单");
            long before = s.Clock.Now.TotalMinutes;
            ClickGuideButton("4×"); Frame(.25f);
            Check(s.Clock.Now.TotalMinutes > before && s.Time.Speed == 8, "打工中倍速实际推进时间");
            _game.Ui.Popup("后台配送消息", "货已送到，收工后查看。");
            var e = EventCatalog.All.First(); s.RaiseEvent(e.Id);
            Check(!s.Time.IsHeld && !_game.Ui.AnyModalOpen, "后台消息与随机选择事件延后，不让工作停住");
            s.AdvanceClock(s.WorkMinutesLeft); Frame(0);
            Check(!s.IsWorking && !_game.Ui.WorkScreenVisible && s.WorkContract.EarnedYuan == 145, "到点收工，移除操作锁并记录当天工资");
            int choice = Enumerable.Range(0, e.Choices.Count).FirstOrDefault(i => s.IsChoiceEnabled(e.Choices[i]));
            Check(s.ResolveEvent(choice), "下班后可以处理延期事件");
            _game.Ui.CloseAllWindows(); Frame(0);
            Check(!s.Time.IsHeld, "处理消息后不残留工作暂停锁");
            s.Clock.Restore(new GameDate(s.WorkContract.EndDay), s.WorkContract.ShiftEnd); s.AdvanceClock(1);
            _game.Ui.OpenMyPanel(); Frame(0);
            var cash = s.Ledger.Balance;
            ClickGuideButton("领取这周工资 ¥145");
            Check(s.WorkContract.Paid && s.Ledger.Balance.Yuan == cash.Yuan + 145, "在我的面板领取已出勤工资，不要求重新进货");
            _game.Ui.CloseAllWindows();
            var bakery = s.City.Places.First(p => p.Business == "面包");
            Teleport(s, bakery.Id); _game.Ui.OnEnteredPlace(bakery); Frame(0);
            ClickGuideButton("在这里打工 · 面包工坊烘焙夜班 · ¥175/天");
            ClickGuideButton("签一周合同（明天开始）");
            _game.Ui.CloseAllWindows();
            s.Clock.Restore(new GameDate(s.WorkContract.StartDay), 22 * 60);
            Teleport(s, bakery.Id); _game.Ui.OnEnteredPlace(bakery); Frame(0);
            Check(s.IsWorking && _game.Ui.WorkScreenVisible && s.WorkHours.Contains("次日"), "夜间商店打烊后凭合同到岗，夜班显示次日下班");
            s.AdvanceClock(6 * 60); Frame(0);
            Check(s.IsWorking && s.CurrentPlace == bakery && s.Clock.MinuteOfDay == 4 * 60, "夜班跨午夜并经过凌晨4点，不会强制晕倒");
            s.AdvanceClock(s.WorkMinutesLeft); Frame(0);
            Check(!s.IsWorking && s.WorkContract.EarnedYuan == 175 && s.WorkContract.HealthCost == 11 && s.WorkContract.MoodCost == 12, "夜班次日6点收工，工资和消耗使用本店参数");
            Teleport(s, s.Home.Id); _game.Ui.OpenBedWindow(); Frame(0);
            ClickGuideButton("睡 8 小时");
            Check(s.Clock.MinuteOfDay == 14 * 60, "夜班下班后能白天补觉，不被原白天不能睡规则卡住");
            _game.Ui.CloseAllWindows();
        }
    }
}
