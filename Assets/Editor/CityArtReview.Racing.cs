using PCShopSell.Core.Session;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.EditorTools
{
    public static partial class CityArtReview
    {
        // Explicitly requested map review. The production scene is rendered first, then
        // identical camera coordinates are used to place explanatory UI labels.
        private static void RacingOverview(string output)
        {
            camera.farClipPlane=5000;
            var focus=new Vector3(0,0,155);
            Shot(output,"actual-city-overview",focus,0,70,690);
            var canvas=Ui.CreateCanvas("RacingMapAnnotations",100);
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            foreach(var t in RaceTracks.All)
            {
                var placed=RaceTracks.Get(t.Id,reviewLayout);
                var point=camera.WorldToViewportPoint(new Vector3((float)placed.X,7,(float)placed.Y));
                var color=new[]{new Color(.2f,.48f,.27f),new Color(.15f,.40f,.68f),new Color(.73f,.40f,.12f),new Color(.47f,.27f,.62f)}[t.Tier];
                var label=Ui.Panel(canvas.transform,"TrackLabel_"+t.Id,color).rectTransform;
                label.anchorMin=label.anchorMax=new Vector2(point.x,point.y);label.pivot=new Vector2(.5f,0);
                float offset=(float)t.Depth*.39f+18;
                label.anchoredPosition=new Vector2(0,offset);label.sizeDelta=new Vector2(220,62);
                var text=Ui.Label(label,(t.Tier+1)+"  "+t.Name+"\n"+GameSession.DistrictName(t.Tier)+"片区 · 已建赛场",19,TextAnchor.MiddleCenter,Color.white);
                text.rectTransform.Fill(5);
                var dot=Ui.Panel(canvas.transform,"TrackPin_"+t.Id,color).rectTransform;
                dot.anchorMin=dot.anchorMax=new Vector2(point.x,point.y);dot.anchoredPosition=Vector2.zero;dot.sizeDelta=new Vector2(10,10);
                var leader=Ui.Panel(canvas.transform,"TrackLeader_"+t.Id,color).rectTransform;
                leader.anchorMin=leader.anchorMax=new Vector2(point.x,point.y);leader.pivot=new Vector2(.5f,0);
                leader.anchoredPosition=Vector2.zero;leader.sizeDelta=new Vector2(2,offset);
            }
            var title=Ui.Panel(canvas.transform,"OverviewCaption",new Color(.08f,.12f,.14f,.92f)).rectTransform;
            title.Anchor(0,1,0,1,24,-84,-594,20);
            Ui.Label(title,"游戏实际鸟瞰 · 四座赛场的位置\n使用当前地图、建筑与赛道，正北朝上",22,TextAnchor.MiddleCenter,Color.white).rectTransform.Fill(8);
            Canvas.ForceUpdateCanvases();
            Shot(output,"actual-city-racing-locations",focus,0,70,690);
            Object.DestroyImmediate(canvas.gameObject);
            Shot(output,"actual-lake-racing-closeup",new Vector3(-40,0,170),0,70,485);
        }
    }
}
