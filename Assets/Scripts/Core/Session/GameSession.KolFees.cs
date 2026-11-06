namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private long KolAppearanceRate(string person) => 200L + KolFollowers(person) / 100 + KolPopularity(person) * 5L;

        public long KolLiveSessionQuote(string person) => KolAppearanceRate(person) * 10;

        public long KolLiveSessionFee(KolLiveContract contract) =>
            contract.SessionFeeCents > 0 ? contract.SessionFeeCents : KolLiveSessionQuote(contract.PersonId) * 100;
    }
}
