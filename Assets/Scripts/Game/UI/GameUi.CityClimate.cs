using UnityEngine;
namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        public void OpenCityClimate()
        {
            OpenWindow("天气与穿衣",700,490,w=>
            {
                Line(w.Content,S.WeatherSummary,23,Ui.Accent);
                Para(w.Content,S.CityWeatherForecast,19);
                Para(w.Content,S.CityComfortSummary,19);
                Para(w.Content,"厚薄衣物按适温和场合选择，价格不决定保暖。已安装且可用的风扇在热天改善舒适；空调改善所在室内冷暖，自动发挥作用。",17,Ui.Dim);
                if(S.CurrentPlace!=null)Para(w.Content,S.CityShopClimateSummary(S.CurrentPlace.Id),18);
                Para(w.Content,"家具水电沿用固定月费，无额外运行电费；坏风扇修好后才有效。",17,Ui.Dim);
            });
        }
    }
}
