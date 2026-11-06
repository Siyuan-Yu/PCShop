using PCShopSell.Core.Session;

namespace PCShopSell.Tests
{
    // Establish an existing phone contact for tests focused on streaming or exhibitions.
    internal static class McnSigningTest
    {
        internal static bool Sign(GameSession s, string brand, string person)
        {
            s.Meet(person, 0);
            if (!s.SignKolAmbassador(person, 90, 1, remote: true, phoneCall: true)) return false;
            var contract = s.ActiveKolAmbassador(person);
            return s.AssignKolBrand(contract.Id, brand) &&
                s.SetKolSchedule(contract.Id, contract.StartMinute, contract.EndMinute, true);
        }
    }
}
