using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
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

        public void OpenWardrobeWindow() => OpenRoomWindow("衣柜", 700, 440, BuildWardrobe);

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
            Line(w.Content, $"当前魅力 {S.EffectiveCharm}，其中衣着加成 +{S.Outfit.CharmBonus}。柜子里的衣服：", 18);
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
            Line(w.Content, "没有的衣服：网购（2 天到）。点右上角“网购”。", 16, Ui.Dim);
        }

        // ---------- 电饭锅：真做饭 ----------

        private double _cookJin = 1;
        private string _cookIngredient;

        private void BuildCooker(Window w)
        {
            Pantry pan = S.Pantry;
            PlayerStats st = S.Player;
            Line(w.Content, $"饥饿 {st.Hunger:0}  健康 {st.Health:0}   米缸：{pan.RiceJin:0.#} 斤（1 斤约 {Pantry.BowlsPerJin} 碗）", 18);
            var ings = pan.Ingredient.GroupBy(b => b.Id).ToList();
            Line(w.Content, ings.Count == 0 ? "没有别的食材（农贸市场 6:00–18:00 有卖，没有冰箱放不久）。" : "食材：" + string.Join("、", ings.Select(g => $"{g.First().Name}×{g.Sum(b => b.Count)}")), 16, Ui.Dim);

            // 放多少米
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

            // 加什么
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
            Ui.Size(Ui.Button(w.Content, $"开始焖饭：{_cookJin:0.#} 斤，约 {bowls} 碗，要 {Pantry.CookMinutes} 分钟", () =>
            {
                S.CookRice(_cookJin, _cookIngredient);
                w.Rebuild();
            }, 19, Ui.ButtonHot, pan.RiceJin >= 0.5 && _cookJin >= 0.5), -1, 48);

            // 现成的饭
            Line(w.Content, $"做好的饭：{pan.CookedBowls} 碗（没有冰箱，隔夜就坏）  饭盒：{pan.PackedBowls} 碗（带出门，饭点优先吃）", 17);
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
        }

        // ---------- “我的” ----------

        public void OpenMyPanel()
        {
            OpenWindow("我的", 860, 860, w =>
            {
                RectTransform actions = Ui.Row(w.Content, 50);
                Ui.Size(Ui.Button(actions, "摆放家具（家里）", () => StartPlacement(false), 18, Ui.ButtonHot, _ctrl.InHome), 230);
                Ui.Size(Ui.Button(actions, "装修店面（歇业一天）", () => StartPlacement(true), 18, Ui.ButtonBg, _ctrl.InInterior), 250);
                Ui.Size(Ui.Button(actions, S.HasSmartphone ? "网购" : "网购（没手机）", OpenOnlinePanel, 18, Ui.ButtonBg, S.HasSmartphone), S.HasSmartphone ? 110 : 190);
                if (!_ctrl.InHome && !_ctrl.InInterior) Line(w.Content, "摆放家具要在家里；改造店面要在自己的店里。", 15, Ui.Dim);

                RectTransform list = Ui.Scroll(w.Content, 660);
                PlayerStats st = S.Player;
                Line(list, "—— 我 ——", 18, Ui.Accent);
                Line(list, $"饥饿 {st.Hunger:0}  精力 {st.Energy:0}/{st.MaxEnergy:0}  健康 {st.Health:0}  欠觉 {st.SleepDebt:0.#} 小时", 17, Ui.Dim);
                Line(list, $"智力 {st.Intelligence}  魅力 {S.EffectiveCharm}（衣着 +{S.Outfit.CharmBonus}）  技术 {st.Skill}   现金 {S.Ledger.Balance}", 17, Ui.Dim);
                Line(list, "三餐（7:30 / 12:00 / 18:30）：先吃饭盒，在家就吃自己做的，都没有就在外面吃，每顿 " + GameSession.MealPrice, 15, Ui.Dim);

                Line(list, "—— 出租屋 ——", 18, Ui.Accent);
                Line(list, $"月租 {GameSession.HomeRent}；睡眠恢复 ×{S.HomeSleepQuality:0.##}；" + (S.HomeUtilities.IsPositive ? $"水电 {S.HomeUtilities}/月" : "不用交水电（没有电器）"), 17);
                string placed = string.Join("、", S.HomeLayout.GroupBy(p => p.Id).Select(g => FurnitureLabel(g.Key, g.Count())));
                Line(list, "屋里摆着：" + (placed.Length == 0 ? "什么都没有" : placed), 16, Ui.Dim);

                Line(list, "—— 背包 ——", 18, Ui.Accent);
                Line(list, $"负重 {S.CarriedKg:0.#}/{S.CarryLimitKg:0.#} 公斤（{S.CurrentVehicle.Name}；进货的货也算）。" + (S.HasSmartphone ? "" : "没有智能手机，不能网购、上 1788。"), 16, S.CarriedKg > S.CarryLimitKg * 0.85 ? Ui.Warn : Ui.Dim);
                Backpack bag = S.Backpack;
                var bagIngs = bag.Food.Ingredient.GroupBy(b => b.Id).ToList();
                string bagFood = (bag.Food.RiceJin > 0 ? $"大米 {bag.Food.RiceJin:0.#} 斤" : "") + (bagIngs.Count > 0 ? "  " + string.Join("、", bagIngs.Select(g => $"{g.First().Name}×{g.Sum(b => b.Count)}")) : "");
                Line(list, "买的菜米：" + (bagFood.Trim().Length == 0 ? "没有" : bagFood.Trim()) + "（回家自动放进厨房）", 16);
                var bagFurn = bag.FurnitureLines.ToList();
                if (bagFurn.Count > 0) Line(list, "背着的家具：" + string.Join("、", bagFurn.Select(kv => FurnitureLabel(kv.Key, kv.Value))) + "（回家或回店里放进家具背包）", 16);
                Line(list, "礼品：" + (bag.GiftLines.Count() == 0 ? "没有。去烟酒茶行、农贸市场买，或者网购。" : string.Join("、", bag.GiftLines.Select(kv => $"{Gifts.Get(kv.Key).Name}×{kv.Value}"))), 16);

                Line(list, "—— 吃的 ——", 18, Ui.Accent);
                Pantry pan = S.Pantry;
                Line(list, $"大米 {pan.RiceJin:0.#} 斤   做好的饭 {pan.CookedBowls} 碗   饭盒 {pan.PackedBowls} 碗", 17);
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

                Line(list, "—— 店里的摆设 ——", 18, Ui.Accent);
                Line(list, $"装饰分 {S.ShopDecorAppeal:0}，客流 ×{S.ShopDecorTrafficMultiplier:0.00}，顾客多等 {S.ShopDecorPatience} 分钟。摆放店里的东西要歇业装修一天。", 16);
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
            if (!S.HasSmartphone)
            {
                Toast(GameSession.NoPhoneMessage);
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
            Line(w.Content, "宜居家具城：现场付钱。轻便的自己背回去；超过 20 公斤的太大件，塞不进背包，得叫货车拖（运费 ¥120，明早送到家）。线上同款便宜 15%，2 天快递到家。", 16, Ui.Dim);
            Line(w.Content, $"现金 {S.Ledger.Balance}   负重 {S.CarriedKg:0.#}/{S.CarryLimitKg:0.#} 公斤", 18);
            RectTransform list = Ui.Scroll(w.Content, 520);
            Line(list, "—— 家里用 ——", 18, Ui.Accent);
            foreach (FurnitureDef f in FurnitureCatalog.Purchasable(false)) MallRow(w, list, f);
            Line(list, "—— 店里的摆设（摆放要歇业装修一天）——", 18, Ui.Accent);
            foreach (FurnitureDef f in FurnitureCatalog.Purchasable(true)) MallRow(w, list, f);
        }

        private void MallRow(Window w, RectTransform list, FurnitureDef def)
        {
            RectTransform row = Ui.Row(list, 58);
            Ui.Size(Ui.Label(row, $"{def.Name}  <size=14><color={(def.IsHeavy ? "#E0A86A" : "#A8B0BA")}>重 {def.WeightKg:0.#} 公斤{(def.IsHeavy ? "（太大件）" : "")}</color></size>\n<size=14><color=#A8B0BA>{def.Description}</color></size>", 18), 430);
            Ui.Size(Ui.Label(row, def.Price.ToString(), 19, TextAnchor.MiddleLeft, Ui.Good), 90);
            int have = S.FurnitureInStock(def.Id) + S.Backpack.FurnitureCount(def.Id);
            Ui.Size(Ui.Label(row, have > 0 ? $"背包 ×{have}" : "", 15, TextAnchor.MiddleLeft, Ui.Dim), 70);
            if (def.IsHeavy)
            {
                Ui.Size(Ui.Button(row, $"叫货车 +{FurnitureCatalog.TruckFee}", () =>
                {
                    if (S.BuyFurnitureByTruck(def.Id)) Toast($"叫了货车，{def.Name}明早送到家。");
                    w.Rebuild();
                }, 16, Ui.Warn), 130);
            }
            else
            {
                Ui.Size(Ui.Button(row, "买", () =>
                {
                    S.BuyFurnitureAtMall(def.Id);
                    w.Rebuild();
                }, 17, Ui.ButtonHot), 130);
            }
        }

        private void BuildTobacco(Window w, Place p)
        {
            Line(w.Content, "烟酒茶行：现场付钱，买了放进背包，再去送人。网购便宜 15%，但要等 2 天。", 16, Ui.Dim);
            Line(w.Content, $"现金 {S.Ledger.Balance}   负重 {S.CarriedKg:0.#}/{S.CarryLimitKg:0.#} 公斤", 18, Ui.Dim);
            RectTransform list = Ui.Scroll(w.Content, 380);
            foreach (GiftDef def in Gifts.All.Where(x => x.SoldAt == Gifts.TobaccoBusiness)) ShopGiftRow(w, list, def, true);
        }

        private void ShopGiftRow(Window w, RectTransform list, GiftDef def, bool open)
        {
            RectTransform row = Ui.Row(list, 66);
            Ui.Size(Ui.Label(row, $"{def.Name}\n<size=14><color=#A8B0BA>{def.Description}  重 {def.WeightKg:0.#} 公斤</color></size>", 18), 400);
            Ui.Size(Ui.Label(row, def.Price.ToString(), 19, TextAnchor.MiddleLeft, Ui.Good), 90);
            int have = S.Backpack.GiftCount(def.Id);
            Ui.Size(Ui.Label(row, have > 0 ? $"背包 ×{have}" : "", 15, TextAnchor.MiddleLeft, Ui.Dim), 80);
            Ui.Size(Ui.Button(row, "买", () =>
            {
                if (S.BuyGift(def.Id)) Toast($"买了 {def.Name}");
                w.Rebuild();
            }, 17, Ui.ButtonHot, open), 70);
        }

        private void BuildPhoneShop(Window w, Place p)
        {
            Line(w.Content, "手机店。网购和 1788 进货都要用智能手机。", 17, Ui.Dim);
            Line(w.Content, $"现金 {S.Ledger.Balance}", 18);
            RectTransform row = Ui.Row(w.Content, 64);
            Ui.Size(Ui.Label(row, "智能手机\n<size=14><color=#A8B0BA>能网购家具衣服礼品，能在 1788 下单进货。</color></size>", 18), 400);
            Ui.Size(Ui.Label(row, GameSession.PhonePrice.ToString(), 19, TextAnchor.MiddleLeft, Ui.Good), 90);
            Ui.Size(Ui.Button(row, S.HasSmartphone ? "已有" : "买", () =>
            {
                if (S.BuySmartphone()) Toast("买了智能手机。");
                w.Rebuild();
            }, 17, Ui.ButtonHot, !S.HasSmartphone), 80);
        }

        private static string GiftLabel(NpcDef n)
        {
            return n.GiftItemId == null ? $"请{n.GiftName} {n.GiftPrice}" : $"送{n.GiftName}";
        }

        private void BuildFarm(Window w, Place p)
        {
            bool open = S.IsFarmMarketOpen;
            Line(w.Content, open ? "营业中（每天 6:00–18:00）。米按斤卖，菜、蛋、肉当天最新鲜。" : "<color=#E0806A>收摊了（每天 6:00–18:00）</color>");
            Line(w.Content, $"现金 {S.Ledger.Balance}   家里：大米 {S.Pantry.RiceJin:0.#} 斤   负重 {S.CarriedKg:0.#}/{S.CarryLimitKg:0.#} 公斤", 18, Ui.Dim);
            Line(w.Content, "买的东西放进背包，回家自动放进厨房；太重了拿不动。家里没有冰箱，买多了会放坏。", 15, Ui.Dim);

            RectTransform rrow = Ui.Row(w.Content, 50);
            Ui.Size(Ui.Label(rrow, $"大米 {Pantry.RicePricePerJin}/斤", 19), 200);
            foreach (int jin in new[] { 1, 5, 10 })
            {
                int j = jin;
                Ui.Size(Ui.Button(rrow, "买" + j + "斤", () =>
                {
                    S.BuyRice(j);
                    w.Rebuild();
                }, 17, Ui.ButtonHot, open), 100);
            }

            foreach (GiftDef g in Gifts.All.Where(x => x.SoldAt == Gifts.FarmBusiness)) ShopGiftRow(w, w.Content, g, open);

            foreach (IngredientDef def in Ingredients.All)
            {
                IngredientDef d = def;
                RectTransform row = Ui.Row(w.Content, 56);
                Ui.Size(Ui.Label(row, $"{d.Name}（{d.Unit}）\n<size=14><color=#A8B0BA>可焖「{d.DishName}」，保质 {d.ShelfDays} 天</color></size>", 18), 380);
                Ui.Size(Ui.Label(row, d.Price.ToString(), 19, TextAnchor.MiddleLeft, Ui.Good), 80);
                Ui.Size(Ui.Label(row, $"家里 ×{S.Pantry.IngredientCount(d.Id) + S.Backpack.Food.IngredientCount(d.Id)}", 15, TextAnchor.MiddleLeft, Ui.Dim), 90);
                foreach (int n in new[] { 1, 3 })
                {
                    int count = n;
                    Ui.Size(Ui.Button(row, "买" + count, () =>
                    {
                        S.BuyIngredient(d.Id, count);
                        w.Rebuild();
                    }, 17, Ui.ButtonHot, open), 70);
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
            Text help = Ui.Label(_placePanel, "左键：放下  ·  R：旋转\n右键 / Esc：放回  ·  X：收进背包\n点已经摆好的家具可以拿起来挪", 15, TextAnchor.UpperLeft, Ui.Dim);
            Ui.Size(help, -1, 64);
            _placeStatus = Ui.Label(_placePanel, "", 16, TextAnchor.UpperLeft);
            Ui.Size(_placeStatus, -1, 56);

            Line(_placePanel, shop ? "背包里的店面摆设：" : "背包里的家具：", 17);
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
            Ui.Size(Ui.Button(_placePanel, "完成", _ctrl.EndPlacement, 19, Ui.ButtonHot), -1, 46);
        }

        private void UpdatePlacementStatus()
        {
            if (_placePanel == null || !_placePanel.gameObject.activeSelf || _placeStatus == null) return;
            if (!_ctrl.Placing) return;
            if (_ctrl.PlaceId == null) _placeStatus.text = "选一件背包里的家具，或者点屋里的家具拿起来挪。";
            else if (_ctrl.PlaceError == null) _placeStatus.text = "<color=#7CE38B>这里可以放，点左键。</color>";
            else _placeStatus.text = "<color=#E0806A>" + _ctrl.PlaceError + "</color>";
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
