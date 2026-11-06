using System;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class QuarterlyExpoView
    {
        private RawImage _screenHost, _screenProduct;
        private Text _screenHostLabel, _screenProductLabel;
        private string _screenArtKey;
        public string ScreenPersonId { get; private set; }
        public string ScreenProductId { get; private set; }

        private static RawImage WorldArtwork(Transform parent, string name, Vector3 at, float width, float height)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = at;
            root.transform.localScale = Vector3.one * .01f;
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(width * 100, height * 100);
            return Ui.Rect(rect, "Image").Fill().gameObject.AddComponent<RawImage>();
        }

        private void BuildScreenArtwork(float width)
        {
            _screenArtKey = null;
            _screenHost = WorldArtwork(_stage, "ExpoScreenHost", new Vector3(-width * .33f, 2.9f, 2.08f), 2.9f, 3.7f);
            _screenProduct = WorldArtwork(_stage, "ExpoScreenProduct", new Vector3(width * .33f, 2.9f, 2.08f), 3.7f, 3.7f);
            _screenHostLabel = Label(_stage, "", new Vector3(-width * .33f, .85f, 2.05f), 3.6f, .35f, ArtDirection.Cream);
            _screenProductLabel = Label(_stage, "", new Vector3(width * .33f, .85f, 2.05f), 3.8f, .35f, ArtDirection.Cream);
        }

        private void RefreshScreenArtwork(BrandExhibit exhibit)
        {
            // No invented host: only booked people who have physically arrived at this venue.
            var present = (exhibit?.KolIds ?? Array.Empty<string>()).Where(_session.ExpoPersonArrived).ToArray();
            int turn = Math.Max(0, exhibit?.Decision ?? 0);
            string person = present.Length == 0 ? null : present[turn % present.Length];
            string product = exhibit?.Products?.Length > 0 ? exhibit.Products[turn % exhibit.Products.Length].ProductId : null;
            string key = person + ":" + product;
            if (_screenArtKey == key) return;
            _screenArtKey = key; ScreenPersonId = person; ScreenProductId = product;
            BrandShowcaseArt.ApplyHost(_screenHost, person);
            BrandShowcaseArt.ApplyProduct(_screenProduct, product);
            _screenHostLabel.text = person == null ? "" : _session.CityPerson(person).Name;
            _screenProductLabel.text = product == null ? "" : _session.BrandProductName(product);
        }
    }
}
