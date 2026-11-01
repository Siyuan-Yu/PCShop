namespace PCShopSell.Core.Session
{
    public sealed partial class SaveData
    {
        public int cityProcurementVersion, cityProcurementNextId;
        public ProcurementPolicy[] cityProcurementPolicies;
        public ProcurementClaim[] cityProcurementClaims;
        public ProcurementOrderRecord[] cityProcurementOrders;
    }
}
