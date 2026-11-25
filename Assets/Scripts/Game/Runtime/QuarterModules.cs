using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    // Shared atlas meshes use metres, a pavement-level origin, and a facade facing -Z.
    internal static class QuarterModules
    {
        private static GameObject _library;
        private static GameObject _tree;
        internal static Transform Tree(Transform parent,Vector3 at,float scale,int heading)
        {
            if(_tree==null) _tree=Resources.Load<GameObject>("OldTown/StreetTree");
            var tree=Object.Instantiate(_tree,parent,false).transform;
            tree.localPosition=at;tree.localRotation=Quaternion.Euler(0,heading,0);tree.localScale=Vector3.one*scale;return tree;
        }

        private static readonly System.Collections.Generic.Dictionary<(Material, Color), Material> Tints = new();

        public static void Tint(Transform part, Color color)
        {
            var renderer = part.GetComponent<MeshRenderer>();
            var original = renderer.sharedMaterial;
            if (!Tints.TryGetValue((original, color), out var material) || material == null)
            {
                material = new Material(original) { name = original.name + " / " + ColorUtility.ToHtmlStringRGB(color) };
                material.SetColor("_Pigment",color);
                Tints[(original,color)] = material;
            }
            renderer.sharedMaterial = material;
        }

        public static Transform Add(Transform parent, string part, Vector3 at, Vector3 scale, float yaw = 0)
        {
            if (_library == null) _library = Resources.Load<GameObject>("OldTown/QuarterModules");
            var template = _library.transform.Find(part);
            var instance = Object.Instantiate(template, parent, false);
            instance.localPosition = at;
            instance.localRotation = Quaternion.Euler(0,yaw,0);
            instance.localScale = scale;
            return instance;
        }
    }

}
