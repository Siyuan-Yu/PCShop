using System;
using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    // District textures are albedos. The same sun, skylight and night cycle illuminate every district.
    internal static class DistrictFinishes
    {
        private static readonly Dictionary<(string, Vector2, Color), Material> Materials = new();
        internal static string Masonry(int district) => district == 1 ? "buffBrick" : district == 2 ? "ivoryPlaster" : "cutStone";
        internal static string Roof(int district) => district == 1 ? "slate" : "limestoneWalk";
        internal static Material Tile(string key, Vector2 repeat, Color? color = null)
        {
            Color tint = color ?? Color.white;
            var id = (key, repeat, tint);
            if (Materials.TryGetValue(id, out var result) && result != null) return result;
            var original = OldTownSurfaces.PanelMaterial(key);
            if (original == null) throw new InvalidOperationException("Missing city surface " + key);
            result = new Material(original) { name = "街区 / " + key };
            result.SetTextureScale("_BaseMap", repeat);
            if(key=="buffBrick") tint*=new Color(1.16f,1.23f,1.29f);
            if(key=="slate") tint*=new Color(1.04f,1.09f,1.16f);
            if(key=="streetMeadow") tint*=new Color(1.12f,1.47f,1.06f);
            if(key=="pearlCeramic" || key=="brushedAlloy" || key=="civicGlazing" || key=="solarCells")
            {
                result.SetFloat("_Smoothness",key=="brushedAlloy"?.46f:key=="pearlCeramic"?.31f:.60f);
                result.SetFloat("_Metallic",key=="brushedAlloy"?.28f:key=="solarCells"?.12f:.02f);
            }
            result.SetColor("_BaseColor", tint);
            Materials[id] = result;
            return result;
        }

        private static Texture2D _landUse;
        private static readonly Vector4 LandBounds=new Vector4(-1536,-1024,1f/3072,1f/3072);
        private static Texture2D LandUse=>_landUse!=null?_landUse:(_landUse=Resources.Load<Texture2D>("OldTown/Surfaces/districtGround"));
        internal static float GroveCover(Vector3 world)
        {
            float u=(world.x-LandBounds.x)*LandBounds.z,v=(world.z-LandBounds.y)*LandBounds.w;
            if(u<0 || u>1 || v<0 || v>1 || LandUse==null)return .5f;
            var sample=LandUse.GetPixelBilinear(u,v);
            return Mathf.Lerp(.5f,sample.b,sample.a);
        }
        private static Material _meadow;
        internal static Material Meadow()
        {
            if (_meadow==null)
            {
                _meadow=new Material(Resources.Load<Shader>("OldTown/Meadow")){name="草甸与裸土"};
                _meadow.SetTexture("_BaseMap",Resources.Load<Texture2D>("OldTown/Surfaces/livingMeadow"));
                _meadow.SetTexture("_Soil",Resources.Load<Texture2D>("OldTown/Surfaces/cultivatedSoil"));
                _meadow.SetTexture("_Variation",Resources.Load<Texture2D>("OldTown/Surfaces/meadowVariation"));
                _meadow.SetTexture("_LandUse",LandUse);_meadow.SetVector("_LandBounds",LandBounds);
                _meadow.SetTexture("_SoilPigment",Resources.Load<Texture2D>("OldTown/Surfaces/soilPigment"));
            }
            return _meadow;
        }

        private static Material _glass,_villaGlass;
        internal static Material WindowGlass(bool livingRoom=false)
        {
            if(livingRoom)
            {
                if(_villaGlass==null)
                {
                    _villaGlass=new Material(Tile("villaRooms",Vector2.one)){name="街区 / 窗内起居室"};
                    _villaGlass.EnableKeyword("_EMISSION");
                    _villaGlass.SetTexture("_EmissionMap",_villaGlass.GetTexture("_BaseMap"));
                }
                Mats.RestoreBakedGlow(_villaGlass,new Color(1.45f,.84f,.42f),.11f);
                return _villaGlass;
            }
            if (_glass == null)
            {
                _glass = new Material(Tile("celadonGlass",Vector2.one)) { name="街区 / 暖窗" };
                _glass.EnableKeyword("_EMISSION");
                _glass.SetTexture("_EmissionMap",_glass.GetTexture("_BaseMap"));
            }
            Mats.RestoreBakedGlow(_glass,new Color(1.30f,.68f,.29f),.06f);
            return _glass;
        }

        internal static void Apply(Transform root, int district)
        {
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (!source || source.shader.name != "Universal Render Pipeline/Lit" ||
                        source.IsKeywordEnabled("_EMISSION") || source.name.StartsWith("街区 /") || source.name.StartsWith("老城 /")) continue;
                    string part = renderer.name.ToLowerInvariant();
                    string tile = Classify(part, source, district);
                    if (tile == null) continue;
                    var repeat = source.mainTextureScale;
                    if (source.mainTexture == null && renderer.TryGetComponent<MeshFilter>(out var filter) &&
                        filter.sharedMesh != null && filter.sharedMesh.name == "Cube")
                    {
                        var size = renderer.transform.localScale;
                        repeat = new Vector2(Mathf.Max(1,Mathf.Round(Mathf.Max(size.x,size.z)/3)),
                            Mathf.Max(1,Mathf.Round((size.y < .5f ? Mathf.Min(size.x,size.z) : size.y)/3)));
                    }
                    // The new albedo already carries its local pigment. Keep the source palette on coloured fittings.
                    var tint = Color.Lerp(Color.white, source.GetColor("_BaseColor"), tile == "ivoryPlaster" ? .45f : .18f);
                    materials[i] = Tile(tile, repeat, tint);
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        internal static void ShopDisplay(Transform front,PCShopSell.Core.World.Place place)
        {
            string business=place.Business??"";
            string kind=business.Contains("花")?"Flowers":business.Contains("织")||business.Contains("裁缝")||business.Contains("服")?"Cloth":
                business.Contains("烘")||business.Contains("面包")?"Bread":business.Contains("书")||business.Contains("文具")?"Books":
                business.Contains("手作")||business.Contains("模型")||business.Contains("画材")||business.Contains("潮玩")?"Models":
                business.Contains("家饰")||business.Contains("陶")||business.Contains("杂货")?"Ceramics":null;
            if(business=="裁缝")kind="Tailor";
            if(kind==null)return;
            for(int side=0;side<2;side++)
                OldTownSurfaces.Panel(front,"display"+kind+side,new Vector3(side==0?-2.78f:2.78f,1.28f,-.198f),new Vector2(2.27f,1.59f));
        }

        private static string Classify(string part, Material source, int district)
        {
            if (source.name.StartsWith("Industrial / "))
                return source.name.Substring(13) switch {
                    "brick" => Masonry(district), "concrete" or "old_plaster" => "ivoryPlaster",
                    "meadow" => "streetMeadow", "paving" => "graniteWalk", "asphalt" => "streetAsphalt",
                    "metal" => part.Contains("roof") ? Roof(district) : "steel", "claytiles" => Roof(district),
                    "wood" => "walnut", _ => null
                };
            if (source.mainTexture != null) return null;
            if (part.Contains("glass") && !part.Contains("balustrade")) return "celadonGlass";
            if (part.Contains("wood") || part.Contains("timber") || part.Contains("pergola") || part.Contains("counter") ||
                part.Contains("shelf") || part.Contains("deck") || part.Contains("slat") && !part.Contains("door")) return "walnut";
            if (part.Contains("canopy") && !part.Contains("tree") || part.Contains("awning")) return "sageCloth";
            if (part.Contains("roof") && !part.Contains("planter") && !part.Contains("solar")) return Roof(district);
            if (part.Contains("pav") || part.Contains("coping") || part.Contains("steppingstone")) return "graniteWalk";
            if (part.Contains("stone") || part.Contains("boundary") || part.Contains("foundation") || part.Contains("pier")) return "cutStone";
            if (part.Contains("storey") || part == "groundfloor" || part == "hallside" || part == "hallback" ||
                part.Contains("plaster") || part.Contains("office") || part == "wallback" || part == "wallright") return "ivoryPlaster";
            return null;
        }
    }
}
