using PCShopSell.Core.Session;

namespace PCShopSell.Tests
{
    // Establish an existing phone contact for tests focused on streaming or exhibitions.
    internal static class McnSigningTest
    {
        internal static bool Sign(GameSession s, string brand, string person)
        {
            s.Meet(person, 0);
            if (!s.SignKolAmbassador(person, 90, 1, remote: true)) return false;
            var contract = s.ActiveKolAmbassador(person);
            if (contract.BrandId != brand && !(contract.BrandId == null && s.AssignKolBrand(contract.Id, brand))) return false; // 签约时已自动分配品牌
            return s.SetKolSchedule(contract.Id, contract.StartMinute, contract.EndMinute, true);
        }
    }
}
