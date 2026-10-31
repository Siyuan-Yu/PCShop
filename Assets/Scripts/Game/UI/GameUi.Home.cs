using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.World;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>
    /// 家里和家具相关的界面：床 / 衣柜 / 电饭锅窗口、“我的”信息菜单、网购、家具城、农贸市场、摆放模式面板、头顶的“可交互”提示。
    /// </summary>
    public sealed partial class GameUi
    {
        // ---------- 屋内小窗口 ----------

        /// <summary>在出租屋里打开的小窗口：不会离开屋子，关掉就回到屋里。</summary>
        private void OpenRoomWindow(string title, float w, float h, System.Action<Window> build)
        {
            OpenWindow(title, w, h, win =>
            {
                if (!_ctrl.InHome)
                {
                    win.Close();
                    return;
                }
                build(win);
                Ui.Size(Ui.Button(win.Content, "关闭", () => win.Close(), 20), -1, 46);
            }, true, true);
        }

        public void OpenBedWindow() => OpenRoomWindow(S.HomeHas(FurnitureRole.Bed) ? "床" : "地铺", 700, 360, BuildBed);

        public void OpenWardrobeWindow() => OpenRoomWindow("衣柜", 700, 640, BuildWardrobe);

        private AvatarPreview _avatarPreview;


        public void OpenStorageWindow() => OpenBoxWindow("纸箱", () => _ctrl.InHome);

        private string _boxSel, _boxSide;

        private static readonly Stock NoParts = new Stock(0);

        private System.Collections.Generic.List<BagStack> CollectBox() =>
            S.InPlayerShop ? CollectStacks(S.ShopLocker, NoParts) : CollectStacks(S.HomeBox, S.HomeBoxParts);

        private System.Collections.Generic.List<BagStack> CollectTemp() =>
            S.InPlayerShop ? CollectStacks(S.ShopTemp, NoParts) : CollectStacks(S.HomeTemp, S.HomeTempParts);

        /// <summary>店里的纸箱（走到纸箱前按 F，或右侧面板“库存”页、背包里打开）：吃的、礼品等放这里，配件放进来直接上货架。</summary>
        public void OpenShopLockerWindow() => OpenBoxWindow("店里纸箱", () => _ctrl.InInterior);

        /// <summary>纸箱窗口：临时存放里还有东西时关不掉（也没有右上角“关闭”、Esc 不管用）。</summary>
        private void OpenBoxWindow(string title, System.Func<bool> stillHere)
        {
            _boxSel = null;
            _boxSide = null;
            OpenWindow(title, 820, 880, win =>
            {
                if (!stillHere())
                {
                    win.Close();
                    return;
                }
                BuildStorage(win);
                bool stuck = S.HasTempHere;
                var close = Ui.Button(win.Content, stuck ? "临时存放还有东西：放进纸箱、拿进背包、叫货车搬走或扔掉，清空了才能关" : "关闭", () =>
                {
                    if (S.HasTempHere) win.Rebuild();
                    else win.Close();
                }, stuck ? 16 : 20, stuck ? Ui.ButtonBg : Ui.ButtonHot);
                close.name = "StorageClose";
                Ui.Size(close, -1, 46);
            }, true, false);
        }

        /// <summary>
        /// 储物处（家里 / 店里的纸箱）：最上面是临时存放（纸箱放不下的、要清空），然后是纸箱的格子，最下面是背包的格子；
        /// 点一格选中，再放进去 / 拿出来。
        /// </summary>
        private void BuildStorage(Window w)
        {
            bool shop = S.InPlayerShop;
            double boxKg = shop ? S.ShopLocker.WeightKg : S.HomeBoxKg;
            double boxLimit = shop ? S.ShopLockerLimitKg : S.HomeBoxLimitKg;
            int boxes = shop ? S.ShopBoxes : S.HomeBoxes;
            string each = $"每个纸箱 {GameSession.BoxCapacityKg * 2:0} 斤（{GameSession.BoxCapacityKg:0} 公斤）";
            Line(w.Content, boxes == 0
                ? $"这里一个纸箱都没摆，放不了东西。家具城有卖纸箱，{each}。"
                : $"{boxes} 个纸箱，{each}，一共能放 {boxLimit:0.#} 公斤，已经放了 {boxKg:0.#} 公斤。" + (shop ? "配件放进来直接上货架。" : "") + "吃的放这里隔天也会坏。", 16, Ui.Dim);
            Ui.Bar(w.Content, boxLimit <= 0 ? 1 : (float)(boxKg / boxLimit), boxKg > boxLimit * 0.85 ? Ui.Warn : Ui.Accent, 760, 10);

            bool hasTemp = S.HasTempHere;
            var tempStacks = hasTemp ? CollectTemp() : new System.Collections.Generic.List<BagStack>();
            if (hasTemp)
            {
                Line(w.Content, $"—— 临时存放 {S.TempKgHere:0.#} 公斤（送来的东西纸箱放不下，先堆在这里；清空了才能关窗口）——", 17, Ui.Warn);
                BagGrid(w.Content, tempStacks, 0, _boxSide == "temp" ? _boxSel : null, key =>
                {
                    _boxSide = "temp";
                    _boxSel = key;
                    w.Rebuild();
                }, 100);
                RectTransform tempBar = Ui.Row(w.Content, 40, 8);
                Ui.Size(Ui.Button(tempBar, "能放的都放进纸箱", () => { S.TempFillBox(); w.Rebuild(); }, 15, Ui.ButtonHot), 170);
                Ui.Size(Ui.Button(tempBar, "打电话叫货车搬走…", OpenTruckWindow, 15, Ui.ButtonBg), 170);
                Ui.Size(Ui.Button(tempBar, "全部扔掉…", () => ConfirmAction("全部扔掉", $"临时存放里 {S.TempKgHere:0.#} 公斤东西都扔掉，不退钱。", "扔掉", () =>
                {
                    S.TempDiscardAll();
                    w.Rebuild();
                }), 15, Ui.Warn), 120);
            }

            Line(w.Content, "—— 纸箱 ——", 17, Ui.Accent);
            var boxStacks = CollectBox();
            int boxCells = Mathf.CeilToInt((float)(boxLimit / Backpack.KgPerSpace));
            int usedCells = boxStacks.Sum(s => Mathf.Max(1, Mathf.CeilToInt((float)(s.Space - 1e-6))));
            BagGrid(w.Content, boxStacks, Mathf.Min(boxCells, usedCells + 16), _boxSide == "box" ? _boxSel : null, key =>
            {
                _boxSide = "box";
                _boxSel = key;
                w.Rebuild();
            }, hasTemp ? 182 : 260);

            Line(w.Content, $"—— 背包（负重 {S.CarriedKg:0.#}/{S.CarryLimitKg:0.#} 公斤）——", 17, Ui.Accent);
            var bagStacks = CollectBag();
            BagGrid(w.Content, bagStacks, Mathf.CeilToInt((float)S.CurrentVehicle.CargoCapacity), _boxSide == "bag" ? _boxSel : null, key =>
            {
                _boxSide = "bag";
                _boxSel = key;
                w.Rebuild();
            }, 182);

            var side = _boxSide == "temp" ? tempStacks : _boxSide == "box" ? boxStacks : bagStacks;
            BagStack pick = side.FirstOrDefault(s => s.Key == _boxSel);
            RectTransform bottom = Ui.Row(w.Content, 50);
            if (pick == null)
            {
                Ui.Size(Ui.Label(bottom, "点一格看详情。", 16, TextAnchor.MiddleLeft, Ui.Dim), -1, -1, 1);
                return;
            }
            Ui.Size(Ui.Label(bottom, $"<b>{pick.Name}</b> ×{pick.Count}　{pick.Kg:0.#} 公斤", 17), shop && pick.Key.StartsWith("gift:") ? 220 : 300, -1, 1);
            string key = pick.Key;
            int n = pick.Count;
            string one = key == "rice" ? "1 斤" : "一份";
            string unit = key == "rice" ? "斤" : "份";
            if (_boxSide == "temp")
            {
                bool toShelf = shop && key.StartsWith("part:");
                HoldButton(w, bottom, toShelf ? "上货架" : "放进纸箱", Ui.ButtonHot, n, unit, c => S.MoveTemp(key, (int)c, "box"), 110);
                HoldButton(w, bottom, "拿进背包", Ui.ButtonBg, n, unit, c => S.MoveTemp(key, (int)c, "bag"), 110);
                HoldButton(w, bottom, "扔掉", Ui.Warn, n, unit, c => S.MoveTemp(key, (int)c, "trash"), 80);
            }
            else if (_boxSide == "box")
            {
                Ui.Size(Ui.Button(bottom, "拿" + one, () => { S.TakeFromBox(key); w.Rebuild(); }, 16, Ui.ButtonHot), 100);
                Ui.Size(Ui.Button(bottom, "全拿出来", () => { S.TakeFromBox(key, n); w.Rebuild(); }, 16, Ui.ButtonBg), 110);
                if (shop && key.StartsWith("gift:"))
                {
                    string gid = key.Substring(5);
                    Ui.Size(Ui.Button(bottom, "上架卖", () => { S.ListGoods(gid, 1); w.Rebuild(); }, 16, Ui.ButtonHot), 80);
                    Ui.Size(Ui.Button(bottom, "全上架", () => { S.ListGoods(gid, n); w.Rebuild(); }, 16, Ui.ButtonBg), 80);
                }
            }
            else
            {
                bool toShelf = shop && key.StartsWith("part:");
                Ui.Size(Ui.Button(bottom, toShelf ? "上货架一件" : "放" + one, () => { S.StoreInBox(key); w.Rebuild(); }, 16, Ui.ButtonHot), 100);
                Ui.Size(Ui.Button(bottom, toShelf ? "全上货架" : "全放进去", () => { S.StoreInBox(key, n); w.Rebuild(); }, 16, Ui.ButtonBg), 110);
                if (shop && key.StartsWith("gift:"))
                {
                    string gid = key.Substring(5);
                    Ui.Size(Ui.Button(bottom, "上架卖", () => { S.ListGoods(gid, 1); w.Rebuild(); }, 16, Ui.ButtonHot), 80);
                    Ui.Size(Ui.Button(bottom, "全上架", () => { S.ListGoods(gid, n); w.Rebuild(); }, 16, Ui.ButtonBg), 80);
                }
            }
        }

        // ---------- 背包 ----------

        /// <summary>顶栏“背包”：身上带着什么、有多重、还能背多少。</summary>
        private Window _backpackWin;

        /// <summary>按 B：背包开着就关，没开就开（别的窗口开着时不理）。</summary>
        public void ToggleBackpack()
        {
            if (_backpackWin != null && !_backpackWin.Closed)
            {
                _backpackWin.Close();
                _backpackWin = null;
                return;
            }
            if (AnyModalOpen) return;
            OpenBackpackPanel();
        }

        /// <summary>背包里的一叠东西（占若干格）。</summary>
        private sealed class BagStack
        {
            public string Key, Name, Kind;
            public int Count;
            public double Kg, Space;
            public Color Color;
        }

        private string _bagSel;
        private const int BagColumns = 8;

        private System.Collections.Generic.List<BagStack> CollectBag() => CollectStacks(S.Backpack, S.Cargo);

        /// <summary>背包 / 纸箱里的东西摊成一叠一叠（配件、米菜、饭盒、礼品、家具）。</summary>
        private static System.Collections.Generic.List<BagStack> CollectStacks(Backpack bag, Stock parts)
        {
            var l = new System.Collections.Generic.List<BagStack>();
            foreach (StockLine s in parts.Lines())
            {
                double space = Stock.SpaceOf(s.Part, s.Boxed, s.Count);
                l.Add(new BagStack
                {
                    Key = "part:" + s.Part.Id + (s.Boxed ? ":b" : ":l"), Name = s.Part.Name + (s.Boxed ? "（整盒）" : "（散件）"), Kind = "进货的配件",
                    Count = s.Count, Space = space, Kg = space * Backpack.KgPerSpace, Color = new Color(0.20f, 0.45f, 0.62f),
                });
            }
            if (bag.Food.RiceJin > 0)
                l.Add(new BagStack { Key = "rice", Name = "大米（斤）", Kind = "买的米和菜", Count = (int)System.Math.Ceiling(bag.Food.RiceJin), Kg = bag.Food.RiceJin * Backpack.RiceKgPerJin, Space = bag.Food.RiceJin * Backpack.RiceKgPerJin / Backpack.KgPerSpace, Color = new Color(0.30f, 0.55f, 0.30f) });
            foreach (var g in bag.Food.Ingredient.GroupBy(b => b.Id))
            {
                int n = g.Sum(b => b.Count);
                double kg = n * Backpack.IngredientKg(g.Key);
                l.Add(new BagStack { Key = "ing:" + g.Key, Name = g.First().Name, Kind = "买的米和菜", Count = n, Kg = kg, Space = kg / Backpack.KgPerSpace, Color = new Color(0.30f, 0.55f, 0.30f) });
            }
            if (bag.Lunchboxes > 0)
            {
                double kg = bag.Lunchboxes * Backpack.LunchboxKg;
                l.Add(new BagStack { Key = "lunch", Name = "饭盒", Kind = "从电饭锅装的饭（饭点自动吃，也可以现在吃一个）", Count = bag.Lunchboxes, Kg = kg, Space = kg / Backpack.KgPerSpace, Color = new Color(0.62f, 0.40f, 0.30f) });
            }
            foreach (var kv in bag.GiftLines)
            {
                GiftDef gd = Gifts.Get(kv.Key);
                double kg = gd.WeightKg * kv.Value;
                l.Add(new BagStack { Key = "gift:" + kv.Key, Name = gd.Name, Kind = "礼品", Count = kv.Value, Kg = kg, Space = kg / Backpack.KgPerSpace, Color = new Color(0.70f, 0.52f, 0.18f) });
            }
            foreach (var kv in bag.FurnitureLines)
            {
                FurnitureDef fd = FurnitureCatalog.Get(kv.Key);
                double kg = fd.WeightKg * kv.Value;
                l.Add(new BagStack { Key = "furn:" + kv.Key, Name = fd.Name, Kind = "背着的家具（回家或回店里放进家具背包）", Count = kv.Value, Kg = kg, Space = kg / Backpack.KgPerSpace, Color = new Color(0.50f, 0.34f, 0.24f) });
            }
            return l;
        }

        private static string ShortName(string name)
        {
            int p = name.IndexOf('（');
            if (p > 0) name = name.Substring(0, p);
            return name.Length > 5 ? name.Substring(0, 5) : name;
        }

        /// <summary>一格一格的背包格子：8 列，一叠东西占几格就画几格。</summary>
        private RectTransform BagGrid(Transform parent, System.Collections.Generic.List<BagStack> stacks, int capacity, string selKey, System.Action<string> onPick, float height)
        {
            var cells = new System.Collections.Generic.List<(BagStack st, int n)>();
            int used = 0;
            foreach (BagStack st in stacks)
            {
                int c = Mathf.Max(1, Mathf.CeilToInt((float)(st.Space - 1e-6)));
                int left = st.Count;
                for (int i = 0; i < c; i++)
                {
                    int n = Mathf.CeilToInt(left / (float)(c - i));
                    left -= n;
                    cells.Add((st, n));
                }
                used += c;
            }
            int total = Mathf.Max(capacity, used);
            total = (total + BagColumns - 1) / BagColumns * BagColumns;

            RectTransform grid = Ui.Scroll(parent, height);
            for (int r = 0; r < total / BagColumns; r++)
            {
                RectTransform row = Ui.Row(grid, 82, 6);
                for (int c = 0; c < BagColumns; c++)
                {
                    int idx = r * BagColumns + c;
                    if (idx < cells.Count)
                    {
                        BagStack st = cells[idx].st;
                        bool sel = st.Key == selKey;
                        Color bg = sel ? Color.Lerp(st.Color, Color.white, 0.35f) : st.Color;
                        string label = cells[idx].n > 0 ? $"<size=15>{ShortName(st.Name)}</size>\n<size=14>×{cells[idx].n}</size>" : "<size=22>…</size>";
                        Ui.Size(Ui.Button(row, label, () => onPick(st.Key), 14, bg), 80, 80);
                    }
                    else
                    {
                        bool beyond = idx >= capacity;
                        Image empty = Ui.Panel(row, "Empty", beyond ? new Color(0.35f, 0.12f, 0.12f, 0.6f) : new Color(1, 1, 1, 0.07f));
                        Ui.Size(empty, 80, 80);
                    }
                }
            }
            return grid;
        }

        /// <summary>顶栏“背包”（B）：一格一格的格子，1 格 = 2 公斤，格子数就是载具的载货容量。</summary>
        public void OpenBackpackPanel()
        {
            _bagSel = null;
            _backpackWin = OpenWindow("背包", 760, 700, w =>
            {
                double carried = S.CarriedKg, limit = S.CarryLimitKg;
                bool heavy = carried > limit * 0.85;
                Line(w.Content, $"{S.CurrentVehicle.Name}　负重 {carried:0.#}/{limit:0.#} 公斤（1 格 = {Backpack.KgPerSpace:0.#} 公斤）", 17, heavy ? Ui.Warn : Ui.Dim);
                Ui.Bar(w.Content, (float)(limit <= 0 ? 0 : carried / limit), heavy ? Ui.Warn : Ui.Accent, 700, 10);

                var stacks = CollectBag();
                BagGrid(w.Content, stacks, Mathf.CeilToInt((float)S.CurrentVehicle.CargoCapacity), _bagSel, key =>
                {
                    _bagSel = key;
                    w.Rebuild();
                }, 360);

                BagStack pick = stacks.FirstOrDefault(s => s.Key == _bagSel);
                if (pick != null)
                    Line(w.Content, $"<b>{pick.Name}</b> ×{pick.Count}　占 {Mathf.Max(1, Mathf.CeilToInt((float)(pick.Space - 1e-6)))} 格　{pick.Kg:0.#} 公斤\n<color=#ABB5A8>{pick.Kind}</color>", 17);
                else
                    Line(w.Content, stacks.Count == 0 ? "背包是空的。去买点东西：礼品在烟酒茶行 / 农贸市场 / 手机网购，进货在电子市场。" : "点一格看详情。", 17, Ui.Dim);
                Line(w.Content, $"每天 12:00、18:30 饿两次，饭点先吃带着的饭盒（在家吃锅里的）。现在带着 {S.Backpack.Lunchboxes} 盒。", 15, S.Backpack.Lunchboxes < 2 ? Ui.Warn : Ui.Dim);

                RectTransform acts = Ui.Row(w.Content, 46);
                if (S.Backpack.Lunchboxes > 0)
                    Ui.Size(Ui.Button(acts, "吃一个饭盒", () =>
                    {
                        S.EatLunchboxNow();
                        w.Rebuild();
                    }, 18), 180);
                string target = S.UnloadTarget;
                if (target != null)
                {
                    bool can = S.HasUnloadable;
                    if (can)
                        Ui.Size(Ui.Button(acts, "一键全放到" + target, () =>
                        {
                            S.UnloadBackpack();
                            _bagSel = null;
                            w.Rebuild();
                        }, 18, Ui.ButtonHot), 260);
                    else
                        // 没东西可放就不摆一个点不动的灰按钮，直接说为什么
                        Ui.Size(Ui.Label(acts, S.InPlayerShop
                            ? "没有要放下的东西（饭盒一直带在身上）。"
                            : "没有要放在家里的东西：饭盒一直带在身上（饭点自动吃），配件要拿到店里。", 14, TextAnchor.MiddleLeft, Ui.Dim), S.InPlayerShop ? 260 : 440);
                    if (S.InPlayerShop)
                        Ui.Size(Ui.Button(acts, "店里纸箱", () =>
                        {
                            w.Close();
                            OpenShopLockerWindow();
                        }, 17, Ui.ButtonBg), 160);
                }
                else
                    Ui.Size(Ui.Label(acts, "回到自己的店或家里，才能把东西放下。", 15, TextAnchor.MiddleLeft, Ui.Dim), 360);
                Ui.Size(Ui.Button(w.Content, "关闭", w.Close, 20), -1, 46);
            });
        }
        public void OpenRepairWindow() => OpenRoomWindow("约师傅上门修", 820, 640, BuildRepair);

        private int _repairDay;

        private void BuildRepair(Window w)
        {
            Line(w.Content, $"约一个时间，到点师傅上门，上门时人要在家，没人就白跑一趟。晚上（18:00 – 次日 6:00）上门加收 50%；至少提前 1 小时约。下单就付上门费，取消或白跑都不退（黑心，约了就别放鸽子）。师傅进门要干约 {GameSession.RepairWorkMinutes} 分钟。", 15, Ui.Dim);
            var broken = S.BrokenHomeItems().ToList();
            if (broken.Count == 0) Line(w.Content, "屋里的东西都是好的。", 18);
            foreach (string id in broken)
            {
                string targetId = id;
                FurnitureDef d = FurnitureCatalog.Get(targetId);
                Line(w.Content, $"{d.Name}  →  修好后：{FurnitureCatalog.Get(d.RepairInto).Name}", 19, Ui.Accent);
                HomeVisit v = S.VisitFor(targetId);
                if (v != null && v.Working)
                {
                    Line(w.Content, $"师傅正在屋里修，还要 {S.RepairWorkMinutesLeft(v)} 分钟。别打扰他，修好他就走。", 18, Ui.Warn);
                    continue;
                }
                if (v != null)
                {
                    RectTransform vrow = Ui.Row(w.Content, 50);
                    int vd = (int)(v.StartMinute / 1440);
                    Ui.Size(Ui.Label(vrow, $"已约：{S.DescribeDay(vd)} {GameSession.ClockText((int)(v.StartMinute % 1440))}，{v.Price}（已付，要在家）", 18, TextAnchor.MiddleLeft, Ui.Good), 480);
                    Ui.Size(Ui.Button(vrow, "取消（不退款）", () =>
                    {
                        S.CancelRepair(targetId);
                        w.Rebuild();
                    }, 15, Ui.Warn), 150);
                    continue;
                }

                RectTransform drow = Ui.Row(w.Content, 44);
                for (int off = 0; off < GameSession.RepairBookDays; off++)
                {
                    int o = off;
                    Ui.Size(Ui.Button(drow, S.DescribeDay(S.Clock.Today.TotalDays + o), () =>
                    {
                        _repairDay = o;
                        w.Rebuild();
                    }, 17, _repairDay == o ? Ui.ButtonHot : Ui.ButtonBg), 110);
                }
                RectTransform hrow = null;
                int col = 0;
                for (int hour = GameSession.RepairFirstHour; hour <= GameSession.RepairLastHour; hour++)
                {
                    if (col == 0) hrow = Ui.Row(w.Content, 46);
                    int hh = hour;
                    long start = (long)(S.Clock.Today.TotalDays + _repairDay) * 1440 + hh * 60;
                    bool ok = S.RepairBookBlocker(targetId, start) == null && S.Ledger.Balance >= S.RepairPrice(d, start);
                    bool night = GameSession.IsRepairNightMinute(hh * 60);
                    Ui.Size(Ui.Button(hrow, $"{hh:00}:00\n<size=13>{S.RepairPrice(d, start)}{(night ? " 夜" : "")}</size>", () =>
                    {
                        S.BookRepair(targetId, start);
                        w.Rebuild();
                    }, 16, night ? Ui.Warn : Ui.ButtonBg, ok), 120);
                    col = (col + 1) % 6;
                }
            }
        }

        // ---------- 日程 ----------

        public void OpenSchedulePanel()
        {
            if (!S.HasCalendar)
            {
                OpenNotebook();
                return;
            }
            const bool calendar = true;
            OpenWindow("日历", 760, 640, w =>
            {
                Line(w.Content, $"现在是 {S.Clock.Today} {GameSession.ClockText(S.Clock.MinuteOfDay)}。约会、师傅上门前 {GameSession.ReminderLeadMinutes} 分钟会提醒你。", 17, Ui.Dim);
                var entries = S.Schedule();
                if (entries.Count == 0)
                {
                    Line(w.Content, "最近没有安排。约朋友、约师傅上门修东西、网购的快递、焖饭，都会出现在这里。", 17, Ui.Dim);
                    return;
                }
                RectTransform list = Ui.Scroll(w.Content, 520);
                string lastDay = null;
                foreach (ScheduleEntry e in entries)
                {
                    if (e.Day != lastDay)
                    {
                        lastDay = e.Day;
                        Line(list, "—— " + DateLabel((int)(e.Minute / PCShopSell.Core.Time.GameTimestamp.MinutesPerDay)) + " ——", 18, Ui.Accent);
                    }
                    RectTransform row = Ui.Row(list, 44);
                    string color = e.Kind == "appointment" ? "#DDC18B" : (e.Kind == "repair" || e.Kind == "repairing") ? "#A9C9A9" : "#C8D0DA";
                    Ui.Size(Ui.Label(row, e.HasTime ? e.TimeText : "全天", 18, TextAnchor.MiddleLeft, Ui.Dim), 80);
                    Ui.Size(Ui.Label(row, $"<color={color}>{e.Text}</color>", 17), e.Kind == "repair" ? 480 : 600);
                    if (e.Kind == "repair")
                    {
                        string target = S.HomeVisits.Count > 0 ? S.HomeVisits[0].TargetId : null;
                        Ui.Size(Ui.Button(row, "取消（不退款）", () =>
                        {
                            if (target != null) S.CancelRepair(target);
                            w.Rebuild();
                        }, 16, Ui.Warn), 100);
                    }
                }
            });
        }

        // ---------- 笔记本（没智能手机时的日程）----------

        /// <summary>笔记本只记最近几天（含今天）；日历 app 能看到以后所有的。</summary>
        private const int NotebookDays = 5;

        /// <summary>“1月2日 周二（今天）”。</summary>
        private string DateLabel(int totalDays)
        {
            var date = new PCShopSell.Core.Time.GameDate(totalDays);
            int delta = totalDays - S.Clock.Today.TotalDays;
            string rel = delta == 0 ? "（今天）" : delta == 1 ? "（明天）" : delta == 2 ? "（后天）" : "";
            return $"{date.Month}月{date.Day}日 {Ui.WeekdayName(date.Weekday)}{rel}";
        }

        private static readonly Color Paper = new Color(0.95f, 0.91f, 0.80f);
        private static readonly Color Ink = new Color(0.16f, 0.20f, 0.40f);
        private static readonly Color InkRed = new Color(0.62f, 0.20f, 0.16f);
        private static readonly Color PaperLine = new Color(0.55f, 0.65f, 0.80f, 0.55f);

        /// <summary>笔记本：约好的事自动记在本子上，纸张、横线、红边线、螺旋圈，没有提醒。</summary>
        public void OpenNotebook()
        {
            Window win = OpenWindow("笔记本", 720, 660, w =>
            {
                Image paper = Ui.Panel(w.Content, "Paper", Paper);
                Ui.Size(paper, -1, -1).gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
                RectTransform prt = paper.rectTransform;
                var v = Ui.VList(prt, 2, 0);
                v.padding = new RectOffset(70, 22, 16, 14);

                // 螺旋圈和红色边线（不参与布局）
                for (int i = 0; i < 12; i++)
                {
                    Image ring = Ui.Panel(prt, "Ring", new Color(0.35f, 0.35f, 0.38f));
                    ring.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                    RectTransform rr = ring.rectTransform;
                    rr.anchorMin = rr.anchorMax = new Vector2(0, 1);
                    rr.pivot = new Vector2(0.5f, 0.5f);
                    rr.sizeDelta = new Vector2(30, 8);
                    rr.anchoredPosition = new Vector2(8, -34 - i * 44);
                }
                Image margin = Ui.Panel(prt, "Margin", new Color(0.80f, 0.30f, 0.28f, 0.7f));
                margin.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                margin.rectTransform.Anchor(0, 0, 0, 1, 56, 0, 0, 0);
                margin.rectTransform.sizeDelta = new Vector2(2, 0);

                Ui.Size(Ui.Label(prt, "最近几天的安排，都自动记在这儿了。", 22, TextAnchor.MiddleLeft, Ink), -1, 36);
                Ui.Size(Ui.Label(prt, "（本子不会提醒你，得自己翻。买了智能手机，日历里能看到以后所有的安排，还会提醒。）", 14, TextAnchor.MiddleLeft, new Color(Ink.r, Ink.g, Ink.b, 0.6f)), -1, 26);
                RuledLine(prt);

                var entries = S.Schedule();
                RectTransform list = Ui.Scroll(prt, 420);
                Image sbg = list.parent.parent.GetComponent<Image>();
                if (sbg != null) sbg.color = new Color(0, 0, 0, 0);
                for (int d = 0; d < NotebookDays; d++)
                {
                    int day = S.Clock.Today.TotalDays + d;
                    RectTransform dayRow = Ui.Row(list, 40);
                    Ui.Size(Ui.Label(dayRow, "<b>" + DateLabel(day) + "</b>", 20, TextAnchor.MiddleLeft, InkRed), -1, -1, 1);
                    RuledLine(dayRow);
                    var today = entries.Where(e => e.Minute / PCShopSell.Core.Time.GameTimestamp.MinutesPerDay == day).ToList();
                    if (today.Count == 0)
                    {
                        RectTransform empty = Ui.Row(list, 36);
                        Ui.Size(Ui.Label(empty, "没安排", 16, TextAnchor.MiddleLeft, new Color(Ink.r, Ink.g, Ink.b, 0.45f)), -1, -1, 1);
                        RuledLine(empty);
                        continue;
                    }
                    foreach (ScheduleEntry e in today)
                    {
                        RectTransform row = Ui.Row(list, 40);
                        Ui.Size(Ui.Label(row, e.HasTime ? e.TimeText : "当天", 18, TextAnchor.MiddleLeft, InkRed), 70);
                        Ui.Size(Ui.Label(row, e.Text, 18, TextAnchor.MiddleLeft, Ink), e.Kind == "repair" ? 400 : 460, -1, 1);
                        if (e.Kind == "repair")
                        {
                            string target = S.HomeVisits.Count > 0 ? S.HomeVisits[0].TargetId : null;
                            Ui.Size(Ui.Button(row, "划掉（不退款）", () =>
                            {
                                if (target != null) S.CancelRepair(target);
                                w.Rebuild();
                            }, 13, new Color(0.62f, 0.30f, 0.24f)), 112);
                        }
                        RuledLine(row);
                    }
                }
                Ui.Size(Ui.Label(prt, $"（本子只记了最近 {NotebookDays} 天）", 13, TextAnchor.MiddleRight, new Color(Ink.r, Ink.g, Ink.b, 0.5f)), -1, 22);            });
            // 牛皮封面
            Transform panel = win.Root.Find("Panel");
            if (panel != null)
            {
                panel.GetComponent<Image>().color = new Color(0.36f, 0.24f, 0.16f, 0.98f);
                Transform bar = panel.Find("TitleBar");
                if (bar != null) bar.GetComponent<Image>().color = new Color(0.26f, 0.17f, 0.11f, 0.98f);
            }
        }

        private static void RuledLine(Transform parent)
        {
            Image line = Ui.Panel(parent, "Rule", PaperLine);
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            line.rectTransform.Anchor(0, 0, 1, 0, 0, 0, 0, -1);
        }

        public void OpenCookerWindow() => OpenRoomWindow("电饭锅", 760, 640, BuildCooker);

        private void BuildBed(Window w)
        {
            PlayerStats st = S.Player;
            Line(w.Content, $"精力 {st.Energy:0}/{st.MaxEnergy:0}  欠觉 {st.SleepDebt:0.#} 小时  睡眠恢复 ×{S.HomeSleepQuality:0.##}", 18, Ui.Dim);
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
            if (!S.HomeHas(FurnitureRole.Bed)) Line(w.Content, "屋里没有床，只能睡地上，恢复得慢。去家具城买，或者网购一张。", 16, Ui.Warn);
            else Line(w.Content, "想换更好的床：去家具城买（当天带回），或者网购（2 天到），再在“我的”里摆放。", 16, Ui.Dim);
        }

        private void BuildWardrobe(Window w)
        {
            // 对着镜子看自己：按当前穿的衣服渲染一张形象图
            _avatarPreview = _avatarPreview ?? new AvatarPreview();
            _avatarPreview.Render(S.EquippedOutfit);
            RectTransform mirror = Ui.Row(w.Content, 330, 16);
            var img = Ui.Rect(mirror, "Mirror").gameObject.AddComponent<RawImage>();
            img.texture = _avatarPreview.Texture;
            img.raycastTarget = false;
            Ui.Size(img, 260, 330);
            Ui.Size(Ui.Label(mirror,
                $"<size=22><b>镜子里的自己</b></size>\n\n穿着：<b>{S.Outfit.Name}</b>\n{S.Outfit.Description}\n\n当前魅力 {S.EffectiveCharm}，其中衣着加成 +{S.Outfit.CharmBonus}。\n出门谈事、进档口，别人看到的就是这个样子。", 17, TextAnchor.MiddleLeft), 340, -1, 1);
            Line(w.Content, "柜子里的衣服：", 18);
            foreach (OutfitDef def in Outfits.All)
            {
                OutfitDef o = def;
                if (!S.OwnedOutfits.Contains(o.Id)) continue;
                bool worn = S.EquippedOutfit == o.Id;
                RectTransform orow = Ui.Row(w.Content, 40);
                Ui.Size(Ui.Label(orow, $"{o.Name}  魅力 +{o.CharmBonus}", 17, TextAnchor.MiddleLeft, worn ? Ui.Good : (Color?)null), 420);
                Ui.Size(Ui.Button(orow, worn ? "穿着中" : "换上", () =>
                {
                    S.Equip(o.Id);
                    w.Rebuild();
                }, 17, Ui.ButtonHot, !worn), 120);
            }
            Line(w.Content, "没有的衣服：网购（2 天到）。手机里下载网购 app 就能买。", 16, Ui.Dim);
        }

        // ---------- 电饭锅：真做饭 ----------

        private double _cookJin = 1;
        private string _cookIngredient;

        private void BuildCooker(Window w)
        {
            S.RefreshCooking();
            Pantry pan = S.Pantry;
            PlayerStats st = S.Player;
            Line(w.Content, $"饥饿 {st.Hunger:0}  健康 {st.Health:0}   米缸：{pan.RiceJin:0.#} 斤（1 斤约 {Pantry.BowlsPerJin} 碗）   背包里的饭盒：{S.Backpack.Lunchboxes} 盒", 17);

            // 正在焖：只显示进度，什么都不能动
            if (pan.Cooking != null)
            {
                long left = pan.CookMinutesLeft(S.Clock.Now.TotalMinutes);
                int readyMinuteOfDay = (int)(pan.Cooking.ReadyMinute % (24 * 60));
                Line(w.Content, $"<color=#DDC18B>正在焖饭：{pan.Cooking.Batch.Name}，约 {pan.Cooking.Batch.Count} 碗</color>", 20);
                Line(w.Content, $"还剩 {left} 分钟（{readyMinuteOfDay / 60:00}:{readyMinuteOfDay % 60:00} 好）。焖好之前锅盖打不开，你可以先去干别的。", 17);
                return;
            }

            // 焖好了：取饭（吃、装饭盒）；取完才能再焖
            if (pan.CookedBowls > 0)
            {
                Line(w.Content, $"锅里焖好的饭：{pan.CookedBowls} 碗（没有冰箱，放不过明天）", 18);
                foreach (FoodBatch b in pan.Cooked) Line(w.Content, $"· {b.Name} ×{b.Count}  （放到第 {b.ExpireDay} 天前吃完）", 15, Ui.Dim);
                RectTransform erow = Ui.Row(w.Content, 46);
                Ui.Size(Ui.Button(erow, $"吃一碗（{Pantry.EatMinutes} 分钟）", () =>
                {
                    S.EatBowlAtHome();
                    w.Rebuild();
                }, 18, Ui.ButtonHot, pan.CookedBowls > 0), 220);
                Ui.Size(Ui.Button(erow, "装 1 个饭盒", () =>
                {
                    S.PackLunch(1);
                    w.Rebuild();
                }, 18, Ui.ButtonBg, pan.CookedBowls > 0), 170);
                Ui.Size(Ui.Button(erow, "全部装饭盒", () =>
                {
                    S.PackLunch(pan.CookedBowls);
                    w.Rebuild();
                }, 18, Ui.ButtonBg, pan.CookedBowls > 0), 170);
                Line(w.Content, $"饭盒装进背包（每盒 {Backpack.LunchboxKg:0.#} 公斤，算负重）。每天 12:00、18:30 饿两次，出门要带够饭盒，不然只能饿着。锅里的饭取完才能再焖新的一锅。", 15, Ui.Dim);
                return;
            }

            // 空锅：选米、选配菜、开始焖
            var ings = pan.Ingredient.GroupBy(b => b.Id).ToList();
            Line(w.Content, ings.Count == 0 ? "没有别的食材（农贸市场 6:00–18:00 有卖，没有冰箱放不久）。" : "食材：" + string.Join("、", ings.Select(g => $"{g.First().Name}×{g.Sum(b => b.Count)}")), 16, Ui.Dim);

            if (_cookJin > pan.RiceJin) _cookJin = pan.RiceJin;
            if (_cookJin < 0.5 && pan.RiceJin >= 0.5) _cookJin = 0.5;
            Line(w.Content, "往锅里加多少米：", 17);
            RectTransform jrow = Ui.Row(w.Content, 44);
            foreach (double amount in new[] { 0.5, 1, 2, 3, 5 })
            {
                double a = amount;
                Ui.Size(Ui.Button(jrow, a + " 斤", () =>
                {
                    _cookJin = a;
                    w.Rebuild();
                }, 17, System.Math.Abs(_cookJin - a) < 1e-6 ? Ui.ButtonHot : Ui.ButtonBg, pan.RiceJin >= a), 90);
            }

            Line(w.Content, "加点什么一起焖（要的份数等于米的斤数向上取整）：", 17);
            RectTransform irow = Ui.Row(w.Content, 44);
            Ui.Size(Ui.Button(irow, "白饭", () =>
            {
                _cookIngredient = null;
                w.Rebuild();
            }, 17, _cookIngredient == null ? Ui.ButtonHot : Ui.ButtonBg), 100);

            foreach (IngredientDef def in Ingredients.All)
            {
                IngredientDef d = def;
                int have = pan.IngredientCount(d.Id);
                Ui.Size(Ui.Button(irow, $"{d.DishName}（{have}）", () =>
                {
                    _cookIngredient = d.Id;
                    w.Rebuild();
                }, 15, _cookIngredient == d.Id ? Ui.ButtonHot : Ui.ButtonBg, have > 0), 150);
            }

            int bowls = Mathf.RoundToInt((float)(_cookJin * Pantry.BowlsPerJin));
            Ui.Size(Ui.Button(w.Content, $"开始焖饭：{_cookJin:0.#} 斤，约 {bowls} 碗，{Pantry.CookMinutes} 分钟后好", () =>
            {
                if (S.CookRice(_cookJin, _cookIngredient)) Toast($"开始焖饭，{Pantry.CookMinutes} 分钟后好。");
                w.Rebuild();
            }, 19, Ui.ButtonHot, pan.RiceJin >= 0.5 && _cookJin >= 0.5), -1, 48);
            Line(w.Content, "点了就开始焖，焖的时候锅里不能动，人可以先去忙别的。", 15, Ui.Dim);
        }

        // ---------- “我的” ----------

        public void OpenMyPanel()
        {
            OpenWindow("我的", 860, 860, w =>
            {
                RectTransform actions = Ui.Row(w.Content, 50);
                Ui.Size(Ui.Button(actions, "摆放家具（家里）", () => StartPlacement(false), 18, Ui.ButtonHot, _ctrl.InHome), 230);
                Ui.Size(Ui.Button(actions, "装修店面（歇业一天）", () => StartPlacement(true), 18, Ui.ButtonBg, _ctrl.InInterior), 250);
                if (!_ctrl.InHome && !_ctrl.InInterior) Line(w.Content, "摆放家具要在家里；改造店面要在自己的店里。", 15, Ui.Dim);

                RectTransform list = Ui.Scroll(w.Content, 660);
                PlayerStats st = S.Player;
                Line(list, "—— 我 ——", 18, Ui.Accent);
                Line(list, $"饥饿 {st.Hunger:0}  精力 {st.Energy:0}/{st.MaxEnergy:0}  健康 {st.Health:0}  欠觉 {st.SleepDebt:0.#} 小时", 17, Ui.Dim);
                Line(list, $"智力 {st.Intelligence}  魅力 {S.EffectiveCharm}（衣着 +{S.Outfit.CharmBonus}）  技术 {st.Skill}   现金 {S.Ledger.Balance}", 17, Ui.Dim);
                Line(list, "每天饿两次（12:00 / 18:30）：在家吃锅里的饭，出门吃背包里的饭盒；都没有就饿着。", 15, Ui.Dim);

                Ui.Size(Ui.Button(list, S.MealDeliveryEnabled ? "缺饭时自动送餐：开（每餐 ¥25）" : "缺饭时自动送餐：关（需智能手机）", () => { S.ToggleMealDelivery(); w.Rebuild(); }, 17, Ui.ButtonBg, S.HasSmartphone), -1, 38);
                if (S.HomeVisits.Any(v => v.Working))
                    Ui.Size(Ui.Button(list, "在家等师傅修完", () => { S.WaitForRepair(); w.Rebuild(); }, 17, Ui.ButtonHot, _ctrl.InHome), -1, 38);
                Line(list, "—— 出租屋 ——", 18, Ui.Accent);
                Line(list, $"月租 {S.CurrentHomeRent}；睡眠恢复 ×{S.HomeSleepQuality:0.##}；" + (S.HomeUtilities.IsPositive ? $"水电 {S.HomeUtilities}/月" : "不用交水电（没有电器）"), 17);
                string placed = string.Join("、", S.HomeLayout.GroupBy(p => p.Id).Select(g => FurnitureLabel(g.Key, g.Count())));
                Line(list, "屋里摆着：" + (placed.Length == 0 ? "什么都没有" : placed), 16, Ui.Dim);

                Line(list, "—— 背包 ——", 18, Ui.Accent);
                Line(list, $"负重 {S.CarriedKg:0.#}/{S.CarryLimitKg:0.#} 公斤（{S.CurrentVehicle.Name}；进货的货也算）。" + (S.HasSmartphone ? "" : "只有按键机，不能网购、上 1788；去手机店买智能手机。"), 16, S.CarriedKg > S.CarryLimitKg * 0.85 ? Ui.Warn : Ui.Dim);
                Backpack bag = S.Backpack;
                var bagIngs = bag.Food.Ingredient.GroupBy(b => b.Id).ToList();
                string bagFood = (bag.Food.RiceJin > 0 ? $"大米 {bag.Food.RiceJin:0.#} 斤" : "") + (bagIngs.Count > 0 ? "  " + string.Join("、", bagIngs.Select(g => $"{g.First().Name}×{g.Sum(b => b.Count)}")) : "");
                Line(list, "买的菜米：" + (bagFood.Trim().Length == 0 ? "没有" : bagFood.Trim()) + "（回家打开背包一键放进厨房）", 16);
                Line(list, $"饭盒：{bag.Lunchboxes} 盒", 16, bag.Lunchboxes < 2 ? Ui.Warn : Ui.TextColor);
                var bagFurn = bag.FurnitureLines.ToList();
                if (bagFurn.Count > 0) Line(list, "背着的家具：" + string.Join("、", bagFurn.Select(kv => FurnitureLabel(kv.Key, kv.Value))) + "（回家或回店里打开背包一键放下）", 16);
                Line(list, "礼品：" + (bag.GiftLines.Count() == 0 ? "没有。去烟酒茶行、农贸市场买，或者网购。" : string.Join("、", bag.GiftLines.Select(kv => $"{Gifts.Get(kv.Key).Name}×{kv.Value}"))), 16);

                Line(list, "—— 吃的 ——", 18, Ui.Accent);
                Pantry pan = S.Pantry;
                Line(list, $"大米 {pan.RiceJin:0.#} 斤   锅里做好的饭 {pan.CookedBowls} 碗", 17);
                if (pan.Cooking != null) Line(list, $"电饭锅正在焖饭，还剩 {pan.CookMinutesLeft(S.Clock.Now.TotalMinutes)} 分钟。", 16, Ui.Warn);
                var ings = pan.Ingredient.GroupBy(b => b.Id).ToList();
                Line(list, "食材：" + (ings.Count == 0 ? "没有" : string.Join("、", ings.Select(g => $"{g.First().Name}×{g.Sum(b => b.Count)}"))) + "。没有冰箱，放不久；去农贸市场买。", 15, Ui.Dim);

                Line(list, "—— 家具背包 ——", 18, Ui.Accent);
                var stock = S.FurnitureStock.ToList();
                if (stock.Count == 0) Line(list, "空的。买来的家具会放在这里，摆出来才算。", 15, Ui.Dim);
                foreach (var kv in stock) Line(list, "· " + FurnitureLabel(kv.Key, kv.Value) + (FurnitureCatalog.Get(kv.Key).ForShop ? "（店里用）" : "（家里用）"), 16);

                Line(list, "—— 在路上的包裹 ——", 18, Ui.Accent);
                if (S.Parcels.Count == 0) Line(list, "没有。网购 " + GameSession.DeliveryDays + " 天送到家；家具城叫的货车明早到。", 15, Ui.Dim);
                foreach (Parcel p in S.Parcels)
                {
                    int left = Mathf.Max(0, p.DueDay - S.Clock.Today.TotalDays);
                    Line(list, $"· {ParcelName(p)}，还要 {left} 天", 16);
                }
                foreach (CourierRun r in S.Couriers)
                {
                    long left = System.Math.Max(0, r.DueMinute - S.Clock.Now.TotalMinutes);
                    Line(list, $"· 叫的车：从{r.FromName}送到{S.CourierRunDestName(r)}，{r.Items.Count} 样东西，约 {left} 分钟到", 16);
                }

                Line(list, "—— 店里的摆设 ——", 18, Ui.Accent);
                Line(list, $"装饰分 {S.ShopDecorAppeal:0}，客流 ×{S.ShopDecorTrafficMultiplier:0.00}，顾客多等 {S.ShopDecorPatience} 分钟。摆放店里的东西要歇业装修一天。", 16);
                if (S.HasShop) Para(list, FitChecklistText(), 15);
                if (S.IsRenovating) Line(list, "今天正在装修，店不营业。", 16, Ui.Warn);
            });
        }

        private static string FurnitureLabel(string id, int count)
        {
            FurnitureDef d = FurnitureCatalog.Get(id);
            string n = d == null ? id : d.Name;
            return count > 1 ? $"{n}×{count}" : n;
        }

        private static string ParcelName(Parcel p)
        {
            switch (p.Kind)
            {
                case ParcelKind.Furniture: return FurnitureLabel(p.Id, p.Count);
                case ParcelKind.Outfit: return Outfits.Get(p.Id)?.Name ?? p.Id;
                case ParcelKind.Gift: return Gifts.Get(p.Id)?.Name ?? p.Id;
                default: return "大米 10 斤" + (p.Count > 1 ? "×" + p.Count : "");
            }
        }

        // ---------- 开始摆放 ----------

        private void StartPlacement(bool shop)
        {
            if (!shop)
            {
                _ctrl.BeginPlacement(false);
                return;
            }
            if (S.IsRenovating)
            {
                _ctrl.BeginPlacement(true);
                return;
            }
            if (!S.ShopFitted)
            {
                // 毛坯铺子本来就没开张，不用再确认
                if (!S.BeginRenovation()) return;
                _ctrl.BeginPlacement(true);
                return;
            }
            OpenWindow("歇业装修", 640, 380, w =>
            {
                Ui.Size(Ui.Label(w.Content, "店里要改造，得歇业装修一天：\n· 现在起店今天不再营业，已经在店里排队的客人也不再接待；\n· 装修期间可以随便摆放、挪动、收起店里的摆设；\n· 明天早上恢复营业。", 19, TextAnchor.UpperLeft), -1, 200);
                RectTransform row = Ui.Row(w.Content, 52);
                Ui.Size(Ui.Button(row, "确认歇业，开始装修", () =>
                {
                    if (!S.BeginRenovation()) return;
                    w.Close();
                    _ctrl.BeginPlacement(true);
                }, 19, Ui.Warn), 300);
                Ui.Size(Ui.Button(row, "再想想", w.Close, 19), 140);
            });
        }

        // ---------- 网购 ----------

        private int _onlineTab;

        public void OpenOnlinePanel()
        {
            string appBlock = S.AppBlocker(PhoneApps.Shop);
            if (appBlock != null)
            {
                Toast(appBlock);
                return;
            }
            OpenWindow("网购（2 天后送到家）", 900, 800, w =>
            {
                RectTransform tabs = Ui.Row(w.Content, 46);
                string[] names = { "家具", "店里摆设", "衣服", "礼品", "大米" };
                for (int i = 0; i < names.Length; i++)
                {
                    int tab = i;
                    Ui.Size(Ui.Button(tabs, names[i], () =>
                    {
                        _onlineTab = tab;
                        w.Rebuild();
                    }, 18, _onlineTab == tab ? Ui.ButtonHot : Ui.ButtonBg), 130);
                }
                Line(w.Content, $"现金 {S.Ledger.Balance}。线上价是店里的 85 折，下单后 {GameSession.DeliveryDays} 天送到家，收到的家具放进背包。", 16, Ui.Dim);
                RectTransform list = Ui.Scroll(w.Content, 600);
                switch (_onlineTab)
                {
                    case 0:
                        foreach (FurnitureDef f in FurnitureCatalog.Purchasable(false)) OnlineFurnitureRow(w, list, f);
                        break;
                    case 1:
                        foreach (FurnitureDef f in FurnitureCatalog.Purchasable(true)) OnlineFurnitureRow(w, list, f);
                        break;
                    case 2:
                        foreach (OutfitDef o in Outfits.All.Where(x => x.Id != Outfits.Default)) OnlineOutfitRow(w, list, o);
                        break;
                    case 3:
                        Line(list, "礼品直接送进背包（不受承重限制）。送人前要先有货。", 15, Ui.Dim);
                        foreach (GiftDef g in Gifts.All) OnlineGiftRow(w, list, g);
                        break;
                    default:
                        RectTransform row = Ui.Row(list, 56);
                        Ui.Size(Ui.Label(row, $"大米 {GameSession.RiceBagJin:0} 斤\n<size=14><color=#A8B0BA>也可以去农贸市场按斤买，更便宜。</color></size>", 18), 460);
                        Ui.Size(Ui.Label(row, S.OnlinePrice(ParcelKind.Rice, "rice").ToString(), 19, TextAnchor.MiddleLeft, Ui.Good), 100);
                        Ui.Size(Ui.Button(row, "下单", () =>
                        {
                            S.OrderOnline(ParcelKind.Rice, "rice");
                            w.Rebuild();
                        }, 17, Ui.ButtonHot), 90);
                        break;
                }
                if (S.Parcels.Count > 0) Line(w.Content, "在路上：" + string.Join("、", S.Parcels.Select(p => $"{ParcelName(p)}（{Mathf.Max(0, p.DueDay - S.Clock.Today.TotalDays)} 天）")), 15, Ui.Dim);
            });
        }

        private void OnlineFurnitureRow(Window w, RectTransform list, FurnitureDef def)
        {
            RectTransform row = Ui.Row(list, 58);
            Ui.Size(Ui.Label(row, $"{def.Name}\n<size=14><color=#A8B0BA>{def.Description}</color></size>", 18), 480);
            Ui.Size(Ui.Label(row, S.OnlinePrice(ParcelKind.Furniture, def.Id).ToString(), 19, TextAnchor.MiddleLeft, Ui.Good), 100);
            int have = S.FurnitureInStock(def.Id);
            Ui.Size(Ui.Label(row, have > 0 ? $"背包 ×{have}" : "", 15, TextAnchor.MiddleLeft, Ui.Dim), 80);
            Ui.Size(Ui.Button(row, "下单", () =>
            {
                S.OrderOnline(ParcelKind.Furniture, def.Id);
                w.Rebuild();
            }, 17, Ui.ButtonHot), 80);
        }

        private void OnlineGiftRow(Window w, RectTransform list, GiftDef g)
        {
            RectTransform row = Ui.Row(list, 58);
            Ui.Size(Ui.Label(row, $"{g.Name}\n<size=14><color=#A8B0BA>{g.Description}</color></size>", 18), 480);
            Ui.Size(Ui.Label(row, S.OnlinePrice(ParcelKind.Gift, g.Id).ToString(), 19, TextAnchor.MiddleLeft, Ui.Good), 100);
            int have = S.Backpack.GiftCount(g.Id);
            Ui.Size(Ui.Label(row, have > 0 ? $"背包 ×{have}" : "", 15, TextAnchor.MiddleLeft, Ui.Dim), 80);
            Ui.Size(Ui.Button(row, "下单", () =>
            {
                S.OrderOnline(ParcelKind.Gift, g.Id);
                w.Rebuild();
            }, 17, Ui.ButtonHot), 80);
        }

        private void OnlineOutfitRow(Window w, RectTransform list, OutfitDef o)
        {
            RectTransform row = Ui.Row(list, 58);
            bool has = S.OwnedOutfits.Contains(o.Id);
            bool coming = S.Parcels.Any(p => p.Kind == ParcelKind.Outfit && p.Id == o.Id);
            Ui.Size(Ui.Label(row, $"{o.Name}  魅力 +{o.CharmBonus}\n<size=14><color=#A8B0BA>{o.Description}</color></size>", 18), 480);
            Ui.Size(Ui.Label(row, S.OnlinePrice(ParcelKind.Outfit, o.Id).ToString(), 19, TextAnchor.MiddleLeft, Ui.Good), 100);
            Ui.Size(Ui.Button(row, has ? "已有" : coming ? "在路上" : "下单", () =>
            {
                S.OrderOnline(ParcelKind.Outfit, o.Id);
                w.Rebuild();
            }, 17, Ui.ButtonHot, !has && !coming), 100);
        }

        // ---------- 家具城、农贸市场 ----------

        private void BuildMall(Window w, Place p)
        {
            bool hasStash = S.MallStashCount > 0;
            var tabs = new List<string> { "家里用", "店面设施", "店里装饰", "店铺设备" };
            if (hasStash) tabs.Add("寄存的货");
            // 还在毛坯里的时候，先给店面设施
            string tab = ShopTabs(w, p, tabs, S.HasShop && !S.ShopFitted ? "店面设施" : null);
            Para(w.Content, "先把要买的加进购物车，在下面分好哪些自己背、哪些叫车，再一起结账。线上同款便宜 15%，2 天到家。", 15, Ui.Dim);
            Line(w.Content, $"现金 {S.Ledger.Balance}　　来时的载具：{S.CurrentVehicle.Name}，还能背 {S.FreeKg:0.#} 公斤", 17);
            RectTransform list = GoodsList(w.Content);
            switch (tab)
            {
                case "家里用":
                    foreach (FurnitureDef f in FurnitureCatalog.Purchasable(false)) MallRow(w, list, f);
                    break;
                case "店面设施":
                    Line(list, "租来的铺子是空的，这些是开店必备，买回去歇业装修时摆好。", 15, Ui.Dim);
                    if (S.HasShop) Para(list, FitChecklistText(), 14, Ui.Dim);
                    foreach (FurnitureDef f in FurnitureCatalog.Purchasable(true).Where(x => x.IsFixture)) MallRow(w, list, f);
                    break;
                case "店里装饰":
                    Line(list, "摆放要歇业装修一天。", 15, Ui.Dim);
                    foreach (FurnitureDef f in FurnitureCatalog.Purchasable(true).Where(x => !x.IsFixture)) MallRow(w, list, f);
                    break;
                case "店铺设备":
                    UpgradeSaleRows(w, list, ShopUpgrades.FurnitureMall);
                    break;
                default:
                    BuildMallStash(w, list);
                    break;
            }
            CartPanel(w, w.Content, false);
        }

        private readonly Dictionary<string, string> _shopTabs = new Dictionary<string, string>();

        /// <summary>店里货品分类的标签栏（窗口最上面），返回当前选中的那一类；每家店各记各的。</summary>
        private string ShopTabs(Window w, Place p, IList<string> tabs, string fallback = null)
        {
            if (!_shopTabs.TryGetValue(p.Id, out string cur) || !tabs.Contains(cur)) cur = fallback ?? tabs[0];
            RectTransform bar = Ui.Row(w.Content, 42, 6);
            foreach (string t in tabs)
            {
                string tab = t;
                bool on = tab == cur;
                var b = Ui.Button(bar, tab, () =>
                {
                    _shopTabs[p.Id] = tab;
                    w.Rebuild();
                }, 17, on ? Ui.ButtonHot : new Color(0.2f, 0.24f, 0.3f));
                b.name = "ShopTab_" + tab;
                Ui.Size(b, Mathf.Max(96, tab.Length * 19 + 36));
            }
            return cur;
        }

        /// <summary>货品列表：至少 minHeight 高，窗口里剩下的高度都给它（购物车空着时就很长）。</summary>
        private static RectTransform GoodsList(Transform parent, float minHeight = 220)
        {
            RectTransform list = Ui.Scroll(parent, minHeight);
            ((RectTransform)list.parent.parent).GetComponent<LayoutElement>().flexibleHeight = 1;
            return list;
        }

        private static readonly Color CellFree = new Color(0.16f, 0.18f, 0.22f);

        /// <summary>以前寄存在店里的货：自己来搬（装不下就分几趟），或者全叫货车送回家。</summary>
        private void BuildMallStash(Window w, RectTransform stash)
        {
            Line(stash, $"寄存在店里的货（已付过钱，不过期）{S.MallStashCount} 件，共 {S.MallStashKg:0.#} 公斤", 17, Ui.Accent);
            foreach (MallCartLine l in S.MallStash.ToList())
            {
                FurnitureDef d = FurnitureCatalog.Get(l.Id);
                RectTransform row = Ui.Row(stash, 40);
                Ui.Size(Ui.Label(row, $"{d.Name} ×{l.Count}　<size=14><color=#A8B0BA>每件 {d.WeightKg:0.#} 公斤</color></size>", 17), -1, -1, 1);
                bool fits = d.WeightKg <= S.FreeKg + 1e-9;
                Ui.Size(Ui.Button(row, fits ? "搬走 1 件" : "装不下", () =>
                {
                    S.TakeFromStash(l.Id);
                    w.Rebuild();
                }, 16, Ui.ButtonHot, fits), 110);
            }
            RectTransform bar = Ui.Row(stash, 44, 8);
            Ui.Size(Ui.Button(bar, "能搬的全搬走", () =>
            {
                S.TakeStashWhatFits();
                w.Rebuild();
            }, 17, Ui.ButtonHot), 170);
            Ui.Size(Ui.Button(bar, $"全部叫货车送到{S.DeliveryName}（{S.StashTruckFee}，{S.StashTrucksNeeded} 辆）", () =>
            {
                S.CallTruckForStash();
                w.Rebuild();
            }, 17, Ui.ButtonBg, S.Ledger.Balance.Cents >= S.StashTruckFee.Cents), -1, -1, 1);
        }

        private void MallRow(Window w, RectTransform list, FurnitureDef def)
        {
            RectTransform row = Ui.Row(list, 58);
            Ui.Size(Ui.Label(row, $"{def.Name}  <size=14><color=#A8B0BA>重 {def.WeightKg:0.#} 公斤</color></size>\n<size=14><color=#A8B0BA>{def.Description}</color></size>", 18), 430);
            Ui.Size(Ui.Label(row, def.Price.ToString(), 19, TextAnchor.MiddleLeft, Ui.Good), 90);
            int have = S.FurnitureInStock(def.Id) + S.Backpack.FurnitureCount(def.Id);
            int placedShop = def.ForShop ? S.ShopLayout.Count(p => p.Id == def.Id) : 0;
            string haveText = (have > 0 ? $"已有 ×{have}" : "") + (have > 0 && placedShop > 0 ? "\n" : "") + (placedShop > 0 ? $"已摆 ×{placedShop}" : "");
            Ui.Size(Ui.Label(row, haveText, 14, TextAnchor.MiddleLeft, Ui.Dim), 80);
            CartAddButton(w, row, "furn:" + def.Id, () => S.CartAddFurniture(def.Id), "加入购物车", 160);
        }

        private void BuildTobacco(Window w, Place p)
        {
            List<GiftDef> goods = Gifts.All.Where(x => x.SoldAt == Gifts.TobaccoBusiness).ToList();
            List<string> tabs = goods.Select(TobaccoTab).Distinct().ToList();
            string tab = ShopTabs(w, p, tabs);
            Line(w.Content, "烟酒茶行：先加进购物车，结账时分好自己背的和叫车送的。网购便宜 15%，但要等 2 天。", 16, Ui.Dim);
            Line(w.Content, $"现金 {S.Ledger.Balance}　　还能背 {S.FreeKg:0.#} 公斤", 18, Ui.Dim);
            RectTransform list = GoodsList(w.Content);
            foreach (GiftDef def in goods.Where(x => TobaccoTab(x) == tab)) ShopGiftRow(w, list, def, true);
            CartPanel(w, w.Content, false);
        }

        private static string TobaccoTab(GiftDef g) =>
            g.Id == Gifts.Cig || g.Id == Gifts.CigHi ? "烟" : g.Id == Gifts.Wine || g.Id == Gifts.Baijiu ? "酒" : "饮料点心";

        private void ShopGiftRow(Window w, RectTransform list, GiftDef def, bool open)
        {
            RectTransform row = Ui.Row(list, 66);
            Ui.Size(Ui.Label(row, $"{def.Name}\n<size=14><color=#A8B0BA>{def.Description}  重 {def.WeightKg:0.#} 公斤</color></size>", 18), 400);
            Ui.Size(Ui.Label(row, def.Price.ToString(), 19, TextAnchor.MiddleLeft, Ui.Good), 90);
            int have = S.Backpack.GiftCount(def.Id);
            Ui.Size(Ui.Label(row, have > 0 ? $"背包 ×{have}" : "", 15, TextAnchor.MiddleLeft, Ui.Dim), 80);
            CartAddButton(w, row, "gift:" + def.Id, () => S.CartAddGift(def.Id), "加入购物车", 150, open);
        }

        private void BuildPhoneShop(Window w, Place p)
        {
            Line(w.Content, "手机店。网购和 1788 进货都要用智能手机，买回来在应用商店里下载对应的 app。", 17, Ui.Dim);
            Line(w.Content, $"现金 {S.Ledger.Balance}", 18);
            RectTransform row = Ui.Row(w.Content, 64);
            Ui.Size(Ui.Label(row, "智能手机\n<size=14><color=#A8B0BA>能在应用商店下载网购、日程、聊天、1788 等 app。</color></size>", 18), 400);
            Ui.Size(Ui.Label(row, GameSession.PhonePrice.ToString(), 19, TextAnchor.MiddleLeft, Ui.Good), 90);
            Ui.Size(Ui.Button(row, S.HasSmartphone ? "已有" : "买", () =>
            {
                if (S.BuySmartphone()) Toast("买了智能手机。");
                w.Rebuild();
            }, 17, Ui.ButtonHot, !S.HasSmartphone), 80);
            UpgradeSaleRows(w, w.Content, ShopUpgrades.PhoneShop);
        }

        /// <summary>这家店卖的店铺设备（收款码、空调……）：买了当场装到自己店里。</summary>
        private void UpgradeSaleRows(Window w, Transform parent, string business)
        {
            Line(parent, "—— 给你的店添设备（买了当天装好）——", 18, Ui.Accent);
            foreach (ShopUpgradeDef u in ShopUpgrades.SoldBy(business))
            {
                bool owned = S.HasUpgrade(u.Id);
                string blocker = owned ? null : S.UpgradeBlocker(u);
                RectTransform row = Ui.Row(parent, blocker != null ? 96 : 76);
                string tail = blocker != null ? $"\n<size=14><color=#D79780>{blocker}</color></size>" : "";
                Ui.Size(Ui.Label(row, $"{u.Name}\n<size=14><color=#A8B0BA>{u.Description}</color></size>{tail}", 17), 400);
                Ui.Size(Ui.Label(row, u.Price.ToString(), 19, TextAnchor.MiddleLeft, Ui.Good), 90);
                Ui.Size(Ui.Button(row, owned ? "已装好" : "买", () =>
                {
                    S.BuyUpgrade(u.Id);
                    w.Rebuild();
                }, 17, Ui.ButtonHot, !owned && blocker == null), 80);
            }
        }

        private static string GiftLabel(NpcDef n)
        {
            return n.GiftItemId == null ? $"请{n.GiftName} {n.GiftPrice}" : $"送{n.GiftName}";
        }

        private void BuildFarm(Window w, Place p)
        {
            bool open = S.IsFarmMarketOpen;
            string tab = ShopTabs(w, p, new[] { "米", "菜蛋肉", "水果" });
            Line(w.Content, open ? "营业中（每天 6:00–18:00）。米按斤卖，菜、蛋、肉当天最新鲜。" : "<color=#D79780>收摊了（每天 6:00–18:00）</color>");
            Line(w.Content, $"现金 {S.Ledger.Balance}   大米：背包 {S.Backpack.Food.RiceJin:0.#} 斤 / 家里 {S.Pantry.RiceJin:0.#} 斤", 18, Ui.Dim);
            Line(w.Content, "先加进购物车，结账时分好自己背的和叫车送的。背回去的要打开背包放进厨房；叫车的直接送进厨房。家里没有冰箱，买多了会放坏。", 15, Ui.Dim);
            RectTransform list = GoodsList(w.Content);
            if (tab == "水果")
                foreach (GiftDef g in Gifts.All.Where(x => x.SoldAt == Gifts.FarmBusiness)) ShopGiftRow(w, list, g, open);
            if (tab == "菜蛋肉") FarmIngredientRows(w, list, open);
            if (tab == "米") FarmRiceRow(w, list, open);
            CartPanel(w, w.Content, false);
        }

        private void FarmRiceRow(Window w, RectTransform list, bool open)
        {
            RectTransform rrow = Ui.Row(list, 50);
            Ui.Size(Ui.Label(rrow, $"大米 {Pantry.RicePricePerJin}/斤", 19), 200);
            double riceIn = S.CartAmount("rice");
            Ui.Size(Ui.Label(rrow, riceIn > 0 ? $"购物车 {riceIn:0.#} 斤" : "", 14, TextAnchor.MiddleLeft, Ui.Accent), 110);
            foreach (int jin in new[] { 1, 5, 10 })
            {
                int j = jin;
                Ui.Size(Ui.Button(rrow, $"+{j} 斤", () =>
                {
                    S.CartAddRice(j);
                    w.Rebuild();
                }, 17, Ui.ButtonHot, open), 90);
            }
        }

        private void FarmIngredientRows(Window w, RectTransform list, bool open)
        {
            foreach (IngredientDef def in Ingredients.All)
            {
                IngredientDef d = def;
                RectTransform row = Ui.Row(list, 56);
                Ui.Size(Ui.Label(row, $"{d.Name}（{d.Unit}）\n<size=14><color=#A8B0BA>可焖「{d.DishName}」，保质 {d.ShelfDays} 天</color></size>", 18), 380);
                Ui.Size(Ui.Label(row, d.Price.ToString(), 19, TextAnchor.MiddleLeft, Ui.Good), 80);
                Ui.Size(Ui.Label(row, $"背包 ×{S.Backpack.Food.IngredientCount(d.Id)}\n家里 ×{S.Pantry.IngredientCount(d.Id)}", 14, TextAnchor.MiddleLeft, Ui.Dim), 90);
                foreach (int n in new[] { 1, 3 })
                {
                    int count = n;
                    CartAddButton(w, row, "ing:" + d.Id, () => S.CartAddIngredient(d.Id, count), "+" + count, 70, open, n == 1);
                }
            }
        }

        // ---------- 摆放模式面板 ----------

        private RectTransform _placePanel;
        private RectTransform _placeList;
        private Text _placeStatus;

        public void ShowPlacementPanel()
        {
            if (_placePanel == null)
            {
                Image bg = Ui.Panel(_hudCanvas.transform, "PlacePanel", Ui.PanelBg);
                _placePanel = bg.rectTransform;
                _placePanel.Anchor(1, 0, 1, 1, -380, 70, 10, 74);
                Ui.VList(_placePanel, 8, 12);
            }
            _placePanel.gameObject.SetActive(true);
            CloseDock();
            RefreshPlacementPanel();
        }

        public void HidePlacementPanel()
        {
            if (_placePanel != null) _placePanel.gameObject.SetActive(false);
            if (_ctrl.InInterior && S != null) OpenDock();
        }

        public void RefreshPlacementPanel()
        {
            if (_placePanel == null || !_placePanel.gameObject.activeSelf) return;
            Ui.Clear(_placePanel);
            bool shop = _ctrl.PlacingShop;
            Text title = Ui.Label(_placePanel, shop ? "装修店面（今天歇业）" : "摆放家具", 22, TextAnchor.MiddleLeft, Ui.Accent);
            Ui.Size(title, -1, 34);
            Text mode = Ui.Label(_placePanel, "编辑模式 · 俯视图　格线 1 米，摆放吸附 0.25 米", 15, TextAnchor.MiddleLeft, new Color(0.45f, 0.9f, 1f));
            Ui.Size(mode, -1, 24);
            Text help = Ui.Label(_placePanel, "左键：放下  ·  R：旋转（墙面的自动挂最近的墙）\n右键 / Esc：放回  ·  X：收进背包  ·  滚轮：缩放\n中键拖动 / Q E：转视角  ·  V：复位视角\n点已经摆好的家具（含墙上的海报）可以拿起来挪", 15, TextAnchor.UpperLeft, Ui.Dim);
            Ui.Size(help, -1, 84);
            _placeStatus = Ui.Label(_placePanel, "", 16, TextAnchor.UpperLeft);
            Ui.Size(_placeStatus, -1, 56);

            Line(_placePanel, shop ? "待安装的店面家具：" : "待安装的家居家具：", 17);
            bool any = false;
            foreach (var kv in S.FurnitureStock)
            {
                FurnitureDef def = FurnitureCatalog.Get(kv.Key);
                if (def == null || def.ForShop != shop) continue;
                any = true;
                string id = def.Id;
                Ui.Size(Ui.Button(_placePanel, $"{def.Name} ×{kv.Value}", () => _ctrl.SelectStock(id), 17, _ctrl.PlaceId == id && !_ctrl.PlaceHoldsPlaced ? Ui.ButtonHot : Ui.ButtonBg), -1, 40);
            }
            if (!any) Ui.Size(Ui.Label(_placePanel, "空的。去家具城买，或者网购。", 15, TextAnchor.MiddleLeft, Ui.Dim), -1, 30);

            if (_ctrl.PlaceId != null)
            {
                Ui.Size(Ui.Button(_placePanel, "旋转（R）", _ctrl.RotateGhost, 17), -1, 40);
                if (_ctrl.PlaceHoldsPlaced) Ui.Size(Ui.Button(_placePanel, "收进背包（X）", _ctrl.StoreSelected, 17, Ui.Warn), -1, 40);
            }
            var history = Ui.Row(_placePanel, 36);
            Ui.Size(Ui.Button(history, "撤销 Ctrl+Z", () => _ctrl.UndoPlacement(), 15, Ui.ButtonBg, S.CanUndoLayout), 165);
            Ui.Size(Ui.Button(history, "重做 Ctrl+Y", () => _ctrl.UndoPlacement(true), 15, Ui.ButtonBg, S.CanRedoLayout), 165);
            Ui.Size(Ui.Button(_placePanel, "完成", _ctrl.EndPlacement, 19, Ui.ButtonHot), -1, 46);
        }

        private void UpdatePlacementStatus()
        {
            if (_placePanel == null || !_placePanel.gameObject.activeSelf || _placeStatus == null) return;
            if (!_ctrl.Placing) return;
            if (_ctrl.PlaceId == null) _placeStatus.text = "选一件背包里的家具，或者点屋里的家具拿起来挪。";
            else if (_ctrl.PlaceError == null) _placeStatus.text = "<color=#A9C9A9>这里可以放，点左键。</color>";
            else _placeStatus.text = "<color=#D79780>" + _ctrl.PlaceError + "</color>";
        }

        // ---------- 头顶的“可以交互”提示 ----------

        private RectTransform _interactBox;
        private RectTransform _interactArrow;
        private Text _interactText;

        private void UpdateInteractMarker()
        {
            if (_interactBox == null)
            {
                Image bg = Ui.Panel(_labelCanvas.transform, "InteractLabel", new Color(0.07f, 0.08f, 0.1f, 0.9f));
                bg.raycastTarget = false;
                _interactBox = bg.rectTransform;
                _interactBox.anchorMin = _interactBox.anchorMax = Vector2.zero;
                _interactBox.pivot = new Vector2(0.5f, 0);
                _interactBox.sizeDelta = new Vector2(300, 40);
                _interactText = Ui.Label(bg.transform, "", 19, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f));
                _interactText.raycastTarget = false;
                _interactText.rectTransform.Fill(2);

                Text arrow = Ui.Label(_labelCanvas.transform, "▼", 34, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.2f));
                arrow.raycastTarget = false;
                _interactArrow = arrow.rectTransform;
                _interactArrow.anchorMin = _interactArrow.anchorMax = Vector2.zero;
                _interactArrow.pivot = new Vector2(0.5f, 0);
                _interactArrow.sizeDelta = new Vector2(44, 40);
            }

            bool show = _ctrl.TryGetInteract(out Vector3 world, out string label);
            Vector3 sp = _ctrl.Cam.WorldToScreenPoint(world);
            show &= sp.z > 0;
            _interactBox.gameObject.SetActive(show);
            _interactArrow.gameObject.SetActive(show);
            if (!show) return;
            _interactText.text = "[F]  " + label;
            var canvasRt = (RectTransform)_labelCanvas.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, LabelCam, out Vector2 local);
            Vector2 basePos = local + canvasRt.rect.size * 0.5f;
            float bob = Mathf.Sin(_uiTime * 5f) * 6f;
            _interactArrow.anchoredPosition = basePos + new Vector2(0, bob);
            _interactBox.anchoredPosition = basePos + new Vector2(0, 38 + bob);
        }
    }
}
