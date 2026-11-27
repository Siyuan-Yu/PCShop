using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Game.Runtime;
using UnityEngine;

namespace PCShopSell.Game
{
    public sealed partial class GameController
    {
        private GameObject _eventCatView, _eventCatBowl;
        private readonly Dictionary<string, GameObject> _eventSampleViews = new Dictionary<string, GameObject>();
        private void SyncShopEventViews()
        {
            if (Session == null || World == null) return;
            Transform Parent(string shopId) => InInterior && Session.CurrentPlace?.Id == shopId ? Interior.Root
                : World.Places.TryGetValue(shopId, out PlaceVisual place) ? place.Root.transform : null;
            var cat = Session.EventCat;
            if (cat == null)
            { if (_eventCatView != null) Kill.It(_eventCatView); _eventCatView = null; }
            else
            {
                var parent = Parent(cat.ShopId);
                if (parent != null)
                {
                    if (_eventCatView == null)
                    {
                        _eventCatView = PetSceneModels.BuildAnimal(null,false,ArtDirection.Hex("B38557"),true,out var head,out var tail,out var legs);
                        _eventCatView.name = "TemporaryShopCat";
                        _eventCatView.AddComponent<PetCompanionMotion>().Bind(false,head,tail,legs);
                        var r = _eventCatView.transform;
                        _eventCatBowl = Prims.Shape(PrimitiveType.Cylinder, r, "WaterBowl", new Vector3(.55f, .08f, -.3f), new Vector3(.3f, .1f, .3f), Mats.Lit(ArtDirection.Blue));
                    }
                    _eventCatView.SetActive(true);
                    _eventCatView.GetComponent<PetCompanionMotion>().Playing = Session.Time.IsRunning;
                    if (_eventCatView.transform.parent != parent)
                    { _eventCatView.transform.SetParent(parent, false); _eventCatView.GetComponent<PetCompanionMotion>().ResetTravel(); }
                    _eventCatView.transform.localPosition = InInterior && Session.CurrentPlace?.Id == cat.ShopId ? new Vector3(.8f, 0, 1.2f) : new Vector3(1.2f, 0, -1);
                    _eventCatBowl.SetActive(cat.Water);
                }
                else if (_eventCatView != null) _eventCatView.SetActive(false);
            }
            var samples = Session.ShopEventTasks.Where(t => t.Kind == "sample" && t.Status != "在途").ToArray();
            foreach (string id in _eventSampleViews.Keys.Where(id => !samples.Any(t => t.Id == id)).ToArray())
            { if (_eventSampleViews[id] != null) Kill.It(_eventSampleViews[id]); _eventSampleViews.Remove(id); }
            foreach (var sample in samples)
            {
                var parent = Parent(sample.ShopId); if (parent == null) continue;
                if (!_eventSampleViews.TryGetValue(sample.Id, out GameObject box) || box == null)
                {
                    box = new GameObject("BorrowedSupplierSample"); _eventSampleViews[sample.Id] = box;
                    Prims.Box(box.transform, "SampleBox", new Vector3(0, .3f, 0), new Vector3(.8f, .6f, .45f), Mats.Lit(ArtDirection.Blue));
                    Prims.Box(box.transform, "SampleGpu", new Vector3(0, .65f, 0), new Vector3(.65f, .08f, .3f), Mats.Lit(ArtDirection.Sage));
                    Prims.SignText(box.transform, "试用样品 · 非卖品", new Vector3(0, 1.1f, -.25f), 1.8f, .28f, ArtDirection.Cream);
                }
                box.transform.SetParent(parent, false);
                box.transform.localPosition = InInterior && Session.CurrentPlace?.Id == sample.ShopId ? new Vector3(Interior.Width - 1, 0, 1.2f) : new Vector3(-1.5f, 0, -1);
            }
        }
    }
}
