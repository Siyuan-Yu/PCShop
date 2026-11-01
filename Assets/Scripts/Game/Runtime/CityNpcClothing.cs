using PCShopSell.Core.Session;
using UnityEngine;
namespace PCShopSell.Game.Runtime
{
    /// <summary>有限季节款式，在人物出场时确定；保留人物发型、肤色与主色，不按帧换衣。</summary>
    public sealed class CityNpcClothing : MonoBehaviour
    {
        public static void Apply(GameObject figure,GameSession session,string personId)
        {
            if(figure==null||session==null||string.IsNullOrEmpty(personId)||figure.GetComponent<CityNpcClothing>()!=null)return;
            figure.AddComponent<CityNpcClothing>();
            double temp=session.CityOutdoorTemperature(session.Clock.Today.TotalDays*1440L+8*60);
            int hash=0;foreach(char c in personId)hash=unchecked(hash*31+c);
            string style=temp<12?"winter_basic":temp<20?"jacket":temp<26?"shirt":(hash&1)==0?"cotton_short":"sport_basic";
            Prims.Dress(figure,style,CrowdView.PersonClothing(personId));
        }
    }
}
