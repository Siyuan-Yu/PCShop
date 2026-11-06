namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private string _kolReceptionAgency;
        private int _kolReceptionDay = -1;

        public string McnReceptionBlocker(string agencyId)
        {
            if (BrandAgency.Get(agencyId) == null) return "请先选择一家MCN公司";
            return McnVisitBlocker();
        }

        public bool BeginMcnRecruitment(string agencyId)
        {
            string why = McnReceptionBlocker(agencyId);
            if (why != null) return Fail(why);
            _kolReceptionAgency = agencyId;
            _kolReceptionDay = BrandDay;
            return true;
        }

        private string McnSigningBlocker(string person)
        {
            var agency = BrandAgency.ForPerson(person);
            string why = McnReceptionBlocker(agency.Id);
            if (why != null) return why;
            if (_kolReceptionAgency != agency.Id || _kolReceptionDay != BrandDay)
                return "请先与" + agency.Name + "人事洽谈代言合约";
            return null;
        }
    }
}
