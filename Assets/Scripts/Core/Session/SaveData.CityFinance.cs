using System;

namespace PCShopSell.Core.Session
{
    public sealed partial class SaveData
    {
        public int cityFinanceVersion;
        public SaveCityLedgerAttribution[] cityLedgerAttributions;
    }
    [Serializable]
    public sealed class SaveCityLedgerAttribution
    {
        public int index, businessKind, quantity;
        public string entryId, shopId, propertyId, activityId, sourceEventId, transactionKind, productId;
        public long unitCostCents;
        public string employeeId;
        public int payrollWorkday = -1;
    }
}
