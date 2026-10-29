using System.Linq;
using System.Text;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Market;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        /// <summary>进入地点后打开对应面板。离开面板 = 回到街上。</summary>
        public void OnEnteredPlace(Place p)
        {
            switch (p.Kind)
            {
                case PlaceKind.Shop when p.IsPlayerShop:
                    OpenDock();
                    break;
                case PlaceKind.Home:
                    OpenPlaceWindow(p, BuildHome, 760, 760);
                    break;
                case PlaceKind.Wholesaler:
                    OpenPlaceWindow(p, BuildStall, 860, 800);
                    break;
                case PlaceKind.Dealer:
                    OpenPlaceWindow(p, BuildDealer);
                    break;
                case PlaceKind.Station:
                    OpenPlaceWindow(p, BuildStation);
                    break;
                default:
                    OpenPlaceWindow(p, BuildNeighbour);
                    break;
            }
        }

        private void OpenPlaceWindow(Place p, System.Action<Window, Place> build, float w = 640, float h = 520)
        {
            OpenWindow(p.Name, w, h, win =>
            {
                if (S.CurrentPlace != p)
                {
                    win.Close();
                    return;
                }
                build(win, p);
                Ui.Size(Ui.Button(win.Content, "离开，回到街上", () => win.Close(), 20), -1, 46);
            }, true, true, () =>
            {
                if (S.CurrentPlace == p) S.Leave();
            });
        }

        private static Text Line(Transform parent, string text, int size = 19, Color? color = null)
        {
            Text t = Ui.Label(parent, text, size, TextAnchor.MiddleLeft, color);
            Ui.Size(t, -1, size + 12);
            return t;
        }

        // ---------- 家 ----------

        private void BuildHome(Window w, Place p)
        {
            PlayerStats st = S.Player;
            Line(w.Content, "城中村出租屋：一张床，一个电饭锅。月租 " + GameSession.HomeRent);
            Line(w.Content, $"饥饿 {st.Hunger:0}  精力 {st.Energy:0}/{st.MaxEnergy:0}  健康 {st.Health:0}  欠觉 {st.SleepDebt:0.#} 小时", 18, Ui.Dim);
            Line(w.Content, $"智力 {st.Intelligence}  魅力 {st.Charm}  技术 {st.Skill}", 18, Ui.Dim);
            Line(w.Content, "三餐（7:30 / 12:00 / 18:30）自动在外面吃，每顿 " + GameSession.MealPrice, 17, Ui.Dim);
            RectTransform row = Ui.Row(w.Content, 54);
            Ui.Size(Ui.Button(row, "睡到明早 8:00", () =>
            {
                if (S.Sleep()) Toast("新的一天。");
                w.Rebuild();
            }, 20, S.IsSleepTime ? Ui.ButtonHot : Ui.ButtonBg), 260);
            Ui.Size(Ui.Button(row, "小睡 2 小时", () =>
            {
                S.Nap();
                w.Rebuild();
            }, 20), 200);
            Line(w.Content, "凌晨 4 点还不睡会晕倒；睡不够 7 小时会欠觉，精力上限降低。", 16, Ui.Dim);

            // 住处改善
            Line(w.Content, $"住处：{HomeComforts.Name(S.HomeComfort)}（睡眠恢复 ×{HomeComforts.SleepQuality(S.HomeComfort):0.##}" + (S.HomeComfort > 0 ? $"，水电 {HomeComforts.MonthlyUtilities(S.HomeComfort)}/月" : "") + "）", 18);
            if (S.HomeComfort < HomeComforts.MaxLevel)
            {
                Money cost = HomeComforts.UpgradePrice(S.HomeComfort + 1);
                Ui.Size(Ui.Button(w.Content, $"改善住处：{HomeComforts.Name(S.HomeComfort + 1)}（{cost}）", () =>
                {
                    S.UpgradeHome();
                    w.Rebuild();
                }, 18, Ui.ButtonHot), -1, 44);
            }

            // 衣橱
            Line(w.Content, $"衣橱（当前魅力 {S.EffectiveCharm}，其中衣着加成 +{S.Outfit.CharmBonus}）：", 18);
            foreach (OutfitDef def in Outfits.All)
            {
                OutfitDef o = def;
                bool has = S.OwnedOutfits.Contains(o.Id);
                bool worn = S.EquippedOutfit == o.Id;
                RectTransform orow = Ui.Row(w.Content, 40);
                Ui.Size(Ui.Label(orow, $"{o.Name}  魅力 +{o.CharmBonus}" + (has ? "" : $"  {o.Price}"), 17, TextAnchor.MiddleLeft, worn ? Ui.Good : (Color?)null), 420);
                Ui.Size(Ui.Button(orow, worn ? "穿着中" : has ? "换上" : "购买", () =>
                {
                    if (has) S.Equip(o.Id); else S.BuyOutfit(o.Id);
                    w.Rebuild();
                }, 17, Ui.ButtonHot, !worn), 120);
            }
        }

        // ---------- 档口 ----------

        private void BuildStall(Window w, Place p)
        {
            bool open = S.IsWholesalerOpen;
            Line(w.Content, open ? "营业中（工作日 9:00–18:00）。批发价，只卖盒装正品。" : "<color=#E0806A>关门了（工作日 9:00–18:00 营业）</color>");
            Line(w.Content, $"随身货：{S.Cargo.UsedSpace:0.#}/{S.Cargo.Capacity:0} 格（{S.CurrentVehicle.Name}）   现金 {S.Ledger.Balance}", 18, Ui.Dim);

            BuildOwnerRow(w, p);

            RectTransform list = Ui.Scroll(w.Content, 330);
            foreach (PartDef part in PartCatalog.All.Where(x => S.StallSells(p, x)))
            {
                PartDef pd = part;
                Money price = S.StallPrice(p, pd);
                Money market = S.Market.PriceOf(pd);
                RectTransform row = Ui.Row(list, 44);
                Ui.Size(Ui.Label(row, $"{pd.Name}", 18), 220);
                Ui.Size(Ui.Label(row, $"{price}", 19, TextAnchor.MiddleLeft, Ui.Good), 100);
                Ui.Size(Ui.Label(row, $"市价 {market}", 15, TextAnchor.MiddleLeft, Ui.Dim), 120);
                Ui.Size(Ui.Label(row, $"{Stock.SpaceOf(pd, true, 1):0.#}格", 15, TextAnchor.MiddleLeft, Ui.Dim), 50);
                int left = S.StallRemaining(p, pd);
                Ui.Size(Ui.Label(row, $"今日余 {left}", 15, TextAnchor.MiddleLeft, left == 0 ? Ui.Warn : Ui.Dim), 90);
                foreach (int n in new[] { 1, 5 })
                {
                    int count = n;
                    Ui.Size(Ui.Button(row, "买" + count, () =>
                    {
                        if (S.BuyAtStall(p, pd, count)) Toast($"买了 {pd.Name}×{count}");
                        w.Rebuild();
                    }, 17, Ui.ButtonHot, open && left >= count), 70);
                }
            }

            RectTransform brow = Ui.Row(w.Content, 50);
            bool tried = S.HasBargainedToday(p);
            Ui.Size(Ui.Button(brow, tried ? "今天砍过价了" : "跟老板砍价", () =>
            {
                BargainResult r = S.Bargain(p);
                if (r == BargainResult.Success) Toast("砍价成功！今天这家便宜了几个点。");
                else if (r == BargainResult.Failed) Toast("老板不乐意：\"爱买不买。\" 今天还涨了 1%。");
                w.Rebuild();
            }, 19, Ui.ButtonBg, open && !tried), 220);
            Ui.Size(Ui.Label(brow, $"成功率看魅力（{S.Player.Charm}），太累会降低", 16, TextAnchor.MiddleLeft, Ui.Dim), 400);
        }

        // ---------- 车行 ----------

        private void BuildDealer(Window w, Place p)
        {
            Line(w.Content, S.IsDealerOpen ? "营业中（每天 9:00–19:00）" : "<color=#E0806A>关门了（每天 9:00–19:00）</color>");
            foreach (VehicleSpec v in Vehicles.All.Where(x => x.Purchasable))
            {
                VehicleSpec vs = v;
                RectTransform row = Ui.Row(w.Content, 56);
                string desc = $"{vs.Name}  {vs.Price}\n<size=15>速度 {vs.SpeedPerMinute * 60 / 1000:0} km/h · 载货 {vs.CargoCapacity:0} 格{(vs.Illegal ? " · <color=#E0806A>城里不能上路，交警会追</color>" : "")}</size>";
                Ui.Size(Ui.Label(row, desc, 19), 420);
                bool owned = S.Owns(vs.Kind);
                Ui.Size(Ui.Button(row, owned ? "已拥有" : "购买", () =>
                {
                    S.BuyVehicle(vs.Kind);
                    w.Rebuild();
                }, 19, Ui.ButtonHot, !owned && S.IsDealerOpen), 120);
            }
            if (S.TricycleImpounded)
            {
                Ui.Size(Ui.Button(w.Content, "交 ¥500 赎回被扣的三轮车", () =>
                {
                    S.RedeemTricycle();
                    w.Rebuild();
                }, 19, Ui.Warn), -1, 46);
            }
            Line(w.Content, "步行能带 6 格货，打车 15 格（按里程收费），小电驴 10 格，三轮车 40 格。", 16, Ui.Dim);
        }

        // ---------- 地铁 ----------

        private void BuildStation(Window w, Place p)
        {
            Place other = S.City.OfKind(PlaceKind.Station).FirstOrDefault(x => x != p);
            Line(w.Content, "地铁 1 号线：商业街站 ⇄ 电子市场站");
            Line(w.Content, $"票价 {GameSession.MetroFare}，约 {GameSession.MetroMinutes} 分钟。运营 6:00–23:00。", 18, Ui.Dim);
            if (other != null)
            {
                Ui.Size(Ui.Button(w.Content, "坐地铁去 " + other.Name, () =>
                {
                    if (S.TakeMetro())
                    {
                        CloseWindowSilently(w);
                        if (S.CurrentPlace != null) OnEnteredPlace(S.CurrentPlace);
                    }
                }, 21, Ui.ButtonHot), -1, 52);
            }
        }

        /// <summary>关窗口但不触发"离开地点"。</summary>
        private void CloseWindowSilently(Window w)
        {
            w.OnClosed = null;
            CloseWindow(w);
        }

        private void BuildNeighbour(Window w, Place p)
        {
            Line(w.Content, $"邻居的店：{p.Business}");
            string size = p.Size == PCShopSell.Core.Shop.ShopSize.Large ? "大（96㎡）" : p.Size == PCShopSell.Core.Shop.ShopSize.Medium ? "中（36㎡）" : "小（12㎡）";
            Line(w.Content, "规格：" + size, 18, Ui.Dim);
            if (p.Business == "电脑") Line(w.Content, "同行。老板瞥了你一眼，没说话。", 18, Ui.Dim);
            else Line(w.Content, "生意不错。以后也许能在这里认识些人。", 18, Ui.Dim);
        }

        // ---------- 行情 ----------

        public void OpenMarketPanel()
        {
            S.ReadNews();
            OpenWindow("行情与新闻", 900, 880, w =>
            {
                Line(w.Content, "新闻 → 宏观指标 → 配件价格。读懂新闻，提前囤货或清货。", 18, Ui.Dim);
                foreach (MacroFactor f in new[] { MacroFactor.Crypto, MacroFactor.AiDemand, MacroFactor.NewGeneration })
                {
                    RectTransform row = Ui.Row(w.Content, 28);
                    double v = S.Market.FactorValue(f);
                    Ui.Size(Ui.Label(row, HardwareMarket.FactorName(f), 18), 120);
                    Ui.Bar(row, (float)(v + 1) / 2, v >= 0 ? Ui.Warn : Ui.Accent, 300, 14);
                    Ui.Size(Ui.Label(row, $"{v:+0.00;-0.00}", 17, TextAnchor.MiddleLeft, Ui.Dim), 80);
                }
                Line(w.Content, "品类价格指数（1.00 = 基准价）及近 30 天走势：", 18);
                foreach (PartCategory c in PartCatalog.Categories)
                {
                    RectTransform row = Ui.Row(w.Content, 30);
                    double idx = S.Market.Index(c);
                    Ui.Size(Ui.Label(row, PartCatalog.CategoryName(c), 18), 100);
                    Ui.Size(Ui.Label(row, $"{idx:0.00}", 19, TextAnchor.MiddleLeft, idx > 1.05 ? Ui.Warn : idx < 0.95 ? Ui.Good : Ui.TextColor), 70);
                    Ui.Size(Ui.Label(row, Sparkline(S.Market.History(c)), 18, TextAnchor.MiddleLeft, Ui.Dim), 520);
                }
                Line(w.Content, "最近新闻：", 18);
                RectTransform list = Ui.Scroll(w.Content, 150);
                foreach (NewsItem n in S.Market.News.Reverse().Take(20))
                {
                    Text t = Ui.Label(list, $"<color=#9AA3AD>{n.Date}</color>  {n.Headline}", 17);
                    Ui.Size(t, -1, 26);
                }
                if (S.Market.News.Count == 0) Ui.Size(Ui.Label(list, "暂时风平浪静。", 17, TextAnchor.MiddleLeft, Ui.Dim), -1, 26);
                BuildTipsSection(w);
            });
        }

        private static string Sparkline(System.Collections.Generic.IReadOnlyList<double> values)
        {
            const string blocks = "▁▂▃▄▅▆▇█";
            var sb = new StringBuilder();
            foreach (double v in values)
            {
                int i = Mathf.Clamp(Mathf.RoundToInt((float)((v - 0.6) / 1.2 * 7)), 0, 7);
                sb.Append(blocks[i]);
            }
            return sb.ToString();
        }

        // ---------- 菜单 ----------

        public void OpenSystemPanel()
        {
            OpenWindow("菜单", 560, 620, w =>
            {
                Line(w.Content, $"身家（现金 + 库存九折）：{S.NetWorth()}", 20);
                GoalLine goal = S.NextGoal();
                if (goal != null)
                {
                    Line(w.Content, $"下一个目标：{goal.Title}（第 {goal.Day} 天）", 17, Ui.Dim);
                    Line(w.Content, goal.Progress, 16, Ui.Dim);
                }
                Ui.Size(Ui.Button(w.Content, "目标与人脉", OpenGoalsPanel, 20, Ui.ButtonHot), -1, 48);
                Ui.Size(Ui.Button(w.Content, S.CanSave ? "存档" : "路上不能存档", () =>
                {
                    if (_ctrl.SaveGame()) Toast("已存档。");
                }, 20, Ui.ButtonHot, S.CanSave), -1, 48);
                Ui.Size(Ui.Button(w.Content, "读档", () => _ctrl.LoadGame(), 20, Ui.ButtonBg, SaveStore.Exists), -1, 48);
                Ui.Size(Ui.Button(w.Content, "重新开始", () => _ctrl.NewGame(), 20, Ui.Warn), -1, 48);
                Ui.Size(Ui.Button(w.Content, "玩法说明", ShowIntro, 20), -1, 48);
                Ui.Size(Ui.Button(w.Content, "账本（最近 30 条）", OpenLedger, 20), -1, 48);
                Line(w.Content, "快捷键：空格 暂停 · 1–4 倍速 · 滚轮 缩放 · Esc 关闭窗口", 16, Ui.Dim);
            });
        }

        private void OpenLedger()
        {
            OpenWindow("账本", 760, 680, w =>
            {
                RectTransform list = Ui.Scroll(w.Content, 560);
                var entries = S.Ledger.Entries;
                for (int i = entries.Count - 1; i >= 0 && i >= entries.Count - 30; i--)
                {
                    var e = entries[i];
                    Text t = Ui.Label(list, $"<color=#9AA3AD>{e.Date}</color>  {e.Category}  {(e.Amount.IsNegative ? "<color=#E0806A>" : "<color=#7CE38B>")}{e.Amount}</color>  {e.Memo}", 16);
                    Ui.Size(t, -1, 24);
                }
            });
        }

        public void ShowGameOver()
        {
            OpenWindow("游戏结束", 600, 340, w =>
            {
                Line(w.Content, S.GameOverReason, 21);
                Line(w.Content, $"坚持到了 {S.Clock.Today}。", 18, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, "重新开始", () => _ctrl.NewGame(), 20, Ui.ButtonHot), -1, 50);
                Ui.Size(Ui.Button(w.Content, "读档", () => _ctrl.LoadGame(), 20, Ui.ButtonBg, SaveStore.Exists), -1, 50);
            }, true, false);
        }

        public void ShowStartChoice()
        {
            OpenWindow("电脑城模拟器 VS1", 560, 360, w =>
            {
                Line(w.Content, "发现了存档。", 20);
                Ui.Size(Ui.Button(w.Content, "继续游戏", () =>
                {
                    w.Close();
                    _ctrl.LoadGame();
                }, 21, Ui.ButtonHot), -1, 54);
                Ui.Size(Ui.Button(w.Content, "新游戏", () =>
                {
                    w.Close();
                    ShowIntro();
                }, 21), -1, 54);
            }, true, false);
        }
    }
}
