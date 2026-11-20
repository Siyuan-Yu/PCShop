namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        // Keep existing entry points directed to the two-page asset workspace.
        public void OpenCityFinanceOverview()
        {
            OpenAssetFinance();
        }
        public void OpenShopFinance(string shopId) => OpenAssetWorkspace(0, shopId);
    }
}
