using System;
using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public static bool UsedCanRepair(UsedItem item) => item != null && item.Owned && item.Inspected && !item.Sold
            && item.Product != ArtifactProduct && !item.RepairAttempted
            && !(item.MarketReferencePrice > 0 && item.MarketGrade == 0)
            && (item.Condition == UsedCondition.Flawed || item.Condition == UsedCondition.Broken);

        public static double UsedRepairSpeed(Employee worker) => (.5 +
            (.6 * Math.Clamp(worker.Dexterity, 0, 100) + .4 * Math.Clamp(worker.Intelligence, 0, 100)) / 100) * EmployeeWorkRate(worker);
        public static double UsedRepairSuccessChance(Employee worker) => .5 + .004 *
            (.4 * Math.Clamp(worker.Dexterity, 0, 100) + .6 * Math.Clamp(worker.Intelligence, 0, 100));
        public static long UsedRepairMaterialCost(UsedItem item)
        {
            var restored = item.Copy();
            restored.Condition = UsedCondition.Good;
            return Math.Max(1, (long)Math.Ceiling(UsedValue(restored) * (item.Condition == UsedCondition.Broken ? .15 : .08)));
        }

        public string UsedRepairBlocker(OwnedShop shop, UsedItem item)
        {
            if (shop == null || shop.RetailKind != "secondhand" || !UsedCanRepair(item))
                return "只能修复已鉴定且尚未尝试修复的瑕疵或损坏旧物；文物与仿造品不参与修复。";
            if (item.Listed || UsedReserved(shop, item.Id)) return "请先将物品下架，等待顾客完成结账后再修复。";
            if (Furnished(shop, "used_repairbench") == 0) return "请先购买并摆放二手修复台。";
            if (!shop.Sim.InHours(Clock.Now)) return "请在营业时间安排修复。";
            if (SpecialWorkers(shop, EmployeeRole.Repairer, Clock.Now).Length == 0) return "请安排修复师到店上班。";
            if (Ledger.Balance < Money.FromYuan(UsedRepairMaterialCost(item))) return "现金不足以支付修复耗材。";
            return null;
        }

        public bool StartUsedRepair(int id)
        {
            if (!SpecialPlayer(CurrentShop) || CurrentShop.RetailKind != "secondhand") return Fail("请在自己的二手店安排修复。");
            var item = SpecialBusiness(CurrentShop.Place.Id).Used.FirstOrDefault(i => i.Id == id);
            string blocker = UsedRepairBlocker(CurrentShop, item);
            if (blocker != null) return Fail(blocker);
            return BeginUsedRepair(CurrentShop, item, Clock.Today);
        }

        private bool BeginUsedRepair(OwnedShop shop, UsedItem item, GameDate date)
        {
            long cost = UsedRepairMaterialCost(item);
            if (!Ledger.TryDebit(date, "进货", Money.FromYuan(cost), "旧物修复耗材 · " + UsedItemName(item),
                LedgerAttribution.ForShop(shop.Place.Id, "used:" + item.Id + ":repair", "修复"))) return false;
            item.RepairAttempted = true;
            item.RepairCost = cost;
            item.Cost += cost;
            item.RepairWorkTotal = item.Condition == UsedCondition.Broken ? 150 : 90;
            item.RepairWorkRemaining = item.RepairWorkTotal;
            item.RepairQualityWork = 0;
            return true;
        }

        public string UsedRepairStatus(OwnedShop shop, UsedItem item)
        {
            if (item.RepairWorkRemaining <= 0) return item.RepairSucceeded ? "修复成功" : "修复未成功";
            var worker = SpecialWorkers(shop, EmployeeRole.Repairer, Clock.Now).FirstOrDefault();
            if (!shop.Sim.InHours(Clock.Now) || worker == null || Furnished(shop, "used_repairbench") == 0)
                return "修复暂停 · 等待修复台和在岗修复师";
            return "修复排队／进行中 · 约剩 " + Math.Ceiling(item.RepairWorkRemaining / UsedRepairSpeed(worker)) + " 分钟";
        }

        private void TickUsedRepairs(OwnedShop shop, SpecialBusinessState state, GameTimestamp now)
        {
            if (!shop.Sim.InHours(now) || !CanServeSpecialShop(shop, now, SpecialPlayer(shop))) return;
            var workers = SpecialWorkers(shop, EmployeeRole.Repairer, now);
            int slots = Math.Min(Furnished(shop, "used_repairbench"), workers.Length);
            var processed = new HashSet<int>();
            for (int slot = 0; slot < slots; slot++)
            {
                var item = state.Used.Where(i => i.Owned && !i.Sold && i.RepairWorkRemaining > 0 && !processed.Contains(i.Id))
                    .OrderBy(i => i.Id).FirstOrDefault();
                if (item == null)
                {
                    item = state.Used.FirstOrDefault(i => UsedCanRepair(i) && !i.Listed && !UsedReserved(shop, i.Id)
                        && Money.FromYuan(UsedRepairMaterialCost(i)) <= Ledger.Balance);
                    if (item == null || !BeginUsedRepair(shop, item, now.Date)) continue;
                }
                processed.Add(item.Id);
                var worker = workers[slot];
                double work = Math.Min(item.RepairWorkRemaining, UsedRepairSpeed(worker));
                item.RepairWorkRemaining = Math.Max(0, item.RepairWorkRemaining - work);
                item.RepairQualityWork += work * UsedRepairSuccessChance(worker);
                if (item.RepairWorkRemaining > 0) continue;
                item.RepairSucceeded = Random.Chance(item.RepairQualityWork / item.RepairWorkTotal);
                if (item.RepairSucceeded) item.Condition = UsedCondition.Good;
                item.Price = Math.Max(1, UsedValue(item));
                SetUsedListing(shop, item, item.Price);
                Say(ShopTitle(shop) + " · " + UsedItemName(item) + (item.RepairSucceeded ? "修复成功" : "修复未成功，保持原状态")
                    + " · 参考价 ¥" + item.Price + " · 耗材 ¥" + item.RepairCost);
            }
        }
    }
}
