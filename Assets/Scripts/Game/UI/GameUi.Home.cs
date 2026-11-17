using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
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
        private void OpenRoomWindow(string title, float w, float h, System.Action<Window> build, bool allowDuringOpeningMeal = false)
        {
            OpenWindow(title, w, h, win =>
            {
                if (!_ctrl.InHome)
                {
                    win.Close();
                    return;
                }
                build(win);

            }, true, true, allowDuringOpeningMeal: allowDuringOpeningMeal);
        }

        public void OpenBedWindow()
        {
            if (!S.CanSleepAt(S.CurrentPlace)) return;
            bool showHelp = false;
            OpenWindow("睡觉", 620, 410, w =>
            {
                if (!S.CanSleepAt(S.CurrentPlace)) { w.Close(); return; }
                w.TimeStatusOverride = $"当前 {Ui.Hm(S.Clock.MinuteOfDay)}";
                RefreshWindowTimeStatus(w);
                BuildBed(w, showHelp, () => { showHelp = !showHelp; w.Rebuild(); });
            });
        }

        public void OpenWardrobeWindow() => OpenRoomWindow("衣柜", 700, 640, BuildWardrobe);

        private AvatarPreview _avatarPreview;


        public void OpenStorageWindow()
        {
            if(S.CurrentPlace!=S.Home||!S.HasWarehouseHere){Toast("这里还没摆纸箱，摆一个纸箱后才能使用后仓。");return;}
            OpenBackpackPanel();
        }

        private string _boxSel, _boxSide;

        private static readonly Stock NoParts = new Stock(0);

        private System.Collections.Generic.List<BagStack> CollectBox()
        {
            if(!S.InPlayerShop)return CollectStacks(S.HomeBox,S.HomeBoxParts);
            var result=CollectStacks(S.ShopLocker,S.PlayerShop.Stock);
            foreach(var line in S.PlayerShop.Stock.GoodsLines())
            {
                var product=Gifts.Get(line.Key);double kg=product.WeightKg*line.Value;
                var batches=S.PlayerShop.Stock.GoodsLots.Where(b=>b.Id==line.Key&&b.Count>0)
                    .Select(b=>new FoodBatch{Id=b.Id,Name=product.Name,Count=b.Count,ExpireDay=b.ExpireDay,Chilled=b.Chilled}).ToArray();
                result.Add(new BagStack{Key="stockgift:"+line.Key,Name=product.Name,Kind="本店商品库存（含已陈列商品）",Count=line.Value,Kg=kg,
                    Space=kg/Backpack.KgPerSpace,Color=new Color(.70f,.52f,.18f),Batches=batches.Length==0?null:batches});
            }
            return result;
        }

        private System.Collections.Generic.List<BagStack> CollectTemp() =>
            S.InPlayerShop ? CollectStacks(S.ShopTemp, NoParts) : CollectStacks(S.HomeTemp, S.HomeTempParts);

        /// <summary>店里的后仓（走到后仓前按 F，或右侧面板“库存”页、背包里打开）：吃的、礼品等放这里，配件放进来直接上货架。</summary>
        public void OpenShopLockerWindow()
        {
            if(!S.InPlayerShop||!S.HasWarehouseHere){Toast("这里还没摆纸箱，摆一个纸箱后才能使用后仓。");return;}
            OpenBackpackPanel();
        }

        /// <summary>后仓窗口：临时存放到期持续提示，但可关闭后继续处理其他事务。</summary>
        private Window OpenBoxWindow()
        {
            _boxSel = null;
            _boxSide = null;
            var place=S.CurrentPlace;
            return OpenWindow("背包与后仓", 820, 880, win =>
            {
                if (!S.HasWarehouseHere||S.CurrentPlace!=place)
                {
                    win.Close();
                    return;
                }
                win.Closable = true;
                BuildQuestItems(win);
                BuildStorage(win);
            },scrollContent:false);
        }

        /// <summary>临时存放还能放多久（“2 天 5 小时” / “40 分钟”）。</summary>
        private static string TempLeftText(long minutes)
        {
            long d = minutes / 1440, h = minutes % 1440 / 60;
            return d > 0 ? $"{d} 天" + (h > 0 ? $" {h} 小时" : "") : h > 0 ? $"{h} 小时" : $"{System.Math.Max(1, minutes)} 分钟";
        }

        /// <summary>
        /// 同一套 B / F 界面：背包在上，房间内的共享后仓在下，各自独立滚动；
        /// 点一格选中，再放进去 / 拿出来。
        /// </summary>
        private void BuildStorage(Window w)
        {
            bool shop = S.InPlayerShop;
            Line(w.Content,$"—— 我的背包 · 负重 {S.CarriedKg:0.##}/{S.CarryLimitKg:0.##} 千克 ——",18,Ui.Accent).name="WarehouseBagHeading";
            var bagStacks=CollectBag(includeQuestPages: true);
            var bagGrid=BagGrid(w.Content,bagStacks,Mathf.CeilToInt((float)S.CurrentVehicle.CargoCapacity),_boxSide=="bag"?_boxSel:null,key=>
            {_boxSide="bag";_boxSel=key;w.Rebuild();},-1);
            bagGrid.parent.parent.name="WarehouseBackpackGrid";

            Line(w.Content,"—— "+(shop?"本店后仓":"家中后仓")+" · 后仓容量不限 ——",18,Ui.Accent).name="WarehouseStoreHeading";
            Line(w.Content,"本房间的 "+(shop?S.ShopBoxes:S.HomeBoxes)+" 个纸箱共用一个后仓。背包和运输仍有承重限制，成品保质期照常计算。",15,Ui.Dim);
            var boxStacks=CollectBox();
            var boxGrid=BagGrid(w.Content,boxStacks,8,_boxSide=="box"?_boxSel:null,key=>
            {_boxSide="box";_boxSel=key;w.Rebuild();},-1,true);
            boxGrid.parent.parent.name="WarehouseStoredGrid";

            bool hasTemp = S.HasTempHere;
            var tempStacks = hasTemp ? CollectTemp() : new System.Collections.Generic.List<BagStack>();
            if (hasTemp)
            {
                Line(w.Content, S.TempDueHere
                    ? $"—— 临时存放 {S.TempKgHere:0.##} 千克：已经放满 {GameSession.TempKeepDays} 天了，请尽快清理，可先关闭稍后处理 ——"
                    : $"—— 临时存放 {S.TempKgHere:0.##} 千克（尚未归仓）：还能放 {TempLeftText(S.TempMinutesLeftHere)}，到期持续提醒清理 ——", 17, Ui.Warn);
                BagGrid(w.Content, tempStacks, 0, _boxSide == "temp" ? _boxSel : null, key =>
                {
                    _boxSide = "temp";
                    _boxSel = key;
                    w.Rebuild();
                }, 100);
                RectTransform tempBar = Ui.Row(w.Content, 40, 8);
                Ui.Size(Ui.Button(tempBar, "能放的都放进后仓", () => { S.TempFillBox(); w.Rebuild(); }, 15, Ui.ButtonHot), 170);
                Ui.Size(Ui.Button(tempBar, "打电话叫货车搬走…", OpenTruckWindow, 15, Ui.ButtonBg), 170);
                Ui.Size(Ui.Button(tempBar, "全部扔掉…", () => ConfirmAction("全部扔掉", $"临时存放里 {S.TempKgHere:0.##} 千克东西都扔掉，不退钱。", "扔掉", () =>
                {
                    S.TempDiscardAll();
                    w.Rebuild();
                }), 15, Ui.Warn), 120);
            }

            var side = _boxSide == "temp" ? tempStacks : _boxSide == "box" ? boxStacks : bagStacks;
            BagStack pick = side.FirstOrDefault(s => s.Key == _boxSel);
            var tools=Ui.Row(w.Content,40);
            Ui.Size(Ui.Button(tools,"全部入库（保留即食食物）",()=>{S.StoreBackpackInWarehouse();_boxSel=null;w.Rebuild();if(shop)RebuildDock();},17,Ui.ButtonHot,S.CanStoreBackpackInWarehouse),-1,-1,1);
            if(S.CanOpenFridge)Ui.Size(Ui.Button(tools,"打开冰箱",()=>OpenFridgeWindow(),17),140);
            if(_boxSide=="bag"&&pick!=null)
            {
                string selected=pick.Key;
                if(selected.StartsWith("lunch:"))Ui.Size(Ui.Button(tools,"吃 / 喝一份",()=>{S.EatLunchboxNow(selected.Substring(6));w.Rebuild();},17),150);
                else if(selected.StartsWith("gift:")&&GameSession.IsEdibleGift(selected.Substring(5)))
                    Ui.Size(Ui.Button(tools,"吃 / 喝一份",()=>{S.ConsumeGift(selected.Substring(5));w.Rebuild();},17),150);
            }
            RectTransform bottom = Ui.Row(w.Content, 50);
            if (pick == null)
            {
                Ui.Size(Ui.Label(bottom, "点一格看详情。", 16, TextAnchor.MiddleLeft, Ui.Dim), -1, -1, 1);
                return;
            }
            Ui.Size(Ui.Label(bottom, $"<b>{pick.Name}</b> ×{pick.Count}　{pick.Kg:0.##} 千克", 17), shop && pick.Key.StartsWith("gift:") ? 220 : 300, -1, 1);
            string key = pick.Key;
            bottom.name="StorageActions_"+_boxSide+"_"+key;
            if (key.StartsWith("quest-page:")) return;
            int n = pick.Count;
            string one = key == "rice" ? "1 千克" : "一份";
            string unit = key == "rice" ? "千克" : "份";
            if (_boxSide != "bag" && key.StartsWith("furn:"))
            {
                string fid = key.Substring(5);
                Ui.Size(Ui.Button(bottom, "摆出来", () => PlaceFromStorage(shop, fid), 16, Ui.ButtonBg), 90);
            }
            if (_boxSide == "temp")
            {
                bool toShelf = shop && key.StartsWith("part:");
                HoldButton(w, bottom, "放进后仓", Ui.ButtonBg, n, unit, c => S.MoveTemp(key, (int)c, "box"), 110);
                HoldButton(w, bottom, "拿进背包", Ui.ButtonBg, n, unit, c => S.MoveTemp(key, (int)c, "bag"), 110);
                HoldButton(w, bottom, "扔掉", Ui.Warn, n, unit, c => S.MoveTemp(key, (int)c, "trash"), 80);
            }
            else if (_boxSide == "box")
            {
                if(key.StartsWith("stockgift:"))
                {
                    string id=key.Substring(10);
                    Ui.Size(Ui.Button(bottom,"拿一件",()=>{S.TakeShopGoodsIntoBag(id,1);w.Rebuild();RebuildDock();},16,Ui.ButtonBg),100);
                    Ui.Size(Ui.Button(bottom,"全拿出来",()=>{S.TakeShopGoodsIntoBag(id,n);w.Rebuild();RebuildDock();},16),110);
                    return;
                }
                Ui.Size(Ui.Button(bottom, "拿" + one, () => { S.TakeFromBox(key); w.Rebuild(); }, 16, Ui.ButtonBg), 100);
                Ui.Size(Ui.Button(bottom, "全拿出来", () => { S.TakeFromBox(key, n); w.Rebuild(); }, 16, Ui.ButtonBg), 110);
                if (shop && key.StartsWith("part:") && key.EndsWith(":b"))
                {
                    var part = PartCatalog.Get(key.Split(':')[1]);
                    Ui.Size(Ui.Button(bottom,"拆 1 盒",()=>{S.Unbox(part,1);w.Rebuild();RebuildDock();},16),85);
                    Ui.Size(Ui.Button(bottom,"全部拆盒",()=>{S.Unbox(part,n);w.Rebuild();RebuildDock();},16),100);
                }
                if (shop && key.StartsWith("gift:"))
                {
                    string gid = key.Substring(5);
                    Ui.Size(Ui.Button(bottom, "上架卖", () => { S.ListGoods(gid, 1); w.Rebuild(); }, 16, Ui.ButtonBg), 80);
                    Ui.Size(Ui.Button(bottom, "全上架", () => { S.ListGoods(gid, n); w.Rebuild(); }, 16, Ui.ButtonBg), 80);
                }
            }
            else
            {
                bool toShelf = shop && key.StartsWith("part:");
                Ui.Size(Ui.Button(bottom, toShelf ? "上货架一件" : "放" + one, () => { S.StoreInBox(key); w.Rebuild(); }, 16, Ui.ButtonBg), 100);
                Ui.Size(Ui.Button(bottom, toShelf ? "全上货架" : "全放进去", () => { S.StoreInBox(key, n); w.Rebuild(); }, 16, Ui.ButtonBg), 120);
                if (shop && key.StartsWith("gift:"))
                {
                    string gid = key.Substring(5);
                    Ui.Size(Ui.Button(bottom, "上架卖", () => { S.ListGoods(gid, 1); w.Rebuild(); }, 16, Ui.ButtonBg), 80);
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
            public FoodBatch[] Batches;
        }

        private string _bagSel;
        private const int BagColumns = 8;

        private System.Collections.Generic.List<BagStack> CollectBag(bool includeQuestPages = false)
        {
            var stacks = CollectStacks(S.Backpack, S.Cargo);
            if (!includeQuestPages) return stacks;

            for (int i = 0; i < 3; i++)
            {
                if (!S.CommunityHasPiece(i)) continue;
                stacks.Add(new BagStack
                {
                    Key = "quest-page:" + i,
                    Name = GameSession.CommunityPieceName(i),
                    Kind = "主线图页，集齐后交给唐蔚。",
                    Count = 1,
                    Color = new Color(.52f, .40f, .18f)
                });
            }
            return stacks;
        }

        /// <summary>背包 / 后仓里的东西摊成一叠一叠（配件、米菜、饭盒、礼品、家具）。</summary>
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
            if (bag.Food.RiceKg > 0)
                l.Add(new BagStack { Key = "rice", Name = "大米（千克）", Kind = "买的米和菜", Count = (int)System.Math.Ceiling(bag.Food.RiceKg), Kg = bag.Food.RiceKg * 1.0, Space = bag.Food.RiceKg * 1.0 / Backpack.KgPerSpace, Color = new Color(0.30f, 0.55f, 0.30f) });
            foreach (var g in bag.Food.Ingredient.GroupBy(b => b.Id))
            {
                int n = g.Sum(b => b.Count);
                double kg = n * Backpack.IngredientKg(g.Key);
                l.Add(new BagStack { Key = "ing:" + g.Key, Name = g.First().Name, Kind = "食材", Count = n, Kg = kg, Space = kg / Backpack.KgPerSpace, Color = new Color(0.30f, 0.55f, 0.30f) });
            }
            foreach (var meal in bag.Food.Packed.GroupBy(b => b.Id))
            {
                int n = meal.Sum(b => b.Count);
                double kg = n * FoodBatch.WeightOf(meal.Key);
                Color color = meal.Key == "veg" ? new Color(0.3f, 0.55f, 0.3f) : meal.Key == "pork" ? new Color(0.62f, 0.35f, 0.28f) : new Color(0.62f, 0.55f, 0.35f);
                l.Add(new BagStack { Key = "lunch:" + meal.Key, Name = meal.First().Name, Batches = meal.ToArray(), Kind = (FastFoodCatalog.Packed(meal.Key)?.Drink == true ? "饮料 · 饱腹 +" : "即食食物 · 饱腹 +") + meal.First().Hunger.ToString("0.##") + " · " + GameSession.MealEffects(meal.Key) + " · 最早到期：第 " + meal.Min(b => b.ExpireDay) + " 天", Count = n, Kg = kg, Space = kg / Backpack.KgPerSpace, Color = color });
            }
            foreach (var kv in bag.GiftLines)
            {
                GiftDef gd = Gifts.Get(kv.Key);
                double kg = gd.WeightKg * kv.Value;
                l.Add(new BagStack { Key = "gift:" + kv.Key, Name = gd.Name, Kind = GameSession.IsEdibleGift(gd.Id) ? "可食用 · " + GameSession.GiftEffects(gd.Id) : "礼品", Count = kv.Value, Kg = kg, Space = kg / Backpack.KgPerSpace, Color = new Color(0.70f, 0.52f, 0.18f) });
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

        private static readonly Color BagBadgeSurface = new Color32(35, 43, 38, 255);

        /// <summary>一格一格的背包格子：8 列，一叠东西占几格就画几格。</summary>
        private RectTransform BagGrid(Transform parent, System.Collections.Generic.List<BagStack> stacks, int capacity, string selKey, System.Action<string> onPick, float height,bool compact=false)
        {
            var cells = new System.Collections.Generic.List<(BagStack st, int n, int? expiry)>();
            int used = 0;
            foreach (BagStack st in stacks)
            {
                var batches = st.Batches == null
                    ? new[] { (day: (int?)null, count: st.Count) }
                    : st.Batches.Where(batch => batch.Count > 0).GroupBy(batch => batch.ExpireDay).OrderBy(group => group.Key)
                        .Select(group => (day: (int?)group.Key, count: group.Sum(batch => batch.Count))).ToArray();
                foreach (var batch in batches)
                {
                    int c = compact?1:Mathf.Max(1, Mathf.CeilToInt((float)(st.Space * batch.count / st.Count - 1e-6)));
                    int left = batch.count;
                    for (int i = 0; i < c; i++)
                    {
                        int n = Mathf.CeilToInt(left / (float)(c - i));
                        left -= n;
                        cells.Add((st, n, batch.day));
                    }
                    used += c;
                }
            }
            int total = Mathf.Max(capacity, used);
            total = (total + BagColumns - 1) / BagColumns * BagColumns;

            RectTransform grid = Ui.Scroll(parent, height);
            if(height<0)
            {
                var area=Ui.Size(grid.parent.parent).GetComponent<LayoutElement>();
                area.minHeight=110;area.flexibleHeight=1;
            }
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
                        Color bg = sel ? Ui.SelectedButton(true) : Color.Lerp(Ui.PanelBg2, st.Color, .18f);
                        int count = cells[idx].n;
                        int? expiry = cells[idx].expiry >= 0 ? cells[idx].expiry : null;
                        var cell = Ui.Size(Ui.Button(row, ShortName(st.Name), () => onPick(st.Key), 14, bg), 80, 80);
                        cell.name="BagItem_"+st.Key;
                        Ui.FlatBorder(cell.image, sel ? Ui.Accent : st.Color, sel ? 2 : 1);
                        var title = cell.GetComponentInChildren<Text>();
                        title.fontStyle = FontStyle.Bold;
                        float band = expiry.HasValue ? .2f : .02f;
                        title.rectTransform.Anchor(0, band, 1, band + .21f, 2, 0, 2, 0);
                        FillItemPortrait(cell.transform, st.Key).Anchor(.18f, band + .21f, .82f, .98f);
                        string amount = st.Key == "rice" ? $"{st.Kg * count / st.Count:0.##}kg" : count > 0 ? $"×{count}" : "…";
                        var quantity = Ui.Panel(cell.transform, "BagQuantity", BagBadgeSurface);
                        quantity.raycastTarget = false;
                        float badgeWidth = Mathf.Clamp(7 * amount.Length + 8, 24, 72);
                        quantity.rectTransform.Anchor(1, 1, 1, 1, -badgeWidth - 2, -21, 2, 2);
                        var badge = Ui.Label(quantity.transform, amount, 14, TextAnchor.MiddleCenter, Color.white);
                        badge.fontStyle = FontStyle.Bold;
                        badge.resizeTextForBestFit = true;
                        badge.resizeTextMinSize = 10;
                        badge.resizeTextMaxSize = 14;
                        badge.horizontalOverflow = HorizontalWrapMode.Overflow;
                        badge.rectTransform.Fill(1);
                        cell.Tip(st.Name);
                        AddFoodExpiry(cell, st.Key, expiry);
                    }
                    else
                    {
                        bool beyond = idx >= capacity;
                        Color fill = beyond ? Color.Lerp(Ui.FlatBand, Ui.Warn, .18f) : Ui.FlatBand;
                        Image empty = Ui.FlatPanel(row, "Empty", fill, beyond ? Ui.Warn : Ui.FlatWellEdge);
                        Ui.Size(empty, 80, 80);
                        if (beyond)
                            Ui.Label(empty.transform, "—", 18, TextAnchor.MiddleCenter, Ui.Dim).rectTransform.Fill();
                    }
                }
            }
            return grid;
        }

        /// <summary>顶栏“背包”（B）：一格一格的格子，1 格 = 2 千克，格子数就是载具的载货容量。</summary>
        public void OpenBackpackPanel()
        {
            if(_backpackWin!=null&&!_backpackWin.Closed)return;
            if(S.HasWarehouseHere){_backpackWin=OpenBoxWindow();return;}
            _bagSel = null;
            _backpackWin = OpenWindow("背包", 760, 700, w =>
            {
                BuildQuestItems(w);
                double carried = S.CarriedKg, limit = S.CarryLimitKg;
                bool heavy = carried > limit * 0.85;
                Line(w.Content, $"{S.CurrentVehicle.Name}　负重 {carried:0.##}/{limit:0.##} 千克", 17, heavy ? Ui.Warn : Ui.Dim)
                    .Tip($"1 格 = {Backpack.KgPerSpace:0.##} 千克。\n营养 {S.Player.Nutrition:0.##}/100 · 心情 {S.Player.Mood:0.##}/100。\n选中饭菜后可吃指定饭菜；每天 12:00、18:30 或饱腹低于 25 时自动进餐，先吃最早到期的。\n摆有纸箱的家或店里，按 B 使用后仓。");
                Ui.Bar(w.Content, (float)(limit <= 0 ? 0 : carried / limit), heavy ? Ui.Warn : Ui.Accent, 700, 10);

                var stacks = CollectBag(includeQuestPages: true);
                BagGrid(w.Content, stacks, Mathf.CeilToInt((float)S.CurrentVehicle.CargoCapacity), _bagSel, key =>
                {
                    _bagSel = key;
                    w.Rebuild();
                }, 360);

                BagStack pick = stacks.FirstOrDefault(s => s.Key == _bagSel);
                if (pick != null)
                    Para(w.Content, $"<b>{pick.Name}</b> ×{pick.Count}　占 {Mathf.Max(1, Mathf.CeilToInt((float)(pick.Space - 1e-6)))} 格　{pick.Kg:0.##} 千克\n<color=#655E52>{pick.Kind}</color>", 17);
                else
                    Line(w.Content, stacks.Count == 0 ? "背包是空的 · 去市场 / 农贸 / 网购添置" : "点一格看详情", 17, Ui.Dim);

                if (S.CanOpenFridge) Ui.Size(Ui.Button(w.Content, "打开这里的冰箱", () => OpenFridgeWindow(), 17), -1, 34);
                RectTransform acts = Ui.Row(w.Content, 46);
                if (pick != null && pick.Key.StartsWith("lunch:"))
                    Ui.Size(Ui.Button(acts, (FastFoodCatalog.Packed(pick.Key.Substring(6))?.Drink == true ? "喝一份" : "吃一份") + pick.Name, () =>
                    {
                        S.EatLunchboxNow(pick.Key.Substring(6));
                        w.Rebuild();
                    }, 18), 180);
                if (pick != null && pick.Key.StartsWith("gift:") && GameSession.IsEdibleGift(pick.Key.Substring(5)))
                    Ui.Size(Ui.Button(acts, pick.Key == "gift:" + Gifts.Soda ? "喝一瓶可乐" : "吃一份" + pick.Name, () => { S.ConsumeGift(pick.Key.Substring(5)); w.Rebuild(); }, 17, Ui.ButtonHot), 180);

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
                    }, 17, Ui.SelectedButton(_repairDay == o)), 110);
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

        public void OpenSchedulePanel() => OpenCalendar();
        private void BuildPersonalSchedule(Window w)
        {
            w.LiveConversation = false;
            int today = S.Clock.Today.TotalDays;
            var entries = S.Schedule().ToLookup(e => (int)(e.Minute / GameTimestamp.MinutesPerDay));
            Line(w.Content, $"笔记本 · 接下来 {NotebookDays} 天", 17, Ui.Accent);
            for (int day = today; day < today + NotebookDays; day++)
            {
                var card = BrandCard(w.Content, "");
                Line(card, DateLabel(day), 18, Ui.Accent);
                if (!entries.Contains(day)) Line(card, "暂无安排", 16, Ui.Dim);
                foreach (var e in entries[day])
                {
                    RectTransform row = Ui.Row(card, 44);
                    string color = e.Kind == "appointment" ? "#DDC18B" : (e.Kind == "repair" || e.Kind == "repairing") ? "#A9C9A9" : "#C8D0DA";
                    Ui.Size(Ui.Label(row, e.HasTime ? e.TimeText : "全天", 18, TextAnchor.MiddleLeft, Ui.Dim), 80);
                    Ui.Size(ProductText(row, $"<color={color}>{e.Text}</color>", 17), -1, -1, 1);
                    if (e.ActivityId != null)
                    {
                        string id = e.ActivityId;
                        Ui.Size(Ui.Button(row, "查看日程", () => OpenCityActivityDetail(id), 16), 90);
                        ScheduleActivityGoButton(row, id);
                    }
                    if (e.ExpoId != null)
                    {
                        string expoId = e.ExpoId;
                        Ui.Size(Ui.Button(row, "参展安排", () => OpenBookedExpo(expoId), 16), 94, 40);
                    }
                    if (e.Kind == "wallet_return" && S.WalletReturnStation is Place station)
                        Ui.Size(Ui.Button(row, S.CurrentPlace == station ? "查看站内交接" : "前往地铁站",
                            OpenWalletReturnDestination, 16), 114, 40);
                    if (e.Kind == "repair")
                    {
                        string target = S.HomeVisits.FirstOrDefault(v => v.StartMinute == e.Minute)?.TargetId;
                        Ui.Size(Ui.Button(row, "取消（不退款）", () =>
                        {
                            if (target != null) S.CancelRepair(target);
                            w.Rebuild();
                        }, 16, Ui.Warn), 100);
                    }
                }
                if (day == today) BuildScheduledEventFollowUps(w);
            }
        }

        private void ScheduleActivityGoButton(Transform row, string activityId)
        {
            var activity=S.CityActivities.FirstOrDefault(a=>a.Id==activityId && a.Active);
            bool here=activity!=null && S.CurrentPlace?.Id==activity.VenueId;
            string blocker=_ctrl.Placing?"请先结束摆放家具。":CityActivityMenuOrTravelBlocker(activityId);
            string early = activity != null && !here && blocker == null ? S.CityActivityEarlyDepartureHint(activity) : null;
            bool walk = S.CityActivityService(activity)?.Id == "hike";
            string action = !here ? "一键前往" : walk ? activity.State == CityActivityPhase.Running ? "散步详情" : "选择路线" : "打开菜单";
            Ui.Size(Ui.Button(row,action,()=>GoToScheduledCityActivity(activityId),16,
                early == null ? Ui.ButtonHot : Ui.ButtonBg,blocker==null),94,40)
                .Tip(blocker ?? early ?? (here ? walk ? "查看散步详情或选择步道。" : "已到达约定场所，打开菜单或服务。"
                    : "沿实际路线前往约定场所；按 WASD 可随时接管。"));
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
        public void OpenNotebook() => OpenSchedulePanel();



        private static void RuledLine(Transform parent)
        {
            Image line = Ui.Panel(parent, "Rule", PaperLine);
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            line.rectTransform.Anchor(0, 0, 1, 0, 0, 0, 0, -1);
        }

        public void OpenCookerWindow()
        {
            if (OpeningMealPreludePending) { RemindOpeningMeal(); return; }
            OpenRoomWindow("电饭锅", 760, 740, BuildCooker, true);
        }

        private int _wakeMinute = 8 * 60;
        private void BuildBed(Window w, bool showHelp, Action toggleHelp)
        {
            PlayerStats stats = S.Player;
            var status = Ui.Row(w.Content, 28, 12);
            Ui.Size(Ui.Label(status, $"精力 <b>{stats.Energy:0}/{stats.MaxEnergy:0}</b>", 17), 130);
            Ui.Size(Ui.Label(status, $"近 24 小时已睡 <b>{S.SleepHoursLast24:0.#}/8 小时</b>", 17), 225);
            Ui.Size(Ui.Label(status, $"欠觉 <b>{stats.SleepDebt:0.##} 小时</b>", 17,
                color: stats.SleepDebt > 0 ? Ui.Accent : Ui.TextColor), -1, -1, 1).name = "SleepDebt";
            if (S.EnergyExhausted) Line(w.Content, S.EnergyExhaustionText, 17, Ui.Warn);
            bool primaryHome = S.CurrentPlace == S.Home;
            if (primaryHome && !S.HomeHas(FurnitureRole.Bed))
                Line(w.Content, "屋里没有床，睡地铺恢复较慢。", 16, Ui.Warn);

            int now = S.Clock.MinuteOfDay;
            const int dayMinutes = GameTimestamp.MinutesPerDay;
            string WakeLabel(int duration)
            {
                int wake = now + duration;
                return $"{(wake < dayMinutes ? "今天" : "明天")} {Ui.Hm(wake % dayMinutes)} 起床";
            }

            var presets = Ui.Row(w.Content, 74, 8);
            SleepPreset(presets, "睡 8 小时", WakeLabel(8 * 60), () => { if (S.SleepEightHours()) w.Rebuild(); },
                Ui.ButtonHot, S.IsSleepTime);
            SleepPreset(presets, "小睡 2 小时", WakeLabel(2 * 60), () => { if (S.Nap()) w.Rebuild(); },
                Ui.ButtonBg, true);

            var rule = Ui.Panel(w.Content, "SleepSectionRule", Ui.Edge);
            rule.raycastTarget = false;
            Ui.Size(rule, -1, 1);
            Line(w.Content, "自选起床时间", 20);
            _wakeMinute = QuarterHourClock(_wakeMinute);
            int duration = (_wakeMinute - now + dayMinutes) % dayMinutes;
            var picker = ClockSelector(w.Content, "起床", _wakeMinute,
                value => { _wakeMinute = value; w.RequestRebuild(); }, showLabel: false);
            Ui.Size(Ui.Button(picker, "睡到这个时间", () => { if (S.SleepUntil(_wakeMinute)) w.Rebuild(); },
                19, Ui.ButtonHot, S.IsSleepTime && duration > 0), 150, 42);
            string preview = "请选择与当前不同的起床时间";
            if (duration > 0)
            {
                string hours = duration >= 60 ? $"{duration / 60} 小时" : "";
                string minutes = duration % 60 > 0 ? $"{duration % 60} 分钟" : "";
                string durationText = (hours + " " + minutes).Trim();
                preview = $"{WakeLabel(duration)} · 睡 {durationText}";
            }
            Line(w.Content, preview, 17, Ui.Dim).name = "SleepWakePreview";

            var help = Ui.Button(w.Content, (showHelp ? "▾ " : "▸ ") + "睡眠说明", toggleHelp, 16, Ui.PanelBg);
            help.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
            Ui.Size(help, -1, 28);
            if (showHelp)
            {
                Para(w.Content, "小睡也计入近 24 小时的睡眠。精力耗尽后连续清醒 4 小时会疲劳晕倒，睡觉可结束倒计时。", 16, Ui.Dim);
                Para(w.Content, S.WeatherSummary + " · " + S.CityComfortSummary
                    + (primaryHome ? "。床、窗帘和室温会影响休息效果。" : "。这套住宅使用房间里配好的床。"), 16, Ui.Dim);
            }
        }

        private static void SleepPreset(Transform parent, string title, string wake, Action sleep, Color color, bool enabled)
        {
            var button = Ui.Button(parent, title, sleep, 22, color, enabled);
            Ui.Size(button, -1, 74, 1);
            var label = button.GetComponentInChildren<Text>();
            label.fontStyle = FontStyle.Bold;
            label.rectTransform.Anchor(0, 1, 1, 1, 8, -38, 8, 7);
            Ui.Label(button.transform, wake, 16, TextAnchor.MiddleCenter, color == Ui.ButtonHot ? Ui.TextColor : Ui.Dim)
                .rectTransform.Anchor(0, 0, 1, 1, 8, 7, 8, 38);
        }

        private void BuildWardrobe(Window w) { if (_cityClothingPreview == null) _cityClothingPreview = S.EquippedOutfit; BuildCityWardrobe(w); }

        private double _cookKg = 0.5;
        private string _cookIngredient;
        private string _cookFish;

        private void BuildCooker(Window w)
        {
            S.RefreshCooking();
            Pantry pan = S.Pantry;
            PlayerStats st = S.Player;
            double rice = S.HomeCookingRiceKg;
            Line(w.Content, $"饱腹 {st.Hunger:0}  健康 {st.Health:0}   可用大米：{rice:0.##} 千克   背包里的饭盒：{S.Backpack.Lunchboxes} 盒", 17);

            if (pan.Cooking != null)
            {
                long left = pan.CookMinutesLeft(S.Clock.Now.TotalMinutes);
                int readyMinuteOfDay = (int)(pan.Cooking.ReadyMinute % (24 * 60));
                Line(w.Content, $"<color=#80551F>正在焖饭：{pan.Cooking.Batch.Name}，约 {pan.Cooking.Batch.Count} 碗</color>", 20);
                Line(w.Content, $"还剩 {left} 分钟（{readyMinuteOfDay / 60:00}:{readyMinuteOfDay % 60:00} 好）。焖好之前锅盖打不开。", 17);
                if (S.OpeningMealTutorialPending)
                {
                    var wait = Ui.Button(w.Content, $"等到饭好 · {left} 分钟", () => { S.AdvanceClock((int)left); w.Rebuild(); }, 18, Ui.ButtonHot);
                    Ui.Size(wait, -1, 44);
                    wait.name = "OpeningMealWait";

                }
                BindOpeningMealCooker(w);
                return;
            }

            // 焖好了：取饭（吃、装饭盒）；取完才能再焖
            if (pan.CookedBowls > 0)
            {
                Line(w.Content, $"饭煮好了 · 锅里有 {pan.CookedBowls} 碗", 18, Ui.Accent);
                foreach (FoodBatch b in pan.Cooked)
                    ItemLabel(w.Content, "cooked:" + b.Id, $"{b.Name} ×{b.Count}  （{FoodExpiryBadge.Countdown(b.ExpireDay, S.Clock.Now.TotalMinutes)}）", 15, color: Ui.Dim);
                RectTransform erow = Ui.Row(w.Content, 46);
                erow.name = "OpeningMealTake";
                var eat = Ui.Button(erow, "现在吃一碗", () =>
                {
                    S.EatBowlAtHome();
                    w.Rebuild();
                }, 18, Ui.ButtonHot, pan.CookedBowls > 0);
                Ui.Size(eat, 220);
                eat.name = "OpeningMealEat";

                Ui.Size(Ui.Button(erow, "装 1 盒进背包", () =>
                {
                    S.PackLunch(1);
                    w.Rebuild();
                }, 18, Ui.ButtonBg, pan.CookedBowls > 0), 170);
                var packAll = Ui.Button(erow, "全部装进背包", () =>
                {
                    bool tutorial = S.OpeningMealTutorialPending;
                    if (!S.PackLunch(pan.CookedBowls)) return;
                    if (tutorial && !S.OpeningMealTutorialPending)
                    {
                        w.Close();
                        Popup("饭盒已经带好了", "放进背包的食物会在饭点或饿了时自动吃，不用每顿手动操作。\n\n注意饭盒的重量和保质期；放进冰箱可以暂停到期计时。\n\n现在吃饱了，也带好了午饭，可以出门看看了。");
                    }
                    else w.Rebuild();
                }, 18, Ui.ButtonBg, pan.CookedBowls > 0);
                packAll.name = "OpeningMealPackAll"; Ui.Size(packAll, 170);
                Line(w.Content, "饭盒放进背包后，饭点或饿了会自动吃。", 17, Ui.Good);
                Line(w.Content, $"每盒 {Backpack.LunchboxKg:0.##} 千克。锅里的饭取完才能再焖。", 15, Ui.Dim);
                if (S.CanOpenFridge && !S.OpeningMealTutorialPending)
                    Ui.Size(Ui.Button(w.Content, "把做好的饭全部放入冰箱", () => { S.ChillCookedFood(pan.CookedBowls); w.Rebuild(); }, 17, Ui.ButtonBg), -1, 38);
                BindOpeningMealCooker(w);
                return;
            }

            // 空锅：选米、选配菜、开始焖
            Line(w.Content, "自动取用背包和家中后仓的食材。", 16, Ui.Dim);

            if (_cookKg > rice) _cookKg = rice;
            if (_cookKg < Pantry.MinCookRiceKg && rice >= Pantry.MinCookRiceKg) _cookKg = Pantry.MinCookRiceKg;
            Line(w.Content, "往锅里加多少米：", 17);
            RectTransform jrow = Ui.Row(w.Content, 44);
            jrow.name = "OpeningMealRice";
            foreach (double amount in new[] { 0.25, 0.5, 1, 1.5, 2.5 })
            {
                double a = amount;
                Ui.Size(Ui.Button(jrow, a + " 千克", () =>
                {
                    _cookKg = a;
                    if (S.OpeningMealTutorialPending) _openingMealCookStage = 1;
                    w.Rebuild();
                }, 17, Ui.SelectedButton(System.Math.Abs(_cookKg - a) < 1e-6), rice >= a), 90);
            }

            var ingredient = Ingredients.GetRiceCookerRecipe(_cookIngredient);
            _cookIngredient = ingredient?.Id;
            int need = Pantry.IngredientUnitsForRice(_cookKg);
            Line(w.Content, "选择做法", 17);
            RectTransform irow = Ui.Row(w.Content, 44);
            irow.name = "OpeningMealRecipe";
            Ui.Size(Ui.Button(irow, "白饭", () =>
            {
                _cookIngredient = null;
                if (S.OpeningMealTutorialPending) _openingMealCookStage = 2;
                w.Rebuild();
            }, 17, Ui.SelectedButton(_cookIngredient == null)), -1, 44, 1);

            foreach (IngredientDef recipe in Ingredients.RiceCookerRecipes)
            {
                int have = S.HomeCookingIngredientCount(recipe.Id);
                Ui.Size(Ui.Button(irow, recipe.DishName, () =>
                {
                    _cookIngredient = recipe.Id;
                    if (S.OpeningMealTutorialPending) _openingMealCookStage = 2;
                    w.Rebuild();
                }, 17, Ui.SelectedButton(_cookIngredient == recipe.Id), have >= need), -1, 44, 1);
            }

            var fishOptions = FishingCatalog.Fish.Select(f => (fish: f, count: S.HomeCookingIngredientCount(f.Id)))
                .Where(option => option.count > 0).ToArray();
            int fishIndex = Array.FindIndex(fishOptions, option => option.fish.Id == _cookFish);
            var fish = fishIndex < 0 ? null : fishOptions[fishIndex].fish;
            _cookFish = fish?.Id;
            bool enoughFish = fish == null || fishOptions[fishIndex].count >= need;
            if (!S.OpeningMealTutorialPending || fishOptions.Length > 0)
            {
                var fishRow = Ui.Row(w.Content, 36, 8);
                Ui.Size(Ui.Label(fishRow, "加鱼", 17), 72, 36);
                if (fishOptions.Length == 0)
                    Ui.Size(Ui.Label(fishRow, "背包和家中后仓暂无鱼", 16, color: Ui.Dim), -1, 36, 1);
                else
                    Ui.Size(Ui.Choice(fishRow, fishOptions.Select(option => $"{option.fish.Name} · {option.fish.Rarity} · {option.count} 条").Prepend("不加鱼"),
                        fishIndex + 1, index => { _cookFish = index == 0 ? null : fishOptions[index - 1].fish.Id; w.Rebuild(); }), -1, 36, 1)
                        .name = "CookFishChoice";
            }
            if (fish != null)
                Line(w.Content, $"本锅用 {fish.Name} {need} 条" + (enoughFish ? "" : " · 库存不足，可少放米或换鱼"),
                    16, enoughFish ? Ui.Dim : Ui.Warn);
            string meal = Pantry.MealId(_cookIngredient, _cookFish);
            string effects = GameSession.MealEffects(meal);
            if (fish != null)
            {
                double hunger = Pantry.BaseBowlHunger + (ingredient?.HungerBonus ?? 0) + fish.MealHungerBonus;
                double health = (ingredient?.HealthBonus ?? 0) + fish.MealHealthBonus;
                effects = $"饱腹 +{hunger:0.#} · 健康 +{health:0.##} · " + effects;
            }
            Line(w.Content, $"{GameSession.MealName(meal)} · {effects}（每份）", 16, Ui.Accent);
            int available = ingredient == null ? 0 : S.HomeCookingIngredientCount(ingredient.Id);
            bool enoughIngredient = ingredient == null || available >= need;
            if (ingredient != null)
                Line(w.Content, $"本锅用 {ingredient.Name} {need * ingredient.WeightKg:0.##} 千克 · 库存 {available * ingredient.WeightKg:0.##} 千克"
                    + (enoughIngredient ? "" : " · 可少放米或换做法"), 16, enoughIngredient ? Ui.Dim : Ui.Warn);
            bool enoughRice = rice >= Pantry.MinCookRiceKg;
            if (!enoughRice) Line(w.Content, "可用大米不足 0.25 千克。", 16, Ui.Warn);
            int bowls = Mathf.RoundToInt((float)(_cookKg * Pantry.BowlsPerKg));
            var start = Ui.Button(w.Content, $"开始焖饭：{_cookKg:0.##} 千克，约 {bowls} 碗，{Pantry.CookMinutes} 分钟后好", () =>
            {
                if (S.CookRice(_cookKg, _cookIngredient, _cookFish))
                {
                    _cookFish = null;
                    Toast($"开始焖饭，{Pantry.CookMinutes} 分钟后好。");
                }
                w.Rebuild();
            }, 19, Ui.ButtonHot, enoughRice && enoughIngredient && enoughFish);
            Ui.Size(start, -1, 48);
            start.name = "OpeningMealStart";

            if (!S.OpeningMealTutorialPending) Line(w.Content, "点了就开始焖，焖的时候锅里不能动，人可以先去忙别的。", 15, Ui.Dim);
            BindOpeningMealCooker(w);
        }

        // ---------- “我的” ----------

        private string WorkbenchAccessReason
        {
            get
            {
                if (S.CanUseHomeWorkbench) return null;
                if (!S.DebugHomeWorkbenchEnabled && !S.HomeWorkbenchInstalled)
                {
                    if (!S.OwnsHomeWorkbench) return S.HomeWorkbenchInTransit ? "居家办公台正在送货" : "到米家购买居家办公台";
                    if (S.HomeTier < GameSession.HomeWorkbenchMinimumResidenceTier) return "需要中型及以上住宅安装";
                    return "回家摆放居家办公台";
                }
                return !_ctrl.InHome ? "回家后可用" : S.IsWorking ? "下班后可用" : "结束当前活动或行程后可用";
            }
        }
        private enum MyPage { Profile, Food, Residence, Contacts, Relations }
        private MyPage _myPage;
        private Window _myWindow;
        private bool _myLeaseExpanded;
        private AvatarPreview _myPortrait;
        private string _myPortraitOutfit;

        public void OpenMyPanel() => OpenMyPanel(_myPage);

        private void OpenMyPanel(MyPage page)
        {
            bool changed = _myPage != page;
            _myPage = page;
            if (_myWindow != null && !_myWindow.Closed)
            {
                _myWindow.Root.SetAsLastSibling();
                _myWindow.ResetScrollOnRebuild |= changed;
                _myWindow.Rebuild();
                return;
            }
            if (OpeningMealRestricted) { RemindOpeningMeal(); return; }
            _myWindow = OpenWindow("我的", 1240, 760, BuildMyPanel, scrollContent: false, onClosed: () =>
            {
                _myWindow = null;
                _myPortrait?.Destroy();
                _myPortrait = null;
                _myPortraitOutfit = null;
            });
            _myWindow.Root.name = "Workspace_personal";
        }

        private void BuildMyPanel(Window w)
        {
            var nav = Ui.Row(w.Content, 42, 6);
            string[] names = { "个人资料", "饮食", "住处", "通讯录", "人际关系图" };
            for (int i = 0; i < names.Length; i++)
            {
                MyPage tab = (MyPage)i;
                var button = Ui.Size(Ui.Button(nav, names[i], () =>
                {
                    if (_myPage != tab) OpenMyPanel(tab);
                }, 18, Ui.SelectedButton(_myPage == tab)), 0, 42, 1);
                button.name = "MyTab_" + i;
            }

            if (_myPage == MyPage.Contacts)
            {
                BuildContactsContent(w, flexible: true);
                return;
            }
            if (_myPage == MyPage.Relations)
            {
                BuildRelationsContent(w, 770, 535, showPlayerPortrait: true);
                return;
            }

            var body = PhoneFlexibleScroll(w.Content, "MyPageContent");
            body.parent.parent.GetComponent<Image>().color = Color.clear;
            switch (_myPage)
            {
                case MyPage.Food: BuildMyFood(w, body); break;
                case MyPage.Residence: BuildMyResidence(w, body); break;
                default: BuildMyProfile(w, body); break;
            }
        }

        private Texture MyPortraitTexture()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return null;
            _myPortrait ??= new AvatarPreview(portrait: true);
            string outfit = S.Outfit.Id;
            if (_myPortraitOutfit != outfit)
            {
                _myPortrait.Render(outfit);
                _myPortraitOutfit = outfit;
            }
            return _myPortrait.Texture;
        }

        private static RectTransform MyCard(Transform parent, string name, float width, float height, float flex = 0)
        {
            var card = Ui.Panel(parent, name, Ui.PanelBg2).rectTransform;
            Ui.Size(card, width, height, flex);
            Ui.VList(card, 7, 12);
            return card;
        }

        private static void MyFact(Transform parent, string title, string value)
        {
            var fact = MyCard(parent, title, 0, 70, 1);
            var layout = fact.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 6);
            layout.spacing = 0;
            Ui.Size(Ui.Label(fact, title, 15, color: Ui.Dim), -1, 20);
            Ui.Size(Ui.Label(fact, "<b>" + value + "</b>", 23), -1, 33);
        }

        private void BuildMyProfile(Window w, Transform body)
        {
            var stats = S.Player;
            var top = Ui.Row(body, 194, 10);

            var identity = MyCard(top, "MyIdentity", 397, 194);
            var intro = Ui.Row(identity, 170, 14);
            var portrait = Ui.Size(Ui.Panel(intro, "MyPortraitFrame", Ui.Accent), 126, 162).rectTransform;
            var face = Ui.Rect(portrait, "MyPortrait").Fill(2).gameObject.AddComponent<RawImage>();
            face.texture = MyPortraitTexture();
            face.raycastTarget = false;
            if (face.texture == null) face.color = Ui.PanelBg;
            var info = Ui.Rect(intro, "MyIdentityText");
            Ui.Size(info, 0, 162, 1);
            var layout = Ui.VList(info, 8, 5);
            layout.childAlignment = TextAnchor.MiddleLeft;
            Line(info, "玩家", 30);
            double fame = S.HasShop ? S.PlayerShop.Reputation : 0;
            Line(info, $"名望 {fame:0.#} · {PersonStanding.Label(fame)}", 19, Ui.Accent).name = "MyStanding";

            var condition = MyCard(top, "MyCondition", 0, 194, 1);
            Line(condition, "身体状态", 22, Ui.Accent);
            void Meter(Transform row, string label, double value, double max, bool warn = false)
            {
                var cell = Ui.Size(Ui.Rect(row, "MyStat_" + label), 0, 43, 1);
                var heading = Ui.Row(cell, 26);
                heading.Anchor(0, 1, 1, 1, 0, -26, 0, 0);
                Ui.Size(Ui.Label(heading, label, 16, color: Ui.Dim), 0, 26, 1);
                Ui.Size(Ui.Label(heading, $"{value:0} / {max:0}", 17, TextAnchor.MiddleRight, warn ? Ui.Warn : Ui.TextColor), 110, 26);
                var rail = Ui.Panel(cell, "StatRail", Ui.PanelBg).rectTransform.Anchor(0, 0, 1, 0, 0, 4, 0, -12);
                Ui.Panel(rail, "StatFill", warn ? Ui.Warn : Ui.Good).rectTransform.Anchor(0, 0, Mathf.Clamp01((float)(value / max)), 1);
            }
            var first = Ui.Row(condition, 43, 16);
            Meter(first, "饱腹", stats.Hunger, 100, stats.Hunger <= 20);
            Meter(first, "精力", stats.Energy, Math.Max(1, stats.MaxEnergy), S.EnergyExhausted);
            var second = Ui.Row(condition, 43, 16);
            Meter(second, "健康", stats.Health, 100, stats.Health <= 25);
            Meter(second, "压力", stats.Stress, 100, stats.Stress >= PlayerStats.HighStressThreshold);
            if (S.EnergyExhausted) Line(condition, S.EnergyExhaustionText, 15, Ui.Warn);

            Line(body, "个人能力", 20, Ui.Accent);
            var skills = Ui.Row(body, 70, 5);
            MyFact(skills, "智力", stats.Intelligence.ToString());
            MyFact(skills, "魅力", S.EffectiveCharm.ToString());
            MyFact(skills, "动手能力", stats.Skill.ToString());
            MyFact(skills, "体能", stats.Stamina.ToString());
            Ui.Size(Ui.Button(body, "天气与穿衣", OpenCityClimate, 17), -1, 38);
            HelpLink(body, "身体详细资料", $"欠觉 {stats.SleepDebt:0.#} 小时 · 营养 {stats.Nutrition:0.#}/100 · 心情 {stats.Mood:0.#}/100\n衣着魅力 +{S.Outfit.CharmBonus} · 现金 {S.Ledger.Balance}\n压力达到 {PlayerStats.HighStressThreshold:0} 时，到岗表现奖金减少 {GameSession.HighStressWorkPenalty}%。");
            BuildPlayerWorkSummary(body, w);

            string blocker = WorkbenchAccessReason;
            Ui.Size(Ui.Button(body, "居家工作台" + (blocker == null ? "" : " · " + blocker), OpenHomeWorkbench,
                17, blocker == null ? Ui.ButtonHot : Ui.ButtonBg, blocker == null), -1, 38).name = "MyHomeWorkbench";
            if (blocker == "回家后可用")
                Ui.Size(Ui.Button(body, "回家", () => { CloseModalWindows(); _ctrl.GoToPlace(S.Home); }, 17), -1, 36);
        }

        private void BuildMyFood(Window w, Transform body)
        {
            var pantry = S.Pantry;
            Line(body, "饮食", 22, Ui.Accent);
            var stats = Ui.Row(body, 70, 6);
            MyFact(stats, "当前饱腹", $"{S.Player.Hunger:0} / 100");
            MyFact(stats, "电饭锅熟饭", $"{pantry.CookedBowls} 碗");
            if (pantry.Cooking != null) Line(body, $"焖饭剩余 {pantry.CookMinutesLeft(S.Clock.Now.TotalMinutes)} 分钟", 17, Ui.Good);
            var ingredients = Ingredients.All.Select(def => (def, count: S.HomeCookingIngredientCount(def.Id)))
                .Where(item => item.count > 0).Select(item => $"{item.def.Name} {item.count * item.def.WeightKg:0.##} 千克");
            Para(body, $"背包和家中后仓 · 大米 {S.HomeCookingRiceKg:0.##} 千克" + string.Concat(ingredients.Select(label => " · " + label)), 17);
            if (S.CanOpenFridge) Ui.Size(Ui.Button(body, "打开当前冰箱", OpenFridgeWindow, 17), -1, 38);
            Ui.Size(Ui.Button(body, "自动订餐 · " + (S.MealDeliveryEnabled ? S.SelectedDeliveryMeal.Name + " ¥" + S.SelectedDeliveryMeal.Price + "/餐" : "关闭"),
                () => OpenMealDeliverySettings(w), 17), -1, 38);
            HelpLink(body, "用餐与保存说明", "到饭点或饥饿时优先吃现有食物，缺餐时按订餐设置处理。冰箱内永久保存，取出后继续消耗剩余保质期。餐馆可用厨房现有大米和配菜做自己的餐食。");
        }

        private void BuildMyResidence(Window w, Transform body)
        {
            Line(body, "我的租约与产权", 22, Ui.Accent);
            bool rented = S.HasResidentialLease(S.Home);
            var residence = MyCard(body, "MyCurrentResidence", -1, 161);
            Line(residence, "当前住处", 15, Ui.Dim);
            Line(residence, S.HomeTitle + (rented ? " · 租住中" : " · 自有"), 23);
            var facts = Ui.Row(residence, 70, 6);
            MyFact(facts, rented ? "周租" : "购入价", rented ? S.CurrentHomeRent.ToString() : Money.FromCents(S.HomePriceCents).ToString());
            MyFact(facts, "水电", S.HomeUtilities + "/月");
            MyFact(facts, "睡眠恢复", $"{S.PrimaryHomeSleepQuality:0.##}×");

            if (rented)
            {
                var expand = Ui.Size(Ui.Button(body, _myLeaseExpanded ? "收起当前租约 ▴" : "查看当前租约 ▾",
                    () => { _myLeaseExpanded = !_myLeaseExpanded; w.Rebuild(); }, 17), -1, 38);
                expand.name = "MyLeaseDetails";
                if (_myLeaseExpanded)
                {
                    Line(body, $"签约周租 {S.CurrentHomeRent} · 每周扣款", 17);
                    if (S.NextResidentialRentDay(S.Home) is int day)
                        Line(body, "下次扣租 · " + new GameDate(day), 16, Ui.Dim);
                }
            }

            var otherLeases = S.RentedResidences.Where(place => place != S.Home).ToArray();
            if (otherLeases.Length > 0)
            {
                Line(body, "其他租约", 19, Ui.Accent);
                foreach (var place in otherLeases)
                {
                    string due = S.NextResidentialRentDay(place) is int day ? $" · 下次扣租 {new GameDate(day)}" : "";
                    Line(body, $"{place.Name} · 周租 {S.SignedResidentialRent(place)}{due}", 17);
                }
            }

            Line(body, "自有住宅", 19, Ui.Accent);
            var owned = S.ResidentialPlaces.Where(place => place != S.Home)
                .Select(place => (place, property: S.PropertyAt(place.Id))).Where(entry => entry.property != null).ToArray();
            if (!rented) Line(body, S.HomeTitle + " · 自住", 17);
            foreach (var (place, property) in owned)
                Line(body, place.Name + (property.UseMode == "vacant" ? " · 空置" : $" · 出租 · 周租 {Money.FromCents(property.WeeklyRentCents)}"), 17);
            if (rented && owned.Length == 0) Line(body, "暂无", 16, Ui.Dim);
            if (S.HomeVisits.Any(visit => visit.Working))
                Ui.Size(Ui.Button(body, "在家等维修完成", () => { S.WaitForRepair(); w.Rebuild(); }, 17, Ui.ButtonHot, _ctrl.InHome), -1, 38);
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
                default: return "大米 5 千克" + (p.Count > 1 ? "×" + p.Count : "");
            }
        }

        // ---------- 开始摆放 ----------

        /// <summary>后仓 / 临时存放里的家具点“摆出来”：进摆放模式，手里直接拿着这件。</summary>
        private void PlaceFromStorage(bool shop, string furnitureId)
        {
            StartPlacement(shop, furnitureId);
        }

        private void StartPlacement(bool shop, string furnitureId = null)
        {
            void Begin()
            {
                if (_ctrl.BeginPlacement(shop) && furnitureId != null) _ctrl.SelectStock(furnitureId);
            }
            if (!shop)
            {
                Begin();
                return;
            }
            if (S.IsRenovating)
            {
                Begin();
                return;
            }
            if (!S.ShopFitted)
            {
                // 毛坯铺子本来就没开张，不用再确认
                if (!S.BeginRenovation()) return;
                Begin();
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
                    Begin();
                }, 19, Ui.Warn), 300);
                Ui.Size(Ui.Button(row, "再想想", w.Close, 19), 140);
            });
        }

        // ---------- 网购 ----------

        private int _onlineTab;

        public void OpenOnlinePanel() => OpenOnlinePanel(null);

        private void OpenOnlinePanel(OwnedShop preferredShop)
        {
            string appBlock = S.AppBlocker(PhoneApps.Shop);
            if (appBlock != null)
            {
                Toast(appBlock);
                return;
            }
            OwnedShop destination = preferredShop ?? S.LastPurchaseDeliveryShop;
            OpenPhoneAppWindow("网购", w =>
            {
                RectTransform tabs = Ui.Row(w.Content, 46);
                string[] names = { "家具", "店里摆设", "衣服", "礼品", "大米", "电脑配件" };
                for (int i = 0; i < names.Length; i++)
                {
                    int tab = i;
                    Ui.Size(Ui.Button(tabs, names[i], () =>
                    {
                        _onlineTab = tab;
                        w.Rebuild();
                    }, 18, Ui.SelectedButton(_onlineTab == tab)), 130);
                }
                var destinations = new OwnedShop[] { null }.Concat(S.MyShops).ToArray();
                int selected = Array.IndexOf(destinations, destination);
                if (selected < 0) { destination = null; selected = 0; }
                var delivery = Ui.Row(w.Content, 42, 8);
                Ui.Size(Ui.Label(delivery, "送到", 17), 44);
                Ui.Size(Ui.Choice(delivery, destinations.Select(S.CourierDestName), selected,
                    index => { destination = destinations[index]; w.Rebuild(); }, bg: Ui.ButtonHot), 400, 40).name = "OnlineDeliveryDestination";
                Ui.Size(Ui.Label(delivery, _onlineTab == 5 ? "次日 10:00 送达" : $"{GameSession.DeliveryDays} 天后送达",
                    16, color: Ui.Dim), -1, -1, 1);
                if (_onlineTab == 5) { BuildOnline(w, destination); return; }
                BuildOnlineParcels(w, destination);
            });
        }

        // ---------- 家具城、农贸市场 ----------

        private void OpenMall(Place p)
        {
            const string firstShopFilterSeen = "tutorial:mall-shop-filter-seen";
            bool useFirstShop = S.MainShop.Rented && S.PendingShopKindPlaceId == null && S.Flags.Add(firstShopFilterSeen);
            _shopTabs[p.Id] = useFirstShop ? RetailShopTypes.Name(S.MainShop.RetailKind) : PurchaseFacets.All;
            _shopTabs.Remove(p.Id + ":facility");
            OpenPlaceWindow(p, BuildCarryStore, StoreWindowWidth, 960);
        }

        private readonly Dictionary<string, string> _shopTabs = new Dictionary<string, string>();

        /// <summary>店里货品分类的标签栏（窗口最上面），返回当前选中的那一类；每家店各记各的。</summary>
        private string ShopTabs(Window w, Place p, IList<string> tabs, string fallback = null, string filterKey = null, string label = null)
        {
            string key=filterKey==null?p.Id:p.Id+":"+filterKey;
            if (!_shopTabs.TryGetValue(key, out string cur) || !tabs.Contains(cur)) cur = fallback ?? tabs[0];
            RectTransform bar = Ui.Row(w.Content, 42, 6);
            if(label!=null)Ui.Size(Ui.Label(bar,label,17,TextAnchor.MiddleLeft,Ui.Dim),60);
            foreach (string t in tabs)
            {
                string tab = t;
                bool on = tab == cur;
                var b = Ui.Button(bar, tab, () =>
                {
                    _shopTabs[key] = tab;
                    w.Rebuild();
                }, 17, Ui.SelectedButton(on));
                b.name = "ShopTab_" + (filterKey==null?"":filterKey+"_") + tab;
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

        private static Color CellFree => Ui.PanelBg2;

        /// <summary>以前寄存在店里的货：自己来搬（装不下就分几趟），或者全叫货车送回家。</summary>
        private void BuildMallStash(Window w, RectTransform stash)
        {
            Line(stash, $"寄存在店里的货（已付过钱，不过期）{S.MallStashCount} 件，共 {S.MallStashKg:0.##} 千克", 17, Ui.Accent);
            foreach (MallCartLine l in S.MallStash.ToList())
            {
                FurnitureDef d = FurnitureCatalog.Get(l.Id);
                RectTransform row = Ui.Row(stash, 40);
                ItemPortrait(row, "furn:" + l.Id, 34);
                Ui.Size(Ui.Label(row, $"{d.Name} ×{l.Count}　<size=14><color=#655E52>每件 {d.WeightKg:0.##} 千克</color></size>", 17), -1, -1, 1);
                bool fits = d.WeightKg <= S.FreeKg + 1e-9;
                Ui.Size(Ui.Button(row, fits ? "搬走 1 件" : "装不下", () =>
                {
                    S.TakeFromStash(l.Id);
                    w.Rebuild();
                }, 16, Ui.ButtonBg, fits), 110);
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
            RectTransform row = Ui.Row(list, 80);
            Ui.Size(ProductText(row, ProductLabel(def.Name, def.WeightKg, def.Description), 18), -1, -1, 1);
            Ui.Size(Ui.Label(row, def.Price.ToString(), 19, TextAnchor.MiddleLeft, Ui.Good), 90);
            int have = S.FurnitureOwnedUnplaced(def.Id);
            int placedShop = S.ShopLayout.Count(p => p.Id == def.Id) + S.HomeLayout.Count(p => p.Id == def.Id);
            string haveText = (have > 0 ? $"已有 ×{have}" : "") + (have > 0 && placedShop > 0 ? "\n" : "") + (placedShop > 0 ? $"已摆 ×{placedShop}" : "");
            Ui.Size(Ui.Label(row, haveText, 14, TextAnchor.MiddleLeft, Ui.Dim), 80);
            CartAddButton(w, row, "furn:" + def.Id, () => S.CartAddFurniture(def.Id), "加入购物车", 160);
        }

        private void BuildTobacco(Window w, Place p)
        {
            BuildCarryStore(w,p);
        }

        private static string TobaccoTab(GiftDef g) =>
            g.Id == Gifts.Cig || g.Id == Gifts.CigHi ? "烟" : g.Id == Gifts.Wine || g.Id == Gifts.Baijiu ? "酒" : "饮料点心";

        private void ShopGiftRow(Window w, RectTransform list, GiftDef def, bool open)
        {
            RectTransform row = Ui.Row(list, 80);
            Ui.Size(ProductText(row, ProductLabel(def.Name, def.WeightKg, def.Description), 18), -1, -1, 1);
            Ui.Size(Ui.Label(row, def.Price.ToString(), 19, TextAnchor.MiddleLeft, Ui.Good), 90);
            int have = S.Backpack.GiftCount(def.Id);
            Ui.Size(Ui.Label(row, have > 0 ? $"背包 ×{have}" : "", 15, TextAnchor.MiddleLeft, Ui.Dim), 80);
            CartAddButton(w, row, "gift:" + def.Id, () => S.CartAddGift(def.Id), "加入购物车", 150, open);
        }

        private bool TryOpenSignHelp(Place place, string activityId = null)
        {
            var activity = S.SignHelpAt(place);
            if (activity == null || S.CurrentPlace != place) return false;
            bool inArrivalWindow = System.Math.Abs(S.Clock.Now.TotalMinutes - activity.ScheduledStart) <= GameSession.CityArrivalWindowMinutes;
            if (activityId != null ? activityId != activity.Id : activity.State != CityActivityPhase.Running && !inArrivalWindow)
                return false;

            S.PrepareVenueCompanions(place, activity.Id);
            OpenSignHelp(activity);
            return true;
        }

        private void OpenSignHelp(CityActivity activity)
        {
            var definition = S.CityActivityService(activity);
            bool running = activity.State == CityActivityPhase.Running;
            string blocker = running ? null : S.SignHelpStartBlocker();
            var window = OpenWindow(definition.Name, 740, 400, w =>
            {
                if (!activity.Active || S.CurrentPlace?.Id != activity.VenueId)
                {
                    Line(w.Content, activity.EndReason ?? "这次帮忙已经结束。", 18);
                    return;
                }

                Line(w.Content, $"{S.CurrentPlace.Name} · 耗时 {activity.Duration} 分钟", 18, Ui.Dim);
                if (activity.State == CityActivityPhase.Running)
                {
                    var countdown = Line(w.Content, "", 20, Ui.Good);
                    var progress = Para(w.Content, "", 18);
                    void Refresh()
                    {
                        if (!activity.Active || S.CurrentPlace?.Id != activity.VenueId) { w.Close(); return; }
                        long remaining = System.Math.Max(0, activity.ActualStart + activity.Duration - S.Clock.Now.TotalMinutes);
                        countdown.text = $"正在换招牌 · 剩余 {remaining} 分钟";
                        progress.text = activity.Stage == 0 ? definition.StartText : S.CityActivityMidText(activity);
                    }
                    w.RefreshContent = Refresh;
                    Refresh();
                    return;
                }

                Line(w.Content, $"约定 {CityTime(activity.ScheduledStart)}", 18, Ui.Dim);
                Para(w.Content, "和唐俊杰一起拆下旧招牌，再把新招牌装好。", 18);
                string why = S.SignHelpStartBlocker();
                if (why != null) Ui.Notice(w.Content, why);
                Ui.Size(Ui.Button(w.Content, "开始帮忙", () =>
                {
                    if (!S.StartSignHelp()) { w.Rebuild(); return; }
                    CloseAllWindows();
                    S.Time.SetPaused(false);
                    OpenSignHelp(activity);
                }, 20, Ui.ButtonHot, why == null), -1, 48).name = "StartSignHelp";
            }, holds: !running && blocker == null);

            if (!running && blocker != null)
            {
                window.LiveConversation = true;
                window.LiveKey = () => S.SignHelpStartBlocker();
                window.LiveStamp = window.LiveKey();
            }
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
                string tail = blocker != null ? $"\n<size=14><color=#9C4634>{blocker}</color></size>" : "";
                Ui.Size(Ui.Label(row, $"{u.Name}\n<size=13><color=#655E52>购买后安装，不占背包</color></size>\n<size=13>{u.Description}</size>{tail}", 17), 400);
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

        // ---------- 摆放模式面板 ----------

        internal const float HomeDockWidth = 430f;
        private Button _homeDockToggle, _homePlacementConfirm;
        private RectTransform _homeDock, _homeDockContent;
        private Text _homeDockToggleLabel, _homeDockTitle, _homeDockTenure, _homePlacementStatus;
        private bool _homeDockCollapsed;
        private GameSession _homeDockSession;
        private string _homeDockResidence;
        private KeyValuePair<string, int>[] _homeDockStored = Array.Empty<KeyValuePair<string, int>>();
        private bool HomeDockOpen => _homeDock != null && _homeDock.gameObject.activeSelf;

        private void BuildHomeDockToggle(Transform parent)
        {
            _homeDockToggle = Ui.Button(parent, "收起住处面板 ▴", () =>
            {
                _homeDockCollapsed = HomeDockOpen;
                if (_homeDockCollapsed && _ctrl.Placing && !_ctrl.PlacingShop) _ctrl.EndPlacement();
                RefreshHomeDockVisibility();
            }, 17);
            _homeDockToggle.name = "HomeDockToggle";
            _homeDockToggleLabel = _homeDockToggle.GetComponentInChildren<Text>();
            ((RectTransform)_homeDockToggle.transform).Anchor(1, 1, 1, 1, -200, -112, 12, 74);
            _homeDockToggle.gameObject.SetActive(false);
        }

        private void RefreshHomeDockVisibility(bool force = false)
        {
            string residence = _ctrl.InHome ? S.Home.Id : null;
            if (_homeDockSession != S || _homeDockResidence != residence)
            {
                _homeDockSession = S;
                _homeDockResidence = residence;
                _homeDockCollapsed = false;
                force = true;
            }
            bool visible = _ctrl.InHome && !MainMenuOpen && !S.IsGameOver && !S.IsWorking
                && !OpeningMealRestricted && !ExpoHudOpen && !_ctrl.PlacingShop;
            if (_homeDockToggle.gameObject.activeSelf != visible) _homeDockToggle.gameObject.SetActive(visible);
            bool showDock = visible && !_homeDockCollapsed;
            if (showDock && _homeDock == null) BuildHomeDock();
            bool changed = _homeDock != null && _homeDock.gameObject.activeSelf != showDock;
            if (changed) _homeDock.gameObject.SetActive(showDock);
            if (visible) _homeDockToggleLabel.text = showDock ? "收起住处面板 ▴" : "展开住处面板 ▾";
            if (showDock && (changed || force)) RefreshHomeDock(force: true);
        }

        private void BuildHomeDock()
        {
            _homeDock = Ui.Panel(_dockCanvas.transform, "HomeDock", Ui.PanelBg).rectTransform
                .Anchor(1, 0, 1, 1, -HomeDockWidth, 70, 10, 120);
            var header = Ui.Panel(_homeDock, "HomeDockHeader", Ui.PanelBg2).rectTransform.Anchor(0, 1, 1, 1, 0, -76, 0, 0);
            Ui.Label(header, "<b>我的住处</b>", 24).rectTransform.Anchor(0, 1, 1, 1, 13, -40, 102, 7);
            _homeDockTitle = Ui.Label(header, "", 17, color: Ui.Dim);
            _homeDockTitle.rectTransform.Anchor(0, 1, 1, 1, 13, -67, 102, 37);
            _homeDockTitle.resizeTextForBestFit = true;
            _homeDockTitle.resizeTextMinSize = 13;
            _homeDockTitle.resizeTextMaxSize = 17;
            _homeDockTenure = Ui.Label(header, "", 15, TextAnchor.MiddleRight, Ui.Good);
            _homeDockTenure.rectTransform.Anchor(1, 1, 1, 1, -98, -43, 13, 8);
            _homeDockContent = Ui.Scroll(_homeDock);
            ((RectTransform)_homeDockContent.parent.parent).Anchor(0, 0, 1, 1, 5, 53, 5, 78);
            var details = Ui.Button(_homeDock, "查看我的住处资料 ›", () => OpenMyPanel(MyPage.Residence), 17);
            details.name = "HomeDockResidence";
            ((RectTransform)details.transform).Anchor(0, 0, 1, 0, 10, 8, 10, -45);
            RefreshHomeDock(force: true);
        }

        private void RefreshHomeDock(bool force = false)
        {
            if (!HomeDockOpen) return;
            var stored = S.FurnitureToPlaceList(false).Where(entry => entry.Value > 0 && FurnitureCatalog.Get(entry.Key) != null).ToArray();
            string tenure = S.HasResidentialLease(S.Home) ? "租住中" : "自有";
            if (!force && _homeDockStored.SequenceEqual(stored)
                && _homeDockTitle.text == S.HomeTitle && _homeDockTenure.text == tenure) return;
            _homeDockStored = stored;
            _homeDockTitle.text = S.HomeTitle;
            _homeDockTenure.text = tenure;
            var scroll = _homeDockContent.parent.parent.GetComponent<ScrollRect>();
            float previousScroll = scroll.verticalNormalizedPosition;
            Ui.Clear(_homeDockContent);
            Line(_homeDockContent, "装修", 21, Ui.Accent);

            _homePlacementStatus = null;
            _homePlacementConfirm = null;
            bool editing = _ctrl.Placing && !_ctrl.PlacingShop;
            if (!editing)
            {
                Ui.Size(Ui.Button(_homeDockContent, "开始装修", () => StartPlacement(false), 17, Ui.ButtonHot), -1, 38)
                    .name = "HomeDockBeginPlacement";
            }
            else if (_ctrl.PlaceId != null)
            {
                Line(_homeDockContent, (_ctrl.PlaceHoldsPlaced ? "移动 · " : "摆放 · ") + FurnitureLabel(_ctrl.PlaceId, 1), 18, Ui.Accent);
                _homePlacementStatus = Line(_homeDockContent, "", 16, Ui.Dim);
                Ui.Size(_homePlacementStatus, -1, 32);
                var actions = Ui.Row(_homeDockContent, 36, 4);
                Ui.Size(Ui.Button(actions, "旋转（R）", _ctrl.RotateGhost, 15), 86, 36);
                _homePlacementConfirm = Ui.Size(Ui.Button(actions, _ctrl.PlaceHoldsPlaced ? "确认移动" : "确认摆放",
                    _ctrl.ConfirmHomeFurniturePlacement, 16, Ui.ButtonHot), 0, 36, 1);
                _homePlacementConfirm.name = "HomeDockConfirmPlacement";
                Ui.Size(Ui.Button(actions, "取消", _ctrl.CancelPlacementSelection, 15), 60, 36).name = "HomeDockCancelPlacement";
                UpdateHomePlacementStatus();
            }
            else
            {
                Para(_homeDockContent, "点击房间里的家具可移动，选中后按 X 收纳。", 15, Ui.Dim);
            }

            Line(_homeDockContent, $"可摆放家具  ·  {stored.Sum(entry => entry.Value)} 件", 17, Ui.Dim);
            foreach (var entry in stored)
            {
                string id = entry.Key;
                var row = HomeDockFurnitureRow(id, FurnitureLabel(id, entry.Value), _ctrl.PlaceId == id && !_ctrl.PlaceHoldsPlaced);
                Ui.Size(Ui.Button(row, "摆放", () =>
                {
                    if (_ctrl.Placing && !_ctrl.PlacingShop) _ctrl.SelectStock(id);
                    else StartPlacement(false, id);
                }, 16, Ui.ButtonBg), 74, 34).name = "HomeDockPlace_" + id;
            }
            if (stored.Length == 0) Line(_homeDockContent, "暂无可摆放家具", 15, Ui.Dim);
            if (editing)
            {
                var history = Ui.Row(_homeDockContent, 36, 5);
                Ui.Size(Ui.Button(history, "撤销 Ctrl+Z", () => _ctrl.UndoPlacement(), 14, Ui.ButtonBg, S.CanUndoLayout), 0, 36, 1);
                Ui.Size(Ui.Button(history, "重做 Ctrl+Y", () => _ctrl.UndoPlacement(true), 14, Ui.ButtonBg, S.CanRedoLayout), 0, 36, 1);
                Ui.Size(Ui.Button(_homeDockContent, "完成装修", _ctrl.EndPlacement, 17, Ui.ButtonHot), -1, 38).name = "HomeDockFinishPlacement";
            }
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = editing && _ctrl.PlaceId != null ? 1 : previousScroll;
        }

        private RectTransform HomeDockFurnitureRow(string id, string label, bool selected)
        {
            var row = Ui.Row(_homeDockContent, 40, 5);
            var background = row.gameObject.AddComponent<Image>();
            background.color = selected ? Ui.ButtonBg : Ui.PanelBg2;
            background.raycastTarget = false;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 5, 0, 0);
            layout.childForceExpandHeight = false;
            ItemPortrait(row, "furn:" + id, 34);
            Ui.Size(Ui.Label(row, label, 16), 0, 34, 1);
            return row;
        }

        private void UpdateHomePlacementStatus()
        {
            if (!HomeDockOpen || _homePlacementStatus == null || _homePlacementConfirm == null) return;
            bool picked = _ctrl.HasHomePlacementTarget;
            string issue = _ctrl.PlaceError;
            _homePlacementStatus.text = !picked ? "点击房间选择位置" : issue ?? "可以放在这里 · 点确认完成";
            _homePlacementStatus.color = !picked ? Ui.Dim : issue == null ? Ui.Good : Ui.Warn;
            _homePlacementConfirm.interactable = picked && issue == null;
        }

        private RectTransform _placePanel;
        private Text _placeStatus;

        public void ShowPlacementPanel()
        {
            if (!_ctrl.PlacingShop)
            {
                _homeDockCollapsed = false;
                RefreshHomeDockVisibility(force: true);
                return;
            }
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
            if (_ctrl.InInterior && S != null&&!_dockCollapsed) OpenDock();
            if (S != null) RefreshHomeDockVisibility(force: true);
        }

        public void RefreshPlacementPanel()
        {
            if (_ctrl.Placing && !_ctrl.PlacingShop) { RefreshHomeDock(force: true); return; }
            if (_placePanel == null || !_placePanel.gameObject.activeSelf) return;
            Ui.Clear(_placePanel);
            bool shop = _ctrl.PlacingShop;
            Text title = Ui.Label(_placePanel, shop ? "装修店面（今天歇业）" : "摆放家具", 22, TextAnchor.MiddleLeft, Ui.Accent);
            Ui.Size(title, -1, 34);
            Text mode = Ui.Label(_placePanel, "俯视图 · 格线 1 米 · 吸附 0.25 米", 15, TextAnchor.MiddleLeft, Ui.Dim);
            Ui.Size(mode, -1, 24);
            Text help = Ui.Label(_placePanel, "左键放置或挪动 · R 旋转 · Esc 放回\nX 收纳 · 滚轮缩放 · Q/E 转视角 · V 复位", 15, TextAnchor.UpperLeft, Ui.Dim);
            Ui.Size(help, -1, 44);
            _placeStatus = Ui.Label(_placePanel, "", 16, TextAnchor.UpperLeft);
            Ui.Size(_placeStatus, -1, 56);

            Line(_placePanel, shop ? "待摆放的家具（店里后仓、临时存放里的）：" : "待摆放的家具（后仓、临时存放里的）：", 17);
            if (shop)
            {
                string limit = S.CurrentShop.RetailKind == "restaurant"
                    ? $"餐桌 {GameSession.RestaurantTablesPlaced(S.CurrentShop)} / {GameSession.RestaurantTableLimit(S.PlayerShop.Size)}"
                    : $"货架 {S.PlacedFixtures(FixtureKind.Shelf)} / {S.ShopShelfLimit}";
                Line(_placePanel, limit, 17, Ui.Accent);
            }
            bool any = false;
            foreach (var kv in S.FurnitureToPlaceList(shop))
            {
                FurnitureDef def = FurnitureCatalog.Get(kv.Key);
                if (def == null) continue;
                any = true;
                string id = def.Id;
                var button = Ui.Size(Ui.Button(_placePanel, $"{def.Name} ×{kv.Value}", () => _ctrl.SelectStock(id), 17,
                    Ui.SelectedButton(_ctrl.PlaceId == id && !_ctrl.PlaceHoldsPlaced)), -1, 40);
                ItemButtonPortrait(button, "furn:" + id, 32);
            }
            if (!any) Ui.Size(Ui.Label(_placePanel, "空的。去家具城买，或者网购。", 15, TextAnchor.MiddleLeft, Ui.Dim), -1, 30);

            if (_ctrl.PlaceId != null)
            {
                Ui.Size(Ui.Button(_placePanel, "旋转（R）", _ctrl.RotateGhost, 17), -1, 40);
                if (_ctrl.PlaceHoldsPlaced) Ui.Size(Ui.Button(_placePanel, "收进后仓（X）", _ctrl.StoreSelected, 17, Ui.Warn), -1, 40);
            }
            var history = Ui.Row(_placePanel, 36);
            Ui.Size(Ui.Button(history, "撤销 Ctrl+Z", () => _ctrl.UndoPlacement(), 15, Ui.ButtonBg, S.CanUndoLayout), 165);
            Ui.Size(Ui.Button(history, "重做 Ctrl+Y", () => _ctrl.UndoPlacement(true), 15, Ui.ButtonBg, S.CanRedoLayout), 165);
            Ui.Size(Ui.Button(_placePanel, "完成", _ctrl.EndPlacement, 19, Ui.ButtonHot), -1, 46);
        }

        private void UpdatePlacementStatus()
        {
            UpdateHomePlacementStatus();
            if (_placePanel == null || !_placePanel.gameObject.activeSelf || _placeStatus == null) return;
            if (!_ctrl.Placing) return;
            if (_ctrl.PlaceId == null) _placeStatus.text = "选一件背包里的家具，或者点屋里的家具拿起来挪。";
            else if (_ctrl.PlaceError == null) _placeStatus.text = "<color=#386047>这里可以放，点左键。</color>";
            else _placeStatus.text = "<color=#9C4634>" + _ctrl.PlaceError + "</color>";
        }

        // ---------- 头顶的“可以交互”提示 ----------

        private RectTransform _interactBox;
        private RectTransform _interactArrow;
        private Text _interactText;

        private void UpdateInteractMarker()
        {
            if (_interactBox == null)
            {
                Image bg = Ui.Panel(_labelCanvas.transform, "InteractLabel", Ui.Chrome);
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
