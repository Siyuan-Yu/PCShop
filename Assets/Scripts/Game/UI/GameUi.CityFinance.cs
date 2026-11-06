namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        // Keep existing entry points directed to the two-page asset workspace.
        public void OpenCityFinanceOverview()
        {
            _assetStreet = _assetKind = _assetStoreId = null;
            OpenAssetWorkspace();
        }
        public void OpenShopFinance(string shopId) => OpenAssetWorkspace(0, shopId);
    }
}
