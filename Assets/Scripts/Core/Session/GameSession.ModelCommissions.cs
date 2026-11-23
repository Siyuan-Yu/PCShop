using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public enum ModelCommissionStage { Offered, Waiting, EmployeeWork, ManualWork, Ready, Delivered, Returned }

    [Serializable]
    public sealed class ModelCommission
    {
        public int Id, Passes;
        public string ModelId, Customer, Theme;
        public bool Conversion;
        public long OfferUntil;
        public double Work, QualityWork, Quality;
        public ModelCommissionStage Stage;

        public ModelCommission Copy() => (ModelCommission)MemberwiseClone();
        public int WorkMinutes => Conversion ? 150 : 90;
        public int Materials => Conversion ? 60 : 30;
        public int BaseFee => Conversion ? 360 : 180;
        public int Payout => (int)Math.Round(BaseFee * (.5 + Quality / 15));
        public string Service => Conversion ? "改胶＋喷漆" : "喷漆";
        public string ModelName => Gifts.Get(ModelId)?.Name ?? "客带模型";
        public string PassName => Passes == 0 ? (Conversion ? "修件粘合" : "打磨底漆")
            : Passes == 1 ? "主色喷涂" : "细节收边";
    }

    [Serializable]
    public sealed class ModelCommissionBook
    {
        public string ShopId;
        public int NextId = 1;
        public long NextOffer;
        public List<ModelCommission> Orders = new List<ModelCommission>();

        public ModelCommissionBook Copy() => new ModelCommissionBook
        {
            ShopId = ShopId,
            NextId = NextId,
            NextOffer = NextOffer,
            Orders = Orders.Select(order => order.Copy()).ToList()
        };
    }

    public sealed partial class SaveData
    {
        public ModelCommissionBook[] modelCommissions;
    }

    public sealed partial class GameSession
    {
        public const int ModelCommissionLimit = 4;
        private const int ModelCommissionHistory = 6;
        private static readonly string[] ModelPaintThemes = { "海雾蓝", "暖铜橙", "苔绿", "星夜紫" };
        private readonly Dictionary<string, ModelCommissionBook> _modelCommissions =
            new Dictionary<string, ModelCommissionBook>();

        public static double ModelPainterSpeed(Employee person) => .65 + Math.Clamp(person.Dexterity, 0, 100) * .0085;
        public static double ModelPainterQuality(Employee person) => Math.Clamp(person.Dexterity, 0, 100) / 10d;

        public IReadOnlyList<ModelCommission> ModelCommissions(OwnedShop shop) =>
            shop != null && _modelCommissions.TryGetValue(shop.Place.Id, out var book)
                ? (IReadOnlyList<ModelCommission>)book.Orders : Array.Empty<ModelCommission>();

        public bool ModelCommissionsBusy(OwnedShop shop) =>
            ModelCommissions(shop).Any(o => IsAcceptedModelCommission(o.Stage));

        private static bool IsAcceptedModelCommission(ModelCommissionStage stage) =>
            stage == ModelCommissionStage.Waiting || stage == ModelCommissionStage.EmployeeWork
            || stage == ModelCommissionStage.ManualWork || stage == ModelCommissionStage.Ready;

        private void OfferModelCommission(OwnedShop shop, ShopCustomer customer)
        {
            if (!shop.Rented || shop.RetailKind != "models" || Furnished(shop, FurnitureCatalog.ModelPaintBench) == 0) return;
            if (!_modelCommissions.TryGetValue(shop.Place.Id, out var book))
                _modelCommissions.Add(shop.Place.Id, book = new ModelCommissionBook { ShopId = shop.Place.Id });
            long now = Clock.Now.TotalMinutes;
            if (now < book.NextOffer || book.Orders.Count(o => IsAcceptedModelCommission(o.Stage)
                || o.Stage == ModelCommissionStage.Offered && o.OfferUntil > now) >= ModelCommissionLimit) return;
            int id = book.NextId++;
            var models = ModelProducts.Regular.Where(p => p.ModelSeries == CityRetailSeriesFor(customer.PersonId ?? customer.VisitId)).ToArray();
            var model = models[customer.Id % models.Length];
            book.Orders.Add(new ModelCommission
            {
                Id = id,
                ModelId = model.Id,
                Customer = CityPerson(customer.PersonId)?.Name ?? "到店顾客",
                Theme = ModelPaintThemes[(customer.Id + id) % ModelPaintThemes.Length],
                Conversion = id % 2 == 0,
                OfferUntil = now + 2 * GameTimestamp.MinutesPerDay
            });
            book.NextOffer = now + (id % 2 + 1) * GameTimestamp.MinutesPerDay;
            if (SpecialPlayer(shop)) Say("有顾客带模型来委托改造，可到柜台或喷漆台查看。");
        }

        private void TickModelCommissions(GameTimestamp from, GameTimestamp to)
        {
            foreach (var shop in MyShops)
            {
                if (shop.RetailKind != "models" || !_modelCommissions.TryGetValue(shop.Place.Id, out var book)) continue;
                book.Orders.RemoveAll(o => o.Stage == ModelCommissionStage.Offered && to.TotalMinutes >= o.OfferUntil);
                var queue = book.Orders.Where(o => o.Stage == ModelCommissionStage.EmployeeWork).ToList();
                if (queue.Count == 0) continue;
                int benches = Furnished(shop, FurnitureCatalog.ModelPaintBench);
                if (benches == 0 || shop.Sim.ForcedClosed || shop.Sim.NotFitted) continue;
                for (long minute = from.TotalMinutes + 1; minute <= to.TotalMinutes && queue.Count > 0; minute++)
                {
                    var now = new GameTimestamp(minute);
                    if (!shop.Sim.InHours(now) || ShopPowerTraffic(shop, now) <= 0) continue;
                    var workers = SpecialWorkers(shop, EmployeeRole.ModelPainter, now);
                    int slots = Math.Min(queue.Count, Math.Min(benches, workers.Length));
                    for (int i = 0; i < slots; i++)
                    {
                        var order = queue[i];
                        double speed = ModelPainterSpeed(workers[i]);
                        if (CompanyResearchDelivered(shop, CompanyProject.Workflow)) speed /= .9;
                        double work = Math.Min(order.WorkMinutes - order.Work, speed);
                        order.Work += work;
                        order.QualityWork += work * ModelPainterQuality(workers[i]);
                        if (order.Work < order.WorkMinutes - 1e-8) continue;
                        order.Quality = Math.Round(order.QualityWork / order.WorkMinutes, 1);
                        order.Stage = ModelCommissionStage.Ready;
                        Say($"{ShopTitle(shop)} · {order.ModelName}已完工，可到店交付。");
                    }
                    queue.RemoveAll(o => o.Stage == ModelCommissionStage.Ready);
                }
            }
        }

        private bool CanManageModelCommission(OwnedShop shop) =>
            SpecialPlayer(shop) && shop.Rented && shop.RetailKind == "models";

        private ModelCommission ModelCommissionAt(OwnedShop shop, int id) =>
            CanManageModelCommission(shop) ? ModelCommissions(shop).FirstOrDefault(o => o.Id == id) : null;

        public bool AcceptModelCommission(OwnedShop shop, int id)
        {
            var order = ModelCommissionAt(shop, id);
            if (order == null || order.Stage != ModelCommissionStage.Offered || Clock.Now.TotalMinutes >= order.OfferUntil)
                return Fail("这笔委托已不可接。");
            if (Furnished(shop, FurnitureCatalog.ModelPaintBench) == 0) return Fail("请先摆放喷漆台。");
            order.Stage = ModelCommissionStage.Waiting;
            return true;
        }

        public string ModelCommissionWorkBlocker(OwnedShop shop, ModelCommission order, bool manual)
        {
            if (order == null || !CanManageModelCommission(shop)) return "请在自己的模型手办店操作。";
            int benches = Furnished(shop, FurnitureCatalog.ModelPaintBench);
            if (benches == 0) return "请先摆放喷漆台。";
            if (shop.Sim.ForcedClosed || ShopPowerTraffic(shop, Clock.Now) <= 0) return "喷漆台目前无法使用。";
            if (manual && shop.Sim.InHours(Clock.Now)
                && ModelCommissions(shop).Count(o => o.Stage == ModelCommissionStage.EmployeeWork) >= benches
                && SpecialWorkers(shop, EmployeeRole.ModelPainter, Clock.Now).Length >= benches)
                return "喷漆台都在使用中，请等员工做完一件。";
            if (!manual && !ShopStaff(shop, EmployeeRole.ModelPainter).Any()) return "请先安排喷漆师。";
            if (order.Stage == ModelCommissionStage.Waiting && !Ledger.CanAfford(Money.FromYuan(order.Materials)))
                return $"现金不足，耗材需要 ¥{order.Materials}。";
            return null;
        }

        public bool StartModelCommission(OwnedShop shop, int id, bool manual)
        {
            var order = ModelCommissionAt(shop, id);
            if (order == null || (order.Stage != ModelCommissionStage.Waiting
                && !(manual && order.Stage == ModelCommissionStage.ManualWork))) return false;
            string blocker = ModelCommissionWorkBlocker(shop, order, manual);
            if (blocker != null) return Fail(blocker);
            if (order.Stage == ModelCommissionStage.ManualWork) return true;
            if (!Ledger.TryDebit(Clock.Today, "进货", Money.FromYuan(order.Materials), "模型委托耗材 · " + order.ModelName,
                LedgerAttribution.ForShop(shop.Place.Id, transactionKind: "模型委托耗材"))) return false;
            order.Stage = manual ? ModelCommissionStage.ManualWork : ModelCommissionStage.EmployeeWork;
            return true;
        }

        public bool CompleteModelCommissionPass(OwnedShop shop, int id, double accuracy)
        {
            var order = ModelCommissionAt(shop, id);
            if (order == null || order.Stage != ModelCommissionStage.ManualWork || double.IsNaN(accuracy)) return false;
            string blocker = ModelCommissionWorkBlocker(shop, order, true);
            if (blocker != null) return Fail(blocker);
            order.QualityWork += Math.Clamp(accuracy, 0, 1) * 10;
            order.Passes++;
            if (order.Passes == 3)
            {
                order.Quality = Math.Round(order.QualityWork / 3, 1);
                order.Stage = ModelCommissionStage.Ready;
            }
            return true;
        }

        public bool DeliverModelCommission(OwnedShop shop, int id)
        {
            var order = ModelCommissionAt(shop, id);
            if (order == null || order.Stage != ModelCommissionStage.Ready) return false;
            if (Furnished(shop, FurnitureCatalog.ShopCounter) == 0) return Fail("请先摆放收银台。");
            Ledger.Credit(Clock.Today, ShopSimulation.SalesCategory, Money.FromYuan(order.Payout),
                $"模型委托 · {order.Service} · {order.ModelName}", LedgerAttribution.ForShop(shop.Place.Id, transactionKind: "销售"));
            shop.Sim.AdjustShopReputation((order.Quality - 5) * .06);
            if (order.Quality >= 8) shop.Sim.AdjustPersonalReputation(.25);
            FinishModelCommission(shop, order, ModelCommissionStage.Delivered);
            Say($"已交付{order.ModelName} · 品质 {order.Quality:0.#}/10 · 收款 ¥{order.Payout}");
            return true;
        }

        public bool ReturnModelCommission(OwnedShop shop, int id)
        {
            var order = ModelCommissionAt(shop, id);
            if (order == null || !IsAcceptedModelCommission(order.Stage)) return false;
            FinishModelCommission(shop, order, ModelCommissionStage.Returned);
            return true;
        }

        private void FinishModelCommission(OwnedShop shop, ModelCommission order, ModelCommissionStage stage)
        {
            order.Stage = stage;
            var book = _modelCommissions[shop.Place.Id];
            book.Orders.RemoveAll(o => o.Stage == ModelCommissionStage.Offered && Clock.Now.TotalMinutes >= o.OfferUntil);
            var history = book.Orders.Where(o => o.Stage == ModelCommissionStage.Delivered || o.Stage == ModelCommissionStage.Returned)
                .OrderByDescending(o => o.Id).Skip(ModelCommissionHistory).ToArray();
            foreach (var old in history) book.Orders.Remove(old);
        }

        private void MoveModelCommissionBook(string from, string to)
        {
            if (!_modelCommissions.TryGetValue(from, out var book)) return;
            _modelCommissions.Remove(from);
            book.ShopId = to;
            _modelCommissions[to] = book;
        }

        private void LoadModelCommissions(SaveData data)
        {
            foreach (var book in data.modelCommissions ?? Array.Empty<ModelCommissionBook>())
            {
                if (book == null || string.IsNullOrEmpty(book.ShopId) || ShopAt(book.ShopId)?.RetailKind != "models") continue;
                book.Orders ??= new List<ModelCommission>();
                _modelCommissions[book.ShopId] = book.Copy();
            }
        }
    }
}
