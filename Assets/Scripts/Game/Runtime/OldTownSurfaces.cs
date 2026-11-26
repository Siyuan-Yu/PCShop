using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    internal static class OldTownSurfaces
    {
        private static readonly Dictionary<(Material, string), Material> Paint = new();
        private static readonly Dictionary<string, Texture2D> Tiles = new();
        private static readonly Color WarmPaper = ArtDirection.Hex("C7B194");

        public static Material Surface(string tile, Color color, Vector2 repeat)
        {
            var material = Painted(Mats.Lit(color), tile);
            if (repeat == Vector2.one) return material;
            return Painted(IndustrialMaterials.Surface(tile, color, repeat), tile);
        }

        public static void Apply(Transform root)
        {
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (source == null || source.shader.name != "Universal Render Pipeline/Lit" ||
                        source.IsKeywordEnabled("_EMISSION") || (source.name.StartsWith("老城 /") || source.name.StartsWith("街区 /"))) continue;
                    string tile = TileFor(renderer.name, source);
                    if (tile == null) continue;
                    materials[i] = Painted(source, tile);
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        private static readonly Dictionary<string, Material> Panels = new();
        public static void Panel(Transform root, string key, Vector3 center, Vector2 size)
        {
            var mat = PanelMaterial(key);
            if(mat==null) return;
            Prims.Box(root, key, center, new Vector3(size.x, size.y, .018f), mat);
        }

        internal static Material PanelMaterial(string key)
        {
            if (!Panels.TryGetValue(key, out var material) || material == null)
            {
                var texture = Resources.Load<Texture2D>("OldTown/Surfaces/" + key);
                if (texture == null) return null;
                bool paving=key=="streetAsphalt" || key=="graniteWalk" || key=="districtFlagstones" || key=="streetPavers";
                var shader=paving?Resources.Load<Shader>("OldTown/PavedSurface"):Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader) { name = "老城 / " + key, enableInstancing = true };
                if(paving)
                {
                    material.SetTexture("_Variation",Resources.Load<Texture2D>("OldTown/Surfaces/meadowVariation"));
                    material.SetFloat("_Asphalt",key=="streetAsphalt"?1:0);
                }
                material.SetTexture("_BaseMap", texture);
                material.SetFloat("_Smoothness", key=="slate"?.25f:key=="walnut"?.22f:.13f);
                bool relief=key=="buffBrick" || key=="cutStone" || key=="ivoryPlaster" || key=="slate" || key=="parkSlate" || key=="quarterClay" || key=="walnut";
                if(relief)
                {
                    material.SetTexture("_BumpMap",Resources.Load<Texture2D>("OldTown/Surfaces/"+(key=="parkSlate"?"slate":key)+"Relief"));
                    material.SetFloat("_BumpScale",key=="quarterClay"?.80f:.64f);
                    material.EnableKeyword("_NORMALMAP");
                }
                if (key == "windowStock" || key == "laceGlass" || key.StartsWith("display",System.StringComparison.Ordinal))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetTexture("_EmissionMap", texture);
                }
                float roomGloss=key switch {
                    "officeCarpet" => .09f, "terraTile" => .21f, "creamTile" => .29f,
                    "oakParquet" => .31f, "interiorPorcelain" => .57f, "officeSlate" => .41f,
                    "suburbPlaster" => .09f, "suburbEnamel" => .20f, "suburbCement" => .10f, "suburbShopTile" => .23f,
                    "wornLimewash" => .10f, "roomLimewash" => .12f, _ => -1f };
                if (roomGloss>=0)
                {
                    material.SetFloat("_Smoothness",roomGloss);
                    material.SetTexture("_BumpMap",Resources.Load<Texture2D>("OldTown/Surfaces/"+key+"Relief"));
                    material.SetFloat("_BumpScale",.68f);
                    material.EnableKeyword("_NORMALMAP");
                }
                Panels[key] = material;
            }
            if (material.IsKeywordEnabled("_EMISSION")) Mats.RestoreBakedGlow(material, new Color(.80f,.58f,.36f), .12f);
            return material;
        }

        private static readonly Dictionary<(string,Vector2),Material> GroundTiles = new();
        internal static Material Ground(string pigment, Vector2 size)
        {
            var key=(pigment,size);
            if(GroundTiles.TryGetValue(key,out var value) && value!=null) return value;
            var original=PanelMaterial(pigment);
            var material=new Material(original) {name=original.name};
            material.SetTextureScale("_BaseMap",size/4);
            GroundTiles[key]=material;
            return material;
        }

        private static readonly Material[] Ceramics=new Material[16];
        internal static Material FloorTile(int x,int z)
        {
            int i=(x+z*3)%Ceramics.Length;
            if(Ceramics[i]!=null) return Ceramics[i];
            var original=PanelMaterial("shopCeramic");
            var material=new Material(original) { name=original.name+" "+i };
            // Select a different ceramic square, keeping grout in the physical tile gap.
            material.SetTextureScale("_BaseMap",Vector2.one*.239f);
            material.SetTextureOffset("_BaseMap",new Vector2(i%4,i/4)*.25f+Vector2.one*.0055f);
            Ceramics[i]=material;
            return material;
        }

        private static string TileFor(string objectName, Material source)
        {
            if (source.name.StartsWith("Industrial / "))
            {
                return source.name.Substring(13) switch
                {
                    "old_plaster" => "plaster", "claytiles" => "roof", "metal" => "steel", "concrete" => "plaster",
                    "meadow" => "grass", "asphalt" => "asphalt", "paving" => "paving",
                    "brick" => "brick", "wood" => "wood", _ => null
                };
            }
            if (source.mainTexture != null) return null;
            string part = objectName.ToLowerInvariant();
            if (part.Contains("glass") || part.Contains("screen")) return null;
            if (part.Contains("roof") || part.Contains("shingle")) return "roof";
            if (part.Contains("awning") || part.Contains("canvas")) return "canvas";
            if (part.Contains("pav") || part.Contains("tile") || part.Contains("curb")) return "paving";
            if (part.Contains("wood") || part.Contains("counter") || part.Contains("shelf") || part.Contains("bench") ||
                part.Contains("timber") || part.Contains("door") || part.Contains("desk")) return "wood";
            return source.GetFloat("_Metallic") > .22f ? "steel" : "plaster";
        }

        private static Material Painted(Material source, string tile)
        {
            if (Paint.TryGetValue((source, tile), out var cached) && cached != null) return cached;
            if (!Tiles.TryGetValue(tile, out var texture)) Tiles[tile] = texture = Resources.Load<Texture2D>("OldTown/Surfaces/" + tile);
            if (texture == null) return source;
            var material = new Material(source) { name = "老城 / " + tile, enableInstancing = true };
            var tint = source.GetColor("_BaseColor");
            float gray = tint.grayscale;
            tint = Color.Lerp(tint, new Color(gray * 1.08f, gray, gray * .90f), .35f);
            tint = Color.Lerp(tint, WarmPaper, .11f);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", .12f);
            material.SetFloat("_Metallic", 0);
            material.DisableKeyword("_NORMALMAP");
            Paint[(source, tile)] = material;
            return material;
        }
    }
}
