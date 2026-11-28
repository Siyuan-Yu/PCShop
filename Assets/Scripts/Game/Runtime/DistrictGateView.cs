using System.Collections.Generic;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    // Visual only: the Core movement checks are authoritative for walking, vehicles and saved routes.
    public sealed class DistrictGateView : MonoBehaviour
    {
        private GameController _game;
        private readonly List<(GameObject root, Transform sign, int tier)> _barriers = new();
        public void Initialize(GameController game)
        {
            _game=game;
            foreach(var gate in DistrictRoadGate.ForCity(game.City))
            {
                if (GameSession.FirstChapterTrial && gate.Tier > 1) continue;
                var root=new GameObject("DistrictCheckpoint_"+gate.Tier);
                root.transform.SetParent(transform,false);
                root.transform.position=StreetWorldBuilder.ToWorld(gate.Center);
                root.transform.rotation=Quaternion.LookRotation(new Vector3((float)gate.Direction.X,0,(float)gate.Direction.Y));
                for(int i=-3;i<=3;i++)
                {
                    Box(root.transform,new Vector3(i*2, .55f,0),new Vector3(1.9f,1.1f,.5f),new Color(.86f,.59f,.14f));
                    Box(root.transform,new Vector3(i*2, .6f,-.27f),new Vector3(.28f,.9f,.04f),new Color(.12f,.15f,.16f));
                }
                Box(root.transform,new Vector3(0,1.8f,0),new Vector3(.16f,2.5f,.16f),new Color(.24f,.28f,.3f));
                // Match the existing UI font so Chinese labels remain legible.
                var canvasObject=new GameObject("CheckpointSign",typeof(Canvas),typeof(UnityEngine.UI.Image));
                canvasObject.transform.SetParent(root.transform,false);
                var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var signBackground=canvasObject.GetComponent<UnityEngine.UI.Image>();signBackground.color=new Color(.06f,.09f,.13f,.95f);signBackground.raycastTarget=false;
                var rect=(RectTransform)canvasObject.transform;rect.sizeDelta=new Vector2(600,100);rect.localScale=Vector3.one*.024f;
                rect.localPosition=new Vector3(0,3.2f,-.1f);rect.localRotation=Quaternion.Euler(20,180,0);
                var textObject=new GameObject("Label",typeof(UnityEngine.UI.Text));textObject.transform.SetParent(rect,false);
                var label=textObject.GetComponent<UnityEngine.UI.Text>();
                label.font=UI.Ui.Font;label.fontSize=42;label.alignment=TextAnchor.MiddleCenter;label.color=new Color(1,.87f,.55f);label.raycastTarget=false;
                label.text = GameSession.FirstChapterTrial ? "试玩区域边界"
                    : "通往" + GameSession.DistrictName(gate.Tier) + " · 完成主线后放行";
                var labelRect=label.rectTransform;labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;labelRect.offsetMin=Vector2.zero;labelRect.offsetMax=Vector2.zero;
                _barriers.Add((root, rect, gate.Tier));
            }
        }
        private static void Box(Transform parent,Vector3 position,Vector3 scale,Color color)
        {
            var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.transform.SetParent(parent,false);
            box.transform.localPosition=position;box.transform.localScale=scale;
            box.GetComponent<Renderer>().sharedMaterial=Mats.Lit(color);
            var collider=box.GetComponent<Collider>();if(collider!=null){collider.enabled=false;Kill.It(collider);}
        }
        private void LateUpdate()=>Refresh();
        public void Refresh()
        {
            if(_game?.Session==null)return;
            bool outdoors = !_game.InHome && !_game.InInterior;
            foreach (var barrier in _barriers)
            {
                bool visible = outdoors && !_game.Session.DistrictRoadOpen(barrier.tier);
                if (barrier.root.activeSelf != visible) barrier.root.SetActive(visible);
                if (visible && _game.Cam != null)
                    barrier.sign.rotation = Quaternion.LookRotation(barrier.sign.position - _game.Cam.transform.position, Vector3.up);
            }
        }
    }
}
