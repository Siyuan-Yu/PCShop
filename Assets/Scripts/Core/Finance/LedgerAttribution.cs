using System;

namespace PCShopSell.Core.Finance
{
    public enum LedgerBusinessKind { Historical, Operating, Personal, AssetInvestment, Financing, InternalTransfer }

    /// <summary>收支发生时记录的业务归属；显示名称和备注不参与归属判断。</summary>
    public sealed class LedgerAttribution
    {
        public LedgerBusinessKind BusinessKind { get; }
        public string ShopId { get; }
        public string PropertyId { get; }
        public string ActivityId { get; }
        public string SourceEventId { get; }
        public string TransactionKind { get; }
        public string ProductId { get; private set; }
        public int Quantity { get; private set; }
        public long UnitCostCents { get; private set; }
        public string EmployeeId { get; private set; }
        public int PayrollWorkday { get; private set; } = -1;
        public LedgerAttribution Payroll(string employeeId, int workday) { EmployeeId=Clean(employeeId); PayrollWorkday=workday; return this; }
        public bool IsInternalTransfer => BusinessKind == LedgerBusinessKind.InternalTransfer;

        public LedgerAttribution(LedgerBusinessKind businessKind, string shopId = null, string propertyId = null,
            string activityId = null, string sourceEventId = null, string transactionKind = null)
        {
            BusinessKind = businessKind; ShopId = Clean(shopId); PropertyId = Clean(propertyId); ActivityId = Clean(activityId);
            SourceEventId = Clean(sourceEventId); TransactionKind = Clean(transactionKind);
        }
        private static string Clean(string value) => string.IsNullOrEmpty(value) ? null : value;
        public LedgerAttribution Product(string productId, int quantity, long unitCostCents) =>
            new LedgerAttribution(BusinessKind, ShopId, PropertyId, ActivityId, SourceEventId, TransactionKind)
            { ProductId = Clean(productId), Quantity = Math.Max(0, quantity), UnitCostCents = Math.Max(0, unitCostCents), EmployeeId=EmployeeId, PayrollWorkday=PayrollWorkday };
        public static LedgerAttribution ForShop(string shopId, string sourceEventId = null, string transactionKind = null) =>
            new LedgerAttribution(LedgerBusinessKind.Operating, shopId, sourceEventId: sourceEventId, transactionKind: transactionKind);
        public static LedgerAttribution ForAsset(string propertyId = null, string sourceEventId = null, string transactionKind = null) =>
            new LedgerAttribution(LedgerBusinessKind.AssetInvestment, propertyId: propertyId, sourceEventId: sourceEventId, transactionKind: transactionKind);

        public static LedgerAttribution FromCategory(string category)
        {
            switch (category)
            {
                case "零售": case "装机": case "进货": case "工资": case "培训": case "招聘": case "推广":
                case "运费": case "偿还货款": case "企业奖励": case "企业固定费": case "企业维护": case "进货退款":
                    return new LedgerAttribution(LedgerBusinessKind.Operating, transactionKind: category);
                case "贷款到账": case "偿还贷款": case "贷款利息": case "偿还房贷": case "房贷利息":
                    return new LedgerAttribution(LedgerBusinessKind.Financing, transactionKind: category);
                case "企业资金转回": case "企业周转金": case "采购暂存": case "采购找零":
                    return new LedgerAttribution(LedgerBusinessKind.InternalTransfer, transactionKind: category);
                case "购置物业": case "物业出售": case "物业租金": case "物业维护": case "指数投资": case "指数赎回":
                case "购置住宅": case "住宅出售": case "企业投资": case "园区投资": case "MCN投资": case "押金": case "履约保证金":
                    return new LedgerAttribution(LedgerBusinessKind.AssetInvestment, transactionKind: category);
                case "餐饮": case "生活": case "人情": case "医疗": case "购物": case "网购": case "个人衣物": case "打工": case "罚款":
                    return new LedgerAttribution(LedgerBusinessKind.Personal, transactionKind: category);
                default: return new LedgerAttribution(LedgerBusinessKind.Historical, transactionKind: category);
            }
        }
        public static string KindName(LedgerBusinessKind kind)
        {
            switch (kind)
            {
                case LedgerBusinessKind.Operating: return "经营";
                case LedgerBusinessKind.Personal: return "个人生活";
                case LedgerBusinessKind.AssetInvestment: return "资产投资";
                case LedgerBusinessKind.Financing: return "融资";
                case LedgerBusinessKind.InternalTransfer: return "内部划转";
                default: return "历史未归属";
            }
        }
    }
}
