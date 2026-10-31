using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void FastFoodFromBatch() => RunSmokeBatch(() =>
        {
            // This check deliberately does not capture screenshots.
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildRig(); _game = new GameObject("Game").AddComponent<GameController>();
            _game.Bootstrap(false, 7); _game.Ui.QuestPopups = false;
            var s = _game.Session; s.EventsEnabled = false; s.DebugAddCash(1000);
            s.Clock.Restore(s.Clock.Today, 16 * 60);
            var shop = s.City.Places.First(p => p.Business == GameSession.FastFoodBusiness);
            Teleport(s, shop.Id); _game.Ui.OnEnteredPlace(shop); Frame(0);
            Check(ActiveButtons().Count(b => b.name.StartsWith("BuyFastFood_")) == 6, "实际城市快餐店显示六种可购买餐品");
            Check(ActiveInputs().Length == 0, "点餐不需要输入数字");
            Check(ActiveButtons().Any(b => ButtonText(b).StartsWith("在这里打工")), "快餐菜单保留原有打工入口");
            Check(_game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Text>()).Any(t => t.text.Contains("营养") && t.text.Contains("心情") && t.text.Contains("鸡腿汉堡")), "菜单展示价格、饱腹、营养与心情");
            s.Player.Hunger = 5; long cash = s.Ledger.Balance.Yuan; long time = s.Clock.Now.TotalMinutes;
            ActiveButtons().First(b => b.name == "BuyFastFood_combo").onClick.Invoke(); Frame(0);
            Check(s.Player.Hunger == 5 && s.Ledger.Balance.Yuan == cash - 32 && s.Clock.Now.TotalMinutes == time,
                "买套餐只扣费放背包，不立即吃、不消耗用餐时间");
            Check(s.Backpack.Food.Packed.Any(b => b.Id == "fast:combo" && b.Name.Contains("汉堡炸鸡")), "背包获得具名套餐");
            ActiveButtons().First(b => b.name == "BuyFastFood_cola").onClick.Invoke(); Frame(0);
            Check(s.Backpack.Food.Packed.Any(b => b.Id == "fast:cola"), "可乐也购买后放进背包");
            Check(ActiveButtons().Any(b => b.name == "QuantityPlus_online_fastfood_burger"), "快餐份数沿用可长按加减按钮");
            _game.Ui.CloseAllWindows(); Check(s.CurrentPlace != shop, "关闭菜单回街上");
            _game.Ui.OpenBackpackPanel(); Frame(0);
            var comboCell = ActiveButtons().First(b => ButtonText(b).Contains("汉堡炸鸡")); comboCell.onClick.Invoke(); Frame(0);
            ActiveButtons().First(b => ButtonText(b).StartsWith("吃一份汉堡炸鸡")).onClick.Invoke(); Frame(0);
            Check(s.Player.Hunger > 90 && !s.Backpack.Food.Packed.Any(b => b.Id == "fast:combo"), "背包手动吃套餐产生饱腹效果并移除一份");
            var colaCell = ActiveButtons().First(b => ButtonText(b).Contains("可乐")); colaCell.onClick.Invoke(); Frame(0);
            Check(ActiveButtons().Any(b => ButtonText(b).StartsWith("喝一份可乐")), "背包可乐显示主动喝的入口");
            _game.Ui.CloseAllWindows();
        });
    }
}
