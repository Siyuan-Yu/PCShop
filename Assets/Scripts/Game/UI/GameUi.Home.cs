using System;
using System.Collections;
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
        private void OpenRoomWindow(string title, float w, float h, System.Action<Window> build,
            bool allowDuringOpeningMeal = false, bool scrollContent = true)
        {
            OpenWindow(title, w, h, win =>
            {
                if (!_ctrl.InHome)
                {
                    win.Close();
                    return;
                }
                build(win);

            }, true, true, scrollContent: scrollContent, allowDuringOpeningMeal: allowDuringOpeningMeal);
        }

        public void OpenBedWindow()
        {
            if (!S.CanSleepAt(S.CurrentPlace)) return;
            bool showHelp = false;
            // 确认锁属于本次打开的窗口，重建内容也不能再次执行睡眠。
            bool sleepConfirmed = false;
            OpenWindow("睡觉", 620, 410, w =>
            {
                if (!S.CanSleepAt(S.CurrentPlace)) { w.Close(); return; }
                w.TimeStatusOverride = $"当前 {Ui.Hm(S.Clock.MinuteOfDay)}";
                RefreshWindowTimeStatus(w);
                BuildBed(w, showHelp, () => { showHelp = !showHelp; w.Rebuild(); }, sleep =>
                {
                    if (sleepConfirmed) return;
                    sleepConfirmed = true;
                    foreach (var button in w.Content.GetComponentsInChildren<Button>(true))
                        button.interactable = false;
                    void FinishBedWindow(bool slept)
                    {
                        if (w.Closed) return;
                        if (slept) w.Close();
                        else
                        {
                            sleepConfirmed = false;
                            w.Rebuild();
                        }
                    }
                    if (CanAnimateUi) _ctrl.StartCoroutine(PlaySleepTransition(sleep, FinishBedWindow));
                    else FinishBedWindow(sleep());
                });
            });
        }

        private Window _sleepTransition;

        internal void OnResidenceSleepFinished(long duration)
        {
            // Manual sleep already owns the fade and wake-up sequence.
            if (CanAnimateUi && _sleepTransition?.Closed != false)
                _ctrl.StartCoroutine(PlaySleepTransition(null, null, duration));
        }

        private IEnumerator PlaySleepTransition(Func<bool> sleep, Action<bool> finishBedWindow, long completedDuration = 0)
        {
            var session = S;
            var residence = session.CurrentPlace;
            HomeRoomView wakingRoom = null;
            var transition = OpenWindow("睡眠转场", 0, 0, _ => { }, closable: false,
                onClosed: () =>
                {
                    wakingRoom?.FinishWakeUp();
                    _sleepTransition = null;
                }, scrollContent: false,
                allowDuringOpeningMeal: true, showTitleBar: false, fullScreen: true);
            _sleepTransition = transition;
            // Keep the fade above any notifications raised while advancing to the wake time.
            var screen = transition.Root;
            screen.SetParent(_tipCanvas.transform, false);
            screen.Fill();
            var fade = screen.gameObject.AddComponent<CanvasGroup>();
            var wake = Ui.Label(screen, "", 34, TextAnchor.MiddleCenter, Ui.ChromeInk);
            wake.rectTransform.Anchor(.1f, .5f, .9f, .5f, 0, -54, 0, -54);

            var closeEyes = new UiReveal();
            closeEyes.Add(screen, 0, .18f, progress => fade.alpha = progress);
            yield return closeEyes.Play();
            if (transition.Closed || S != session) yield break;

            long duration = completedDuration;
            if (sleep != null)
            {
                long start = session.Clock.Now.TotalMinutes;
                bool slept = sleep();
                finishBedWindow(slept);
                if (transition.Closed || S != session) yield break;
                if (slept) duration = session.Clock.Now.TotalMinutes - start;
            }

            if (duration > 0 && session.CurrentPlace == residence && !session.IsGameOver)
            {
                wakingRoom = _ctrl.BeginResidenceWakeUp();
                wake.text = $"<b>醒来 · {Ui.Hm(session.Clock.MinuteOfDay)}</b>\n<size=22>睡了 {SleepDurationText(duration)}</size>";
                yield return new WaitForSecondsRealtime(.75f);
                if (transition.Closed || S != session) yield break;
            }

            var openEyes = new UiReveal();
            openEyes.Add(screen, 0, .28f, progress => fade.alpha = 1f - progress);
            yield return openEyes.Play();
            if (wakingRoom != null)
            {
                const float standUpSeconds = 1.85f;
                float start = Time.unscaledTime;
                while (!transition.Closed && S == session && session.CurrentPlace == residence && !session.IsGameOver)
                {
                    float progress = Mathf.Clamp01((Time.unscaledTime - start) / standUpSeconds);
                    wakingRoom.SetWakeUpProgress(progress);
                    if (progress >= 1f) break;
                    yield return null;
                }
            }
            transition.Close();
        }

        public void OpenStorageWindow()
        {
            if((S.CurrentPlace!=S.Home && !S.HasSecondaryResidenceWarehouse)||!S.HasWarehouseHere){Toast("这里还没摆纸箱，摆一个纸箱后才能使用后仓。");return;}
            OpenBackpackPanel();
        }

        private string _boxSel, _boxSide;

        private static readonly Stock NoParts = new Stock(0);

        private System.Collections.Generic.List<BagStack> CollectBox()
        {
            if(!S.InPlayerShop)return CollectStacks(S.WarehouseBagHere,S.WarehousePartsHere);
            var result=CollectStacks(S.ShopLocker,S.PlayerShop.Stock);
            foreach (var stack in result)
                stack.StoreInventory = stack.Key.StartsWith("part:");
            foreach(var line in S.PlayerShop.Stock.GoodsLines())
            {
                var product=Gifts.Get(line.Key);double kg=product.WeightKg*line.Value;
                var batches=S.PlayerShop.Stock.GoodsLots.Where(b=>b.Id==line.Key&&b.Count>0)
                    .Select(b=>new FoodBatch{Id=b.Id,Name=product.Name,Count=b.Count,ExpireDay=b.ExpireDay,Chilled=b.Chilled}).ToArray();
                result.Add(new BagStack{Key="stockgift:"+line.Key,Name=product.Name,Kind="本店商品库存（含已陈列商品）",Count=line.Value,Kg=kg,StoreInventory=true,
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
            return OpenWindow("背包与后仓", 1360, 880, win =>
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
        /// 同一套 B / F 界面：背包和房间内的共享后仓并排。
        /// 点一格选中，再放进去 / 拿出来。
        /// </summary>
        private void BuildStorage(Window w)
        {
            bool shop = S.InPlayerShop;
            var columns = Ui.Row(w.Content, 320, 12);
            var columnSize = columns.GetComponent<LayoutElement>();
            columnSize.flexibleHeight = 1;
            var backpack = StorageWell(columns, "BackpackWell");
            var storage = StorageWell(columns, "WarehouseWell");
            Line(backpack, $"背包 · {S.CarriedKg:0.##} / {S.CarryLimitKg:0.##} 千克", 19).name="WarehouseBagHeading";
            var bagStacks=CollectBag(includeQuestPages: true);
            var bagGrid=BagGrid(backpack,bagStacks,Mathf.CeilToInt((float)S.CurrentVehicle.CargoCapacity),_boxSide=="bag"?_boxSel:null,key=>
            {_boxSide="bag";_boxSel=key;w.Rebuild();},-1, columns: 7);
            bagGrid.parent.parent.name="WarehouseBackpackGrid";

            bool otherResidence = S.HasSecondaryResidenceWarehouse;
            string warehouse = shop ? "本店后仓与商品库存" : otherResidence ? S.PlaceTitle(S.CurrentPlace) + " · 后仓" : "家中后仓";
            Line(storage, warehouse + (shop ? "" : " · 容量不限"), 19).name="WarehouseStoreHeading";
            var boxStacks=CollectBox();
            var boxGrid=BagGrid(storage,boxStacks,7,_boxSide=="box"?_boxSel:null,key=>
            {_boxSide="box";_boxSel=key;w.Rebuild();},-1,true, columns: 7);
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
            public string Key, Name, Kind, Portrait;
            public int Count;
            public double Kg, Space;
            public Color Color;
            public FoodBatch[] Batches;
            public bool StoreInventory;
        }

        private string _bagSel;
        private const int BagColumns = 8;

        private System.Collections.Generic.List<BagStack> CollectBag(bool includeQuestPages = false)
        {
            var stacks = CollectStacks(S.Backpack, S.Cargo);
            if (!includeQuestPages) return stacks;

            foreach (var item in S.CarriedMarketFinds)
            {
                double kg = GameSession.UsedWeight(item);
                stacks.Add(new BagStack
                {
                    Key = "market-find:" + item.Id, Name = GameSession.UsedItemName(item),
                    Portrait = GameSession.UsedItemPortrait(item),
                    Kind = $"{GameSession.UsedConditionName(item)} · 参考价 ¥{GameSession.UsedValue(item):N0} · 可带到自家的二手店入库",
                    Count = 1, Kg = kg, Space = kg / Backpack.KgPerSpace, Color = Ui.Accent
                });
            }
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


        /// <summary>一格一格的背包格子：8 列，一叠东西占几格就画几格。</summary>
        private static RectTransform StorageWell(Transform parent, string name)
        {
            var panel = Ui.Size(Ui.FlatPanel(parent, name, Ui.FlatBand), 0, -1, 1);
            panel.raycastTarget = false;
            Ui.VList(panel.rectTransform, 5, 8);
            return panel.rectTransform;
        }

        private RectTransform BagGrid(Transform parent, System.Collections.Generic.List<BagStack> stacks, int capacity, string selKey,
            System.Action<string> onPick, float height, bool compact = false, int columns = BagColumns)
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
            total = (total + columns - 1) / columns * columns;

            RectTransform grid = Ui.Scroll(parent, height);
            if(height<0)
            {
                var area=Ui.Size(grid.parent.parent).GetComponent<LayoutElement>();
                area.minHeight=110;area.flexibleHeight=1;
            }
            for (int r = 0; r < total / columns; r++)
            {
                RectTransform row = Ui.Row(grid, 82, 6);
                for (int c = 0; c < columns; c++)
                {
                    int idx = r * columns + c;
                    if (idx < cells.Count)
                    {
                        BagStack st = cells[idx].st;
                        bool sel = st.Key == selKey;
                        Color bg = sel ? Ui.SelectedButton(true) : Ui.FlatWell;
                        int count = cells[idx].n;
                        int? expiry = cells[idx].expiry >= 0 ? cells[idx].expiry : null;
                        var cell = Ui.Size(Ui.Button(row, ShortName(st.Name), () => onPick(st.Key), 14, bg), 80, 80);
                        cell.name="BagItem_"+st.Key;
                        Ui.FlatBorder(cell.image, sel ? Ui.SelectionEdge : Ui.FlatWellEdge, sel ? 2 : 1);
                        var title = cell.GetComponentInChildren<Text>();
                        title.fontStyle = FontStyle.Bold;
                        float band = expiry.HasValue ? .2f : .02f;
                        title.rectTransform.Anchor(0, band, 1, band + .21f, 2, 0, 2, 0);
                        FillItemPortrait(cell.transform, st.Portrait ?? st.Key).Anchor(.18f, band + .21f, .82f, .98f);
                        string amount = st.Key == "rice" ? $"{st.Kg * count / st.Count:0.##}kg" : count > 0 ? $"×{count}" : "…";
                        var quantity = Ui.FlatPanel(cell.transform, "BagQuantity", Ui.PanelBg, Ui.FlatWellEdge);
                        quantity.raycastTarget = false;
                        float badgeWidth = Mathf.Clamp(7 * amount.Length + 8, 24, st.StoreInventory ? 46 : 72);
                        quantity.rectTransform.Anchor(1, 1, 1, 1, -badgeWidth - 2, -21, 2, 2);
                        var badge = Ui.Label(quantity.transform, amount, 14, TextAnchor.MiddleCenter, Ui.Accent);
                        badge.fontStyle = FontStyle.Bold;
                        badge.resizeTextForBestFit = true;
                        badge.resizeTextMinSize = 10;
                        badge.resizeTextMaxSize = 14;
                        badge.horizontalOverflow = HorizontalWrapMode.Overflow;
                        badge.rectTransform.Fill(1);
                        if (st.StoreInventory)
                        {
                            var inventory = Ui.FlatPanel(cell.transform, "BagStoreInventory", Ui.PanelBg, Ui.FlatWellEdge);
                            inventory.raycastTarget = false;
                            inventory.rectTransform.Anchor(0, 1, 0, 1, 2, -21, -30, 2);
                            Ui.Label(inventory.transform, "商品", 11, TextAnchor.MiddleCenter, Ui.Accent).rectTransform.Fill(1);
                        }
                        cell.Tip(st.StoreInventory ? $"{st.Name}\n本店商品库存（含货架陈列）" : st.Name);
                        AddFoodExpiry(cell, st.Key, expiry);
                    }
                    else
                    {
                        bool beyond = idx >= capacity;
                        Color fill = beyond ? Ui.PanelBg2 : Ui.FlatBand;
                        Image empty = Ui.FlatPanel(row, "Empty", fill, Ui.FlatWellEdge);
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
                if (S.PendingMarketFind != null)
                    Ui.Size(Ui.Button(w.Content, "领取淘到的旧物", () => { if (S.ClaimMarketFind()) w.Rebuild(); },
                        17, Ui.ButtonHot), -1, 39);

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
                    if (e.VenueSponsorId != null)
                        Ui.Size(Ui.Button(row, "查看活动", () => OpenVenueSponsorReport(e.VenueSponsorId), 16), 94, 40);
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
            if (S.OpeningMealTutorialPending && S.OpeningMealStep == 0)
                _cookKg = OpeningMealRiceKg;
            _cookFishPickerOpen = false;
            OpenRoomWindow("电饭锅", CookerWindowWidth, 560, BuildCooker, allowDuringOpeningMeal: true, scrollContent: false);
        }

        private int _wakeMinute = 8 * 60;
        private void BuildBed(Window w, bool showHelp, Action toggleHelp, Action<Func<bool>> confirmSleep)
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
            SleepPreset(presets, "睡 8 小时", WakeLabel(8 * 60), () => confirmSleep(() => S.SleepEightHours()),
                Ui.ButtonHot, S.IsSleepTime);
            SleepPreset(presets, "小睡 2 小时", WakeLabel(2 * 60), () => confirmSleep(() => S.Nap()),
                Ui.ButtonBg, true);

            var rule = Ui.Panel(w.Content, "SleepSectionRule", Ui.Edge);
            rule.raycastTarget = false;
            Ui.Size(rule, -1, 1);
            Line(w.Content, "自选起床时间", 20);
            _wakeMinute = QuarterHourClock(_wakeMinute);
            int duration = (_wakeMinute - now + dayMinutes) % dayMinutes;
            var picker = ClockSelector(w.Content, "起床", _wakeMinute,
                value => { _wakeMinute = value; w.RequestRebuild(); }, showLabel: false);
            Ui.Size(Ui.Button(picker, "睡到这个时间", () => confirmSleep(() => S.SleepUntil(_wakeMinute)),
                19, Ui.ButtonHot, S.IsSleepTime && duration > 0), 150, 42);
            string preview = duration > 0 ? $"{WakeLabel(duration)} · 睡 {SleepDurationText(duration)}"
                : "请选择与当前不同的起床时间";
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

        private static string SleepDurationText(long duration)
        {
            string hours = duration >= 60 ? $"{duration / 60} 小时" : "";
            string minutes = duration % 60 > 0 ? $"{duration % 60} 分钟" : "";
            return (hours + " " + minutes).Trim();
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

        private double _cookKg = 0.5;
        private string _cookIngredient;
        private string _cookFish;
        private bool _cookFishPickerOpen;
        private const float CookerWindowWidth = 760;
        private const float CookFishPickerSideWidth = 362;
        private const float CookFishPickerGap = 14;
        private static readonly Color CookFishFill = new Color32(190, 216, 202, 255);
        private static readonly Color CookFishInk = new Color32(39, 76, 62, 255);

        private static bool CookFishPickerFitsBeside(Window w) =>
            w.Root.rect.width >= CookerWindowWidth + 2 * (CookFishPickerSideWidth + CookFishPickerGap + 16);

        private void BuildCooker(Window w)
        {
            S.RefreshCooking();
            Pantry pantry = S.Pantry;
            CookJob job = pantry.Cooking;
            bool canConfigure = !pantry.PotBusy;
            bool holdClock = job == null;
            if (w.Holds != holdClock)
            {
                if (holdClock) S.Time.Hold();
                else if (S.Time.IsHeld) S.Time.Release();
                w.Holds = holdClock;
            }

            double rice = S.HomeCookingRiceKg;
            var fishOptions = canConfigure ? FishingCatalog.Fish
                .Select(fish => (fish, count: S.HomeCookingIngredientCount(fish.Id)))
                .Where(option => option.count > 0).ToArray() : Array.Empty<(FishDefinition fish, int count)>();
            if (canConfigure)
            {
                _cookKg = rice < Pantry.MinCookRiceKg ? rice : Math.Clamp(_cookKg, Pantry.MinCookRiceKg, rice);
                _cookIngredient = Ingredients.GetRiceCookerRecipe(_cookIngredient)?.Id;
                if (!fishOptions.Any(option => option.fish.Id == _cookFish)) _cookFish = null;
            }
            bool showFishPicker = canConfigure && _cookFishPickerOpen && fishOptions.Length > 0;
            bool sideBySide = showFishPicker && CookFishPickerFitsBeside(w);
            bool controlsEnabled = canConfigure && (!showFishPicker || sideBySide);

            int cookedBowls = pantry.CookedBowls;
            FoodBatch batch = job?.Batch ?? pantry.Cooked.FirstOrDefault(food => food.Count > 0);
            string mealId = canConfigure ? Pantry.MealId(_cookIngredient, _cookFish) : batch?.Id ?? "rice";
            int fishSeparator = mealId.IndexOf('+');
            var ingredient = Ingredients.GetRiceCookerRecipe(fishSeparator < 0 ? mealId : mealId.Substring(0, fishSeparator));
            var addedFish = fishSeparator < 0 ? null : FishingCatalog.Get(mealId.Substring(fishSeparator + 1));
            double chosenKg = job != null ? (double)job.Batch.Count / Pantry.BowlsPerKg : _cookKg;
            int need = Pantry.IngredientUnitsForRice(chosenKg);
            int available = canConfigure && ingredient != null ? S.HomeCookingIngredientCount(ingredient.Id) : 0;
            bool enoughIngredient = ingredient == null || available >= need;
            bool enoughRice = rice >= Pantry.MinCookRiceKg;

            string Stats() => $"饱腹 {S.Player.Hunger:0}  健康 {S.Player.Health:0}   可用大米：{S.HomeCookingRiceKg:0.##} 千克   背包里的饭盒：{S.Backpack.Lunchboxes} 盒";
            var stats = Line(w.Content, Stats(), 17);
            Line(w.Content, "米量", 17);
            var riceRow = Ui.Row(w.Content, 44);
            riceRow.name = "OpeningMealRice";
            foreach (double amount in new[] { 0.25, 0.5, 1, 1.5, 2.5 })
            {
                bool selected = cookedBowls == 0 && Math.Abs(chosenKg - amount) < 1e-6;
                Ui.Size(Ui.Button(riceRow, $"{amount} 千克", () =>
                {
                    _cookKg = amount;
                    if (S.OpeningMealTutorialPending) _openingMealCookStage = 1;
                    w.Rebuild();
                }, 17, Ui.SelectedButton(selected), controlsEnabled && rice >= amount), 90);
            }

            Line(w.Content, "做法", 17);
            var recipeRow = Ui.Row(w.Content, 44);
            recipeRow.name = "OpeningMealRecipe";
            void RecipeOption(IngredientDef recipe)
            {
                string id = recipe?.Id;
                bool enough = recipe == null || canConfigure && S.HomeCookingIngredientCount(id) >= need;
                Ui.Size(Ui.Button(recipeRow, recipe?.DishName ?? "白饭", () =>
                {
                    _cookIngredient = id;
                    if (S.OpeningMealTutorialPending) _openingMealCookStage = 2;
                    w.Rebuild();
                }, 17, Ui.SelectedButton(ingredient?.Id == id), controlsEnabled && enough), 0, 44, 1);
            }

            RecipeOption(null);
            foreach (IngredientDef recipe in Ingredients.RiceCookerRecipes) RecipeOption(recipe);

            var fishRow = Ui.Row(w.Content, 40, 8);
            if (addedFish != null) ItemPortrait(fishRow, "ing:" + addedFish.Id, 34);
            string fishLabel = addedFish != null ? $"本锅用 {addedFish.Name} 1 条"
                : canConfigure && fishOptions.Length == 0 ? "暂无鱼可加" : "未加鱼";
            Ui.Size(Ui.Label(fishRow, fishLabel, 16, color: addedFish == null ? Ui.Dim : CookFishInk), 0, 40, 1);
            bool canAddFish = controlsEnabled && fishOptions.Length > 0;
            var fishButton = Ui.Size(Ui.Button(fishRow, showFishPicker ? "收起选鱼" : "加鱼", () =>
            {
                _cookFishPickerOpen = !showFishPicker;
                w.Rebuild();
            }, 16, canAddFish ? CookFishFill : Ui.ButtonBg, canAddFish), 104, 36);
            fishButton.name = "CookFishPickerToggle";
            if (canAddFish)
            {
                Ui.UseCaptionInk(fishButton, CookFishInk);
                fishButton.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
                Ui.FlatBorder(fishButton.image, Color.Lerp(CookFishFill, CookFishInk, showFishPicker ? .75f : .48f));
            }

            int bowls = cookedBowls > 0 ? cookedBowls : batch?.Count ?? Mathf.RoundToInt((float)(chosenKg * Pantry.BowlsPerKg));
            var preview = Ui.Size(Ui.Panel(w.Content, "CookerMealPreview", Ui.PanelBg2), -1, 124);
            var details = Ui.Rect(preview.transform, "CookerMealDetails").Anchor(0, 0, 1, 1, 10, 6, 166, 6);
            Ui.VList(details, 3, 0).childAlignment = TextAnchor.MiddleLeft;
            string name = batch?.Name ?? GameSession.MealName(mealId);
            Line(details, $"{name} · {(cookedBowls > 0 ? "剩 " : "约 ")}{bowls} 碗", 19, Ui.Accent).fontStyle = FontStyle.Bold;
            double hunger = batch?.Hunger ?? Pantry.BaseBowlHunger + (ingredient?.HungerBonus ?? 0) + (addedFish?.MealHungerBonus ?? 0);
            double health = batch?.Health ?? (ingredient?.HealthBonus ?? 0) + (addedFish?.MealHealthBonus ?? 0);
            Ui.Label(details, $"每碗 · 饱腹 +{hunger:0.#}" + (health > 0 ? $" · 健康 +{health:0.##}" : "")
                + " · " + GameSession.MealEffects(mealId), 16);
            if (cookedBowls == 0 && GameSession.FishMealWorkEffect(addedFish?.Id) is string workEffect)
                Ui.Label(details, $"首次吃这锅 · {workEffect}", 16, color: Ui.Good);
            if (cookedBowls > 0)
                Ui.Label(details, $"保质期 · {FoodExpiryBadge.Countdown(batch.ExpireDay, S.Clock.Now.TotalMinutes)}", 15, color: Ui.Dim);
            else if (canConfigure && !enoughRice)
                Ui.Label(details, "可用大米不足 0.25 千克。", 16, color: Ui.Warn);
            else if (canConfigure && ingredient != null)
                Ui.Label(details, $"本锅用 {ingredient.Name} {need * ingredient.WeightKg:0.##} 千克 · 库存 {available * ingredient.WeightKg:0.##} 千克",
                    15, color: enoughIngredient ? Ui.Dim : Ui.Warn);

            var display = BuildCookerDisplay(preview.transform);
            var actions = Ui.Row(w.Content, 48, 6);
            if (job != null)
            {
                int readyMinuteOfDay = (int)(job.ReadyMinute % GameTimestamp.MinutesPerDay);
                Ui.Size(Ui.Button(actions, $"预计 {Ui.Hm(readyMinuteOfDay)} 煮好", null, 18,
                    Ui.PanelBg2, interactable: false), 0, 48, 1);
                long shownMinute = -1;
                w.RefreshContent = () =>
                {
                    if (pantry.Cooking != job)
                    {
                        w.RequestRebuild();
                        return;
                    }
                    long remaining = pantry.CookMinutesLeft(S.Clock.Now.TotalMinutes);
                    if (remaining == shownMinute) return;
                    shownMinute = remaining;
                    stats.text = Stats();
                    display.text = $"<size=26><b>{remaining}</b></size> 分钟";
                };
                w.RefreshContent();
            }
            else if (cookedBowls > 0)
            {
                display.text = "饭好了";
                BuildCookerTakeActions(w, actions, pantry);
            }
            else
            {
                display.text = "待机";
                var start = Ui.Button(actions, $"开始焖饭 · {Pantry.CookMinutes} 分钟", () =>
                {
                    if (S.CookRice(_cookKg, _cookIngredient, _cookFish))
                    {
                        _cookFish = null;
                        _cookFishPickerOpen = false;
                    }
                    w.Rebuild();
                }, 19, Ui.ButtonHot, controlsEnabled && enoughRice && enoughIngredient);
                Ui.Size(start, 0, 48, 1).name = "OpeningMealStart";
            }

            if (showFishPicker)
            {
                BuildCookFishPicker(w, fishOptions, addedFish, sideBySide);
                w.RefreshContent = () =>
                {
                    if (pantry.PotBusy || sideBySide != CookFishPickerFitsBeside(w)) w.RequestRebuild();
                };
            }
            BindOpeningMealCooker(w);
        }

        private static Text BuildCookerDisplay(Transform preview)
        {
            var cooker = Ui.Rect(preview, "CookerIllustration").Anchor(1, .5f, 1, .5f, -152, -51, 12, -51);
            Part("Handle", Ui.ChromeEdge, 39, 79, 62, 23, .55f);
            Part("HandleOpening", Ui.PanelBg2, 44, 77, 52, 20, .55f);
            Part("Body", new Color32(228, 213, 179, 255), 0, 0, 140, 76, .56f);
            Part("Lid", new Color32(233, 221, 191, 255), 0, 71, 140, 16, .7f);
            Part("LidSeam", new Color32(151, 136, 107, 255), 2, 70, 136, 2);
            Part("Switch", new Color32(136, 128, 106, 255), 57, 8, 26, 6);
            var screen = Part("Display", Ui.Chrome, 11, 20, 118, 40);
            var label = Ui.Label(screen, "", 14, TextAnchor.MiddleCenter, Ui.ChromeGold);
            label.name = "CookerCountdown";
            label.rectTransform.Fill();
            return label;

            RectTransform Part(string name, Color color, float x, float y, float width, float height, float cornerScale = 1)
            {
                var image = Ui.Panel(cooker, name, color);
                Ui.Round(image);
                image.pixelsPerUnitMultiplier = cornerScale;
                image.raycastTarget = false;
                return image.rectTransform.Anchor(0, 0, 0, 0, x, y, -(x + width), -(y + height));
            }
        }

        private void BuildCookerTakeActions(Window w, Transform actions, Pantry pantry)
        {
            actions.name = "OpeningMealTake";
            Ui.Size(Ui.Button(actions, "现在吃一碗", () =>
            {
                S.EatBowlAtHome();
                w.Rebuild();
            }, 18, Ui.ButtonHot), 0, 48, 1).name = "OpeningMealEat";

            Ui.Size(Ui.Button(actions, "装 1 盒进背包", () =>
            {
                S.PackLunch(1);
                w.Rebuild();
            }, 17), 0, 48, 1);

            Ui.Size(Ui.Button(actions, "全部装进背包", () =>
            {
                bool tutorial = S.OpeningMealTutorialPending;
                if (!S.PackLunch(pantry.CookedBowls)) return;
                if (tutorial && !S.OpeningMealTutorialPending)
                {
                    w.Close();
                    Popup("饭盒带好了", $"背包里的饭盒会在饭点或饿了时{Ui.Emphasize("自动吃")}。\n放进{Ui.Emphasize("冰箱")}可{Ui.Emphasize("暂停保质期倒计时")}。");
                }
                else w.Rebuild();
            }, 17), 0, 48, 1).name = "OpeningMealPackAll";

            if (S.CanOpenFridge && !S.OpeningMealTutorialPending)
                Ui.Size(Ui.Button(actions, "存入冰箱", () =>
                {
                    S.ChillCookedFood(pantry.CookedBowls);
                    w.Rebuild();
                }, 17), 0, 48, 1);
        }

        private void BuildCookFishPicker(Window w, (FishDefinition fish, int count)[] options,
            FishDefinition selected, bool sideBySide)
        {
            const int padding = 8, gap = 6, cellSize = 80;
            int columns = sideBySide ? 4 : 8;
            int slots = Mathf.Max(16, options.Length);
            int rows = (slots + columns - 1) / columns;
            float height = padding * 2 + (rows + 2) * gap + rows * cellSize + 32 + 27 + 36;

            RectTransform parent = w.Content;
            if (!sideBySide)
            {
                var scrim = Ui.Panel(parent, "FishPickerBackdrop", Ui.WorldShade);
                scrim.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                parent = scrim.rectTransform.Fill();
            }
            var panel = Ui.FlatPanel(parent, "CookFishInventory", Ui.PanelBg, Ui.FlatWellEdge).rectTransform;
            panel.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            if (sideBySide)
                panel.Anchor(1, 1, 1, 1, CookFishPickerGap, 38 - height, -(CookFishPickerGap + CookFishPickerSideWidth), -38);
            else
                panel.Anchor(0, .5f, 1, .5f, 22, -height * .5f, 22, -height * .5f);

            Ui.VList(panel, gap, padding);
            var header = Ui.Row(panel, 32);
            Ui.Size(Ui.Label(header, "选鱼", 19), 0, 32, 1);
            Ui.Size(Ui.Button(header, "关闭", () => { _cookFishPickerOpen = false; w.Rebuild(); }, 15), 64, 32);
            Line(panel, "背包和家中后仓 · 每锅用 1 条", 15, Ui.Dim);

            for (int first = 0; first < slots; first += columns)
            {
                var row = Ui.Row(panel, cellSize, gap);
                for (int i = first; i < first + columns; i++)
                {
                    if (i >= options.Length)
                    {
                        var empty = Ui.Size(Ui.FlatPanel(row, "Empty", Ui.FlatBand, Ui.FlatWellEdge), cellSize, cellSize);
                        empty.raycastTarget = false;
                        continue;
                    }
                    var (fish, count) = options[i];
                    bool active = selected == fish;
                    var slot = Ui.Size(Ui.Button(row, fish.Name, () => { _cookFish = fish.Id; w.Rebuild(); }, 14,
                        Ui.SelectedButton(active)), cellSize, cellSize);
                    slot.name = "CookFishSlot_" + fish.Id;
                    Ui.FlatBorder(slot.image, active ? Ui.Accent : Ui.FlatWellEdge, active ? 2 : 1);
                    var label = slot.GetComponentInChildren<Text>();
                    label.rectTransform.Anchor(0, 0, 1, .25f, 2, 2, 2, 0);
                    FillItemPortrait(slot.transform, "ing:" + fish.Id).Anchor(.17f, .25f, .83f, .98f);
                    var badge = Ui.FlatPanel(slot.transform, "BagQuantity", Ui.PanelBg, Ui.FlatWellEdge);
                    badge.raycastTarget = false;
                    badge.rectTransform.Anchor(1, 1, 1, 1, -42, -21, 2, 2);
                    Ui.Label(badge.transform, $"×{count}", 13, TextAnchor.MiddleCenter, Ui.Accent).rectTransform.Fill(1);
                    slot.Tip(GameSession.FishMealWorkEffect(fish.Id) is string bonus ? $"{fish.Name} · 首次吃这锅 {bonus}" : fish.Name);
                }
            }

            var footer = Ui.Row(panel, 36);
            string picked = selected == null ? "未选鱼" : selected.Name + (FishingCatalog.RequiresCatch(selected.Id) ? " · 钓获" : "");
            Ui.Size(Ui.Label(footer, picked, 16, color: Ui.Dim), 0, 36, 1);
            Ui.Size(Ui.Button(footer, "不加鱼", () => { _cookFish = null; w.Rebuild(); }, 15,
                interactable: selected != null), 84, 36);
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
                    if (!S.CanDecorateCurrentResidence) return "到收货的住处安装";
                    if (S.FurnitureToPlace(false, GameSession.HomeWorkbenchId) == 0)
                        return "工作台不在当前住处，请到收货地址，或先把它存入这里的后仓。";
                    return S.FurnitureEditBlocker(false) ?? "点击开始摆放居家办公台";
                }
                return !S.CanDecorateCurrentResidence ? "回家后可用" : S.IsWorking ? "下班后可用" : "结束当前活动或行程后可用";
            }
        }
        private enum MyPage { Profile, Food, Residence, Contacts, Relations, Assets }
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
            _myWindow = OpenWindow("我的", 1280, 960, BuildMyPanel, scrollContent: false, onClosed: () =>
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
            string[] names = { "个人资料", "饮食", "住处", "我的资产", "通讯录", "人际关系图" };
            var pages = new[] { MyPage.Profile, MyPage.Food, MyPage.Residence, MyPage.Assets, MyPage.Contacts, MyPage.Relations };
            for (int i = 0; i < names.Length; i++)
            {
                MyPage tab = pages[i];
                var button = Ui.Size(Ui.Button(nav, names[i], () =>
                {
                    if (_myPage != tab) OpenMyPanel(tab);
                }, 18, Ui.SelectedButton(_myPage == tab)), 0, 42, 1);
                button.name = "MyTab_" + (int)tab;
            }

            if (_myPage == MyPage.Contacts)
            {
                BuildContactsContent(w, closeOnDriverCall: false);
                return;
            }
            if (_myPage == MyPage.Relations)
            {
                BuildRelationsContent(w, 770, 535, showPlayerPortrait: true);
                return;
            }

            if (_myPage == MyPage.Assets)
            {
                var assets = PhoneFlexibleScroll(w.Content, "MyAssetsContent");
                var parent = w.Content;
                w.Content = assets;
                try { BuildMyAssets(w); }
                finally { w.Content = parent; }
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
            bool canOpenWorkbench = S.CanUseHomeWorkbench || CanStartWorkbenchSetup;
            string workbenchLabel = CanStartWorkbenchSetup ? "安装居家工作台" : "居家工作台" + (blocker == null ? "" : " · " + blocker);
            Ui.Size(Ui.Button(body, workbenchLabel, OpenHomeWorkbench, 17,
                canOpenWorkbench ? Ui.ButtonHot : Ui.ButtonBg, canOpenWorkbench), -1, 38).name = "MyHomeWorkbench";
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
            Line(residence, "主住处", 15, Ui.Dim);
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
                    Line(body, $"签约周租 {S.CurrentHomeRent} · 每周日扣款", 17);
                    if (S.NextResidentialRentDay(S.Home) is int day)
                        Line(body, "下次扣租 · " + new GameDate(day) + " 周日", 16, Ui.Dim);
                }
            }

            var otherLeases = S.RentedResidences.Where(place => place != S.Home).ToArray();
            if (otherLeases.Length > 0)
            {
                Line(body, "其他租约", 19, Ui.Accent);
                foreach (var place in otherLeases)
                {
                    string due = S.NextResidentialRentDay(place) is int day ? $" · 下次扣租 {new GameDate(day)} 周日" : "";
                    ResidenceRow(place, $"{place.Name} · 周租 {S.SignedResidentialRent(place)}{due}");
                }
            }

            Line(body, "自有住宅", 19, Ui.Accent);
            var owned = S.ResidentialPlaces.Where(place => place != S.Home)
                .Select(place => (place, property: S.PropertyAt(place.Id))).Where(entry => entry.property != null).ToArray();
            if (!rented) Line(body, S.HomeTitle + " · 自住", 17);
            foreach (var (place, property) in owned)
                if (property.UseMode == "vacant")
                    ResidenceRow(place, place.Name + " · 自有空置");
                else
                    Line(body, $"{place.Name} · 出租 · 周租 {Money.FromCents(property.WeeklyRentCents)}", 17);
            if (rented && owned.Length == 0) Line(body, "暂无", 16, Ui.Dim);
            if (S.HomeVisits.Any(visit => visit.Working))
                Ui.Size(Ui.Button(body, "在家等维修完成", () => { S.WaitForRepair(); w.Rebuild(); }, 17, Ui.ButtonHot, _ctrl.InHome), -1, 38);

            void ResidenceRow(Place place, string caption)
            {
                var row = Ui.Row(body, 40, 8);
                Ui.Size(Ui.Label(row, caption, 17), 0, 40, 1);
                string blocker = _ctrl.Placing ? "完成装修后可以切换主住处" : S.PrimaryResidenceBlocker(place);
                var button = Ui.Size(Ui.Button(row, "设为主住处", () => ConfirmPrimaryResidence(w, place), 16, Ui.ButtonBg,
                    blocker == null), 148, 36);
                button.name = "SetPrimaryResidence_" + place.Id;
                if (blocker != null) button.Tip(blocker);
            }
        }

        private void ConfirmPrimaryResidence(Window owner, Place place)
        {
            OpenWindow("设为主住处", 620, 340, w =>
            {
                string retained = S.HasResidentialLease(S.Home) ? "保留原租约，继续按原租金扣款。" : "保留产权，仍可入住。";
                Para(w.Content, $"把{place.Name}设为主住处？{S.HomeTitle}{retained}", 19);
                Para(w.Content, "各套住宅的家具和后仓留在原地址。底栏的回家目的地会随主住处更新。", 17, Ui.Dim);
                var row = Ui.Row(w.Content, 44, 8);
                Ui.Size(Ui.Button(row, "取消", w.Close, 17), 0, 42, 1);
                Ui.Size(Ui.Button(row, "确认切换", () =>
                {
                    if (!S.SetPrimaryResidence(place)) return;
                    _myLeaseExpanded = false;
                    RefreshShortcutMenus();
                    RefreshHomeDockVisibility(force: true);
                    w.Close();
                    owner.Rebuild();
                }, 17, Ui.ButtonHot), 0, 42, 1);
            }, fitContent: true);
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
                bool guided = S.OpeningRenovationStep == OpeningRenovationLesson.StartPlacement;
                Ui.Size(Ui.Label(w.Content, "店里要改造，得歇业装修一天：\n· 现在起店今天不再营业，已经在店里排队的客人也不再接待；\n· 装修期间可以随便摆放、挪动、收起店里的摆设；\n· 明天早上恢复营业。", 19, TextAnchor.UpperLeft), -1, 200);
                RectTransform row = Ui.Row(w.Content, 52);
                var confirm = Ui.Size(Ui.Button(row, "确认歇业，开始装修", () =>
                {
                    if (!S.BeginRenovation()) return;
                    w.Close();
                    Begin();
                }, 19, Ui.Warn), 300);
                if (guided)
                {
                    _openingRenovationConfirmWindow = w;
                    _openingRenovationConfirmButton = confirm;
                }
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
            Ui.Size(Ui.Label(row, S.CityGoodsRetailPrice(def).ToString(), 19, TextAnchor.MiddleLeft, Ui.Good), 90);
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
        private Button _homeDockToggle, _homePlacementConfirm, _homePlacementFinish;
        private RectTransform _homeDock, _homeDockContent;
        private Text _homeDockToggleLabel, _homeDockTitle, _homeDockTenure, _homePlacementStatus;
        private bool _homeDockCollapsed;
        private GameSession _homeDockSession;
        private string _homeDockResidence;
        private KeyValuePair<string, int>[] _homeDockStored = Array.Empty<KeyValuePair<string, int>>();
        private bool HomeDockOpen => _homeDock != null && _homeDock.gameObject.activeSelf;
        private bool EditingResidence => _ctrl.Placing && !_ctrl.PlacingShop;
        private bool HomeDockAvailable => S.CanDecorateCurrentResidence && !MainMenuOpen && !S.IsGameOver
            && !S.IsWorking && !OpeningMealRestricted && !ExpoHudOpen && !_ctrl.PlacingShop;

        private void BuildHomeDockToggle(Transform parent)
        {
            _homeDockToggle = Ui.Button(parent, "收起住处面板 ▴", () =>
            {
                _homeDockCollapsed = HomeDockOpen;
                RefreshHomeDockVisibility();
            }, 17);
            _homeDockToggle.name = "HomeDockToggle";
            _homeDockToggleLabel = _homeDockToggle.GetComponentInChildren<Text>();
            ((RectTransform)_homeDockToggle.transform).Anchor(1, 1, 1, 1, -200, -112, 12, 74);
            _homeDockToggle.gameObject.SetActive(false);
        }

        private void RefreshHomeDockVisibility(bool force = false)
        {
            string residence = S.CanDecorateCurrentResidence ? S.CurrentPlace.Id : null;
            if (_homeDockSession != S || _homeDockResidence != residence)
            {
                _homeDockSession = S;
                _homeDockResidence = residence;
                _homeDockCollapsed = false;
                force = true;
            }
            bool visible = HomeDockAvailable && !_ctrl.Placing;
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
            if (!HomeDockOpen || !S.CanDecorateCurrentResidence) return;
            var stored = S.FurnitureToPlaceList(false).Where(entry => entry.Value > 0 && FurnitureCatalog.Get(entry.Key) != null).ToArray();
            string title = _ctrl.InHome ? S.HomeTitle : S.CurrentPlace.Name;
            string tenure = S.HasResidentialLease(S.CurrentPlace) ? "租住中" : "自有";
            if (!force && _homeDockStored.SequenceEqual(stored)
                && _homeDockTitle.text == title && _homeDockTenure.text == tenure) return;
            _homeDockStored = stored;
            _homeDockTitle.text = title;
            _homeDockTenure.text = tenure;
            var scroll = _homeDockContent.parent.parent.GetComponent<ScrollRect>();
            float previousScroll = scroll.verticalNormalizedPosition;
            Ui.Clear(_homeDockContent);
            Ui.Size(Ui.Button(_homeDockContent, "开始装修", () => StartPlacement(false), 17, Ui.ButtonHot), -1, 38)
                .name = "HomeDockBeginPlacement";
            BuildHomeFurnitureList(_homeDockContent, stored, editing: false);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = previousScroll;
        }

        private RectTransform _homePlacementPanel, _homePlacementContent;
        private Text _homePlacementTitle;

        private void BuildHomePlacementPanel()
        {
            _homePlacementPanel = Ui.Panel(_dockCanvas.transform, "HomePlacementPanel", Ui.PanelBg).rectTransform;
            var header = Ui.Panel(_homePlacementPanel, "HomePlacementHeader", Ui.PanelBg2).rectTransform
                .Anchor(0, 1, 1, 1, 0, -76, 0, 0);
            Ui.Label(header, "<b>住宅装修</b>", 24).rectTransform.Anchor(0, 1, 1, 1, 13, -40, 13, 7);
            _homePlacementTitle = Ui.Label(header, "", 17, color: Ui.Dim);
            _homePlacementTitle.rectTransform.Anchor(0, 1, 1, 1, 13, -67, 13, 37);
            _homePlacementContent = Ui.Scroll(_homePlacementPanel);
            ((RectTransform)_homePlacementContent.parent.parent).Anchor(0, 0, 1, 1, 5, 57, 5, 78);
            _homePlacementFinish = Ui.Button(_homePlacementPanel, "完成装修", _ctrl.FinishHomePlacement, 17, Ui.ButtonHot);
            _homePlacementFinish.name = "HomeDockFinishPlacement";
            ((RectTransform)_homePlacementFinish.transform).Anchor(0, 0, 1, 0, 10, 8, 10, -48);
        }

        private void RefreshHomePlacementPanel()
        {
            _homePlacementTitle.text = S.CurrentPlace.Name;
            var scroll = _homePlacementContent.parent.parent.GetComponent<ScrollRect>();
            float previousScroll = scroll.verticalNormalizedPosition;
            Ui.Clear(_homePlacementContent);
            _homePlacementStatus = null;
            _homePlacementConfirm = null;
            if (_ctrl.PlaceId != null)
            {
                Line(_homePlacementContent, (_ctrl.PlaceHoldsPlaced ? "移动 · " : "摆放 · ") + FurnitureLabel(_ctrl.PlaceId, 1), 18, Ui.Accent);
                _homePlacementStatus = Line(_homePlacementContent, "", 16, Ui.Dim);
                Ui.Size(_homePlacementStatus, -1, 40);
                var actions = Ui.Row(_homePlacementContent, 36, 4);
                Ui.Size(Ui.Button(actions, "旋转（R）", _ctrl.RotateGhost, 15), 86, 36);
                _homePlacementConfirm = Ui.Size(Ui.Button(actions, _ctrl.PlaceHoldsPlaced ? "确认移动" : "确认摆放",
                    _ctrl.ConfirmHomeFurniturePlacement, 16, Ui.ButtonBg), 0, 36, 1);
                _homePlacementConfirm.name = "HomeDockConfirmPlacement";
                Ui.Size(Ui.Button(actions, "取消", _ctrl.CancelPlacementSelection, 15), 60, 36).name = "HomeDockCancelPlacement";
            }
            else
            {
                Para(_homePlacementContent, "点击房间里的家具可移动，选中后按 X 收纳。", 15, Ui.Dim);
            }

            var history = Ui.Row(_homePlacementContent, 36, 5);
            Ui.Size(Ui.Button(history, "撤销 Ctrl+Z", () => _ctrl.UndoPlacement(), 14, Ui.ButtonBg, S.CanUndoLayout), 0, 36, 1);
            Ui.Size(Ui.Button(history, "重做 Ctrl+Y", () => _ctrl.UndoPlacement(true), 14, Ui.ButtonBg, S.CanRedoLayout), 0, 36, 1);
            BuildHomeFurnitureList(_homePlacementContent, S.FurnitureToPlaceList(false)
                .Where(entry => entry.Value > 0 && FurnitureCatalog.Get(entry.Key) != null).ToArray(), editing: true);
            _homePlacementFinish.GetComponentInChildren<Text>().text = _ctrl.PlaceId == null ? "完成装修"
                : _ctrl.PlaceHoldsPlaced ? "移动并完成" : "摆放并完成";
            UpdateHomePlacementStatus();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = _ctrl.PlaceId != null ? 1 : previousScroll;
        }

        private void BuildHomeFurnitureList(RectTransform parent, KeyValuePair<string, int>[] stored, bool editing)
        {
            Line(parent, $"可摆放家具  ·  {stored.Sum(entry => entry.Value)} 件", 17, Ui.Dim);
            foreach (var entry in stored)
            {
                string id = entry.Key;
                var row = HomeDockFurnitureRow(parent, id, FurnitureLabel(id, entry.Value), editing && _ctrl.PlaceId == id && !_ctrl.PlaceHoldsPlaced);
                Ui.Size(Ui.Button(row, "摆放", () =>
                {
                    if (editing) _ctrl.SelectStock(id);
                    else StartPlacement(false, id);
                }, 16, Ui.ButtonBg), 74, 34).name = "HomeDockPlace_" + id;
            }
            if (stored.Length == 0) Line(parent, "暂无可摆放家具", 15, Ui.Dim);
        }

        private RectTransform HomeDockFurnitureRow(RectTransform parent, string id, string label, bool selected)
        {
            var row = Ui.Row(parent, 40, 5);
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
            if (!EditingResidence || _homePlacementFinish == null) return;
            bool pending = _ctrl.PlaceId != null;
            bool picked = _ctrl.HasHomePlacementTarget;
            string issue = pending ? S.FurnitureEditBlocker(false) ?? _ctrl.PlaceError : null;
            bool canConfirm = picked && issue == null;
            _homePlacementFinish.interactable = !pending || canConfirm;
            if (_homePlacementStatus == null || _homePlacementConfirm == null) return;
            _homePlacementStatus.text = issue ?? (picked ? "可以放在这里" : "点击房间选择位置");
            _homePlacementStatus.color = issue != null ? Ui.Warn : picked ? Ui.Good : Ui.Dim;
            _homePlacementConfirm.interactable = canConfirm;
        }

        private RectTransform _placePanel, _shopPlacementContent;
        private Text _placeStatus;

        public void ShowPlacementPanel()
        {
            RefreshHomeDockVisibility();
            RefreshShopDockVisibility();
            if (EditingResidence)
            {
                if (_homePlacementPanel == null) BuildHomePlacementPanel();
                _homePlacementPanel.Anchor(1, 0, 1, 1, -HomeDockWidth, 70, 10, 80 + HudFunctionOffset);
                _homePlacementPanel.gameObject.SetActive(true);
                RefreshHomePlacementPanel();
                return;
            }
            if (_placePanel == null)
            {
                _placePanel = Ui.Panel(_dockCanvas.transform, "PlacePanel", Ui.PanelBg).rectTransform;
                _shopPlacementContent = Ui.Scroll(_placePanel);
                ((RectTransform)_shopPlacementContent.parent.parent).Anchor(0, 0, 1, 1, 10, 57, 6, 8);
                var finish = Ui.Button(_placePanel, "完成装修", _ctrl.EndPlacement, 17, Ui.ButtonHot);
                ((RectTransform)finish.transform).Anchor(0, 0, 1, 0, 10, 8, 10, -48);
            }
            _placePanel.Anchor(1, 0, 1, 1, -380, 70, 10, 80 + HudFunctionOffset);
            _placePanel.gameObject.SetActive(true);
            RefreshPlacementPanel();
        }

        public void HidePlacementPanel()
        {
            if (_placePanel != null) _placePanel.gameObject.SetActive(false);
            if (_homePlacementPanel != null) _homePlacementPanel.gameObject.SetActive(false);
            if (S == null) return;
            RefreshShopDockVisibility();
            RefreshHomeDockVisibility();
        }

        public void RefreshPlacementPanel()
        {
            if (EditingResidence) { RefreshHomePlacementPanel(); return; }
            if (!_ctrl.PlacingShop || _placePanel == null || !_placePanel.gameObject.activeSelf) return;
            var content = _shopPlacementContent;
            var scroll = content.parent.parent.GetComponent<ScrollRect>();
            float previousScroll = scroll.verticalNormalizedPosition;
            Ui.Clear(content);
            Text title = Ui.Label(content, "店铺装修（今天歇业）", 22, TextAnchor.MiddleLeft, Ui.Accent);
            Ui.Size(title, -1, 34);
            Text mode = Ui.Label(content, "俯视图 · 格线 1 米 · 吸附 0.25 米", 15, TextAnchor.MiddleLeft, Ui.Dim);
            Ui.Size(mode, -1, 24);
            Text help = Ui.Label(content, "左键放置或挪动 · R 旋转 · Esc 放回\nX 收纳 · 滚轮缩放 · Q/E 转视角 · V 复位", 15, TextAnchor.UpperLeft, Ui.Dim);
            Ui.Size(help, -1, 44);
            _placeStatus = Ui.Label(content, "", 16, TextAnchor.UpperLeft);
            Ui.Size(_placeStatus, -1, 56);

            Line(content, "待摆放的家具（店铺后仓与临时存放）", 17);
            string limit = S.CurrentShop.RetailKind == "restaurant"
                ? $"餐桌 {GameSession.RestaurantTablesPlaced(S.CurrentShop)} / {GameSession.RestaurantTableLimit(S.PlayerShop.Size)}"
                : $"货架 {S.PlacedFixtures(FixtureKind.Shelf)} / {S.ShopShelfLimit}";
            Line(content, limit, 17, Ui.Accent);
            bool any = false;
            foreach (var kv in S.FurnitureToPlaceList(true))
            {
                FurnitureDef def = FurnitureCatalog.Get(kv.Key);
                if (def == null) continue;
                any = true;
                string id = def.Id;
                var button = Ui.Size(Ui.Button(content, $"{def.Name} ×{kv.Value}", () => _ctrl.SelectStock(id), 17,
                    Ui.SelectedButton(_ctrl.PlaceId == id && !_ctrl.PlaceHoldsPlaced)), -1, 40);
                ItemButtonPortrait(button, "furn:" + id, 32);
            }
            if (!any) Ui.Size(Ui.Label(content, "暂无可摆放家具", 15, TextAnchor.MiddleLeft, Ui.Dim), -1, 30);

            if (_ctrl.PlaceId != null)
            {
                Ui.Size(Ui.Button(content, "旋转（R）", _ctrl.RotateGhost, 17), -1, 40);
                if (_ctrl.PlaceHoldsPlaced) Ui.Size(Ui.Button(content, "收进后仓（X）", _ctrl.StoreSelected, 17, Ui.Warn), -1, 40);
            }
            var history = Ui.Row(content, 36);
            Ui.Size(Ui.Button(history, "撤销 Ctrl+Z", () => _ctrl.UndoPlacement(), 15, Ui.ButtonBg, S.CanUndoLayout), 0, 36, 1);
            Ui.Size(Ui.Button(history, "重做 Ctrl+Y", () => _ctrl.UndoPlacement(true), 15, Ui.ButtonBg, S.CanRedoLayout), 0, 36, 1);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = previousScroll;
        }

        private void UpdatePlacementStatus()
        {
            UpdateHomePlacementStatus();
            if (_placePanel == null || !_placePanel.gameObject.activeSelf || _placeStatus == null) return;
            if (!_ctrl.PlacingShop) return;
            if (_ctrl.PlaceId == null) _placeStatus.text = "选择待摆放的家具，或点击店里的家具移动。";
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
