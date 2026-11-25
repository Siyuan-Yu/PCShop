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

    public sealed partial class StreetWorldBuilder
    {
        private void QuarterVenueFront(Transform shell, Place place,float w,float d)
        {
            float h=CityArchitecture.VenueHeight(place);
            bool wholesale=place.Model=="warehouse";
            var shade=QuarterModules.Add(shell,wholesale?"TinCanopy":"StripedCanopy",new Vector3(0,2.98f,-.21f),
                new Vector3((w-.9f)/7.8f,1,1.15f));
            if(place.Business=="venue_bar" || place.Business=="venue_ktv")
                QuarterModules.Tint(shade,ArtDirection.Hex("BA9C91"));
            QuarterModules.Add(shell,"ServiceStack",new Vector3(-w*.46f,0,-.35f),new Vector3(1,h/3.05f,1));
            foreach(int side in new[]{-1,1})
            {
                QuarterModules.Add(shell,"Condenser",new Vector3(side*(w*.5f+.21f),3.15f,d*.39f),Vector3.one*1.1f,side<0?90:270);
                QuarterModules.Add(shell,"SideWindow",new Vector3(side*w*.5f,1.48f,d*.68f),new Vector3(1.45f,1,1),side<0?90:270);
            }
            if(wholesale) QuarterModules.Add(shell,"CargoPallet",new Vector3(w*.31f,.10f,-1.1f),Vector3.one);
            if(place.Business=="venue_restaurant") QuarterModules.Add(shell,"Crates",new Vector3(-w*.36f,.06f,-.94f),Vector3.one);
        }

        private void BuildQuarterMarket(Transform root, Place place, float width, float depth, float height)
        {
            var shell=QuarterModules.Add(root,"WallShell",Vector3.zero,new Vector3(width/8.7f,height/3.05f,depth/5.8f));
            QuarterModules.Tint(shell,ArtDirection.Hex("E4E0CB"));
            var collider=shell.gameObject.AddComponent<BoxCollider>();
            // Collider dimensions are local to the scaled shell.
            collider.center=new Vector3(0,1.525f,2.9f);
            collider.size=new Vector3(8.7f,3.05f,5.8f);
            shell.gameObject.AddComponent<PlaceMarker>().PlaceId=place.Id;
            var roof=QuarterModules.Add(root,"TileRoof",new Vector3(0,height,0),new Vector3(width/8.7f,1,depth/5.8f));
            QuarterModules.Tint(roof,ArtDirection.Hex("B4B5A5"));
            int bays=Mathf.Max(1,Mathf.RoundToInt(width/6));
            if(bays%2==0) bays--;
            float span=width/bays;
            for(int i=0;i<bays;i++)
            {
                float x=(i-(bays-1)*.5f)*span;
                bool central=i==bays/2;
                QuarterModules.Add(root,central?"DisplayFront":"CargoBay",new Vector3(x,0,0),
                    new Vector3(span/(central?8.7f:4.1f),1,1));
                QuarterModules.Add(root,central?"StripedCanopy":"TinCanopy",new Vector3(x,3.13f,0),new Vector3(span*.94f/7.8f,1,1));
                QuarterModules.Add(root,"SideWindow",new Vector3(x,height-.99f,0),new Vector3(1.5f,.52f,1));
                if(!central) QuarterModules.Add(root,"CargoPallet",new Vector3(x+span*.22f,.12f,-.71f),Vector3.one,9);
            }
            foreach(int side in new[]{-1,1})
            for(int j=0;j<3;j++)
                QuarterModules.Add(root,"SideWindow",new Vector3(side*width*.5f,2.4f,(j+.5f)*depth/3),new Vector3(1.65f,1,1),side<0?90:270);
            for(int j=0;j<3;j++) QuarterModules.Add(root,"Condenser",new Vector3(width*.32f,height+.15f,(j*.15f+.32f)*depth),Vector3.one*1.45f,90);
            var sign=StarterPart(root,"Market fascia",new Vector3(0,3.70f,-.14f),new Vector3(span,.74f,.18f),ArtDirection.Hex("596957"));
            Prims.SignText(root,place.Name,sign.transform.localPosition+Vector3.back*.12f,span*.93f,.58f,ArtDirection.Cream);
            var open=Prims.Box(root,"Open indicator",new Vector3(.78f,1.76f,-.28f),new Vector3(.18f,.095f,.04f),Mats.Unlit(UiGreen));
            var closed=Prims.Box(root,"Closed indicator",open.transform.localPosition,new Vector3(.18f,.095f,.04f),Mats.Lit(ArtDirection.Clay));
            _cur.OpenOnly.Add(open); _cur.ClosedOnly.Add(closed);
            CityArchitecture.StreetApron(root,width,true);
        }

        private void BuildQuarterShell(Transform root, Place place, float w, float d, float height,
            bool retail, int seed)
        {
            float ground = Mathf.Min(height, retail ? 3.35f : 3.02f);
            int floors=Mathf.Max(0,Mathf.RoundToInt((height-ground)/2.48f));
            if (floors==0) ground=height;
            var footprint = new Vector3(w/8.7f,ground/3.05f,d/5.8f);
            var shell = QuarterModules.Add(root,"WallShell",Vector3.zero,footprint);
            var limewash = (seed % 4) switch
            {
                1 => ArtDirection.Hex("E4ECDA"), 2 => ArtDirection.Hex("F2DFC8"), _ => Color.white
            };
            if (limewash != Color.white) QuarterModules.Tint(shell,limewash);
            MasonryPatina.Exterior(root,w,d,height,seed,true);
            int gableWindows=Mathf.Max(1,Mathf.FloorToInt(d/4));
            foreach (int side in new[]{-1,1})
            {
                var angle = side<0 ? 90 : 270;
                for(int j=0;j<gableWindows;j++)
                    QuarterModules.Add(root,"SideWindow",new Vector3(side*w*.5f,ground*.37f,(j+.5f)*d/gableWindows),
                        Vector3.one,angle);
            }
            // The rear elevation is visible across vegetable plots and from the lakeside.
            if(w>5.5f)
            {
                QuarterModules.Add(root,"ResidenceFront",new Vector3(0,0,d),new Vector3(w/8.7f,Mathf.Min(1.15f,ground/3.05f),.77f),180);
                QuarterModules.Add(root,"TinCanopy",new Vector3(0,2.45f,d+.045f),new Vector3(.33f,.70f,.70f),180);
                QuarterModules.Add(root,"Condenser",new Vector3(w*.38f,ground-.53f,d+.035f),Vector3.one*.80f,180);
                if(seed%3!=0) QuarterModules.Add(root,"PlantPot",new Vector3(-w*.37f,-.06f,d+.40f),Vector3.one*.87f);
            }
            var solid = new GameObject("Masonry shell");
            solid.transform.SetParent(root,false);
            var collider=solid.AddComponent<BoxCollider>();
            collider.center=new Vector3(0,height*.5f,d*.5f);
            collider.size=new Vector3(w,height,d);
            if(place!=null)solid.AddComponent<PlaceMarker>().PlaceId=place.Id;

            bool shutters = retail && place.Id!=StarterShop && seed%4==0;
            var frontScale=new Vector3(footprint.x,ground/3.05f,1);
            if(retail)
            {
                var openFront=QuarterModules.Add(root,"DisplayFront",Vector3.zero,frontScale);
                DistrictFinishes.ShopDisplay(openFront,place);
                var closedFront=QuarterModules.Add(root,"ShutterFront",Vector3.zero,frontScale);
                closedFront.gameObject.SetActive(false);
                _cur.OpenOnly.Add(openFront.gameObject); _cur.ClosedOnly.Add(closedFront.gameObject);
            }
            else QuarterModules.Add(root,"ResidenceFront",Vector3.zero,frontScale);
            float storey=floors>0 ? (height-ground)/floors : 0;
            for(int level=0;level<floors;level++)
            {
                float sill=ground+level*storey;
                for(int face=0;face<4;face++)
                {
                    float length=face%2==0?w:d;
                    var center=face switch {
                        0 => new Vector3(0,sill,0), 1 => new Vector3(-w/2,sill,d/2),
                        2 => new Vector3(0,sill,d), _ => new Vector3(w/2,sill,d/2)
                    };
                    int spans=Mathf.Max(1,Mathf.RoundToInt(length/8.7f));
                    var axis=Quaternion.Euler(0,face*90,0)*Vector3.right;
                    for(int bay=0;bay<spans;bay++)
                    {
                        var origin=center+axis*((bay+.5f)/spans-.5f)*length;
                        var upper = QuarterModules.Add(root,(face+level+bay+seed)%2==0?"Upper1":"Upper2",origin,
                            new Vector3(length/(spans*8.7f),storey/2.48f,1),face*90);
                        if (limewash != Color.white) QuarterModules.Tint(upper,limewash);
                    }
                }
                if ((level+seed)%2==0)
                    QuarterModules.Add(root,"BalconyRail",new Vector3(w*.31f,sill+.50f,0),Vector3.one);
            }
            // With a low parapet the roof footprint still reads as the footprint of the lease.
            bool pitched = (place==null && seed%3==0) || (retail && (place.Business=="修鞋" || place.Business=="花店"));
            if (pitched)
                StarterRoof(root,w,d,height,Mathf.Min(.96f,w*.10f),ArtDirection.Hex("A86E4E"),
                    ArtDirection.Hex("C8AD8C"),ArtDirection.Hex("91765C"),oldResidence:true,repairSeed:seed);
            else
            {
                var roof = QuarterModules.Add(root,"TileRoof",new Vector3(0,height,0),new Vector3(w/8.7f,1,d/5.8f));
                if (seed%3==1) QuarterModules.Tint(roof,ArtDirection.Hex("D6BDAD"));
                if (seed%3==2) QuarterModules.Tint(roof,ArtDirection.Hex("DDCBB0"));
                Prims.Box(root,"黏土屋面",new Vector3(0,height+.12f,d*.5f),new Vector3(w-.23f,.012f,d-.23f),
                    DistrictFinishes.Tile("quarterClay",new Vector2(Mathf.Max(1,w/2.45f),Mathf.Max(1,d/2.2f)),ArtDirection.Hex("C1C6BF")));
                if (floors==0 || seed%2==1) QuarterModules.Add(root,"Condenser",new Vector3(-w*.30f,height+.13f,d*.56f),Vector3.one);
                if (floors>0 && seed%3!=0)
                    QuarterModules.Add(root,"WaterTanks",new Vector3(w*.22f,height,d*.71f),Vector3.one);
            }
            QuarterModules.Add(root,"ServiceStack",new Vector3(w*.46f,0,-.012f),new Vector3(1,height/3.05f,1));
            if (w>7 && (!retail || place.Id==StarterShop || seed%2==0))
                QuarterModules.Add(root,"Cycle",new Vector3(-w*.5f-.13f,0,d*.39f),Vector3.one,0);

            float paving = retail ? .12f : .045f;
            QuarterModules.Add(root,"PlantPot",new Vector3(-w*.22f,paving-.10f,-.53f),Vector3.one);
            if (!retail)
            {
                if(seed%3!=0) QuarterModules.Add(root,"Bench",new Vector3(w*.33f,.055f,-.46f),Vector3.one);
                return;
            }
            // Keep the door and both main display windows visible under the canopy.
            if (w>=7 && (place.Id==StarterShop || seed%3==0) && OldTownTree!=null)
            {
                var corner=new Vector3(w*.5f-.05f,.127f,-2.20f);
                var pavers=OldTownSurfaces.Surface("paving",ArtDirection.Hex("AD9A80"),Vector2.one);
                Prims.Box(root,"Recessed tree pit",corner,new Vector3(1.16f,.020f,1.14f),pavers);
                Prims.Box(root,"Earth",corner+Vector3.up*.016f,new Vector3(.94f,.012f,.93f),Mats.Lit(ArtDirection.Hex("6A5944")));
                PaintedWoodland.Plant(root,corner,4,0,seed);
            }
            var trim=ArtDirection.Hex(shutters?"875B49":"526B59");
            float fasciaY=ground-.41f;
            StarterPart(root,"Painted fascia",new Vector3(0,fasciaY,-.15f),new Vector3(w*.92f,.49f,.17f),trim);
            foreach(float offset in new[]{-.23f,.23f})
                StarterPart(root,"Fascia timber bead",new Vector3(0,fasciaY+offset,-.244f),new Vector3(w*.92f,.031f,.031f),StarterStone);
            Prims.SignText(root,place.Name,new Vector3(0,fasciaY,-.26f),w*.86f,.40f,ArtDirection.Cream);
            var awning = QuarterModules.Add(root,shutters?"TinCanopy":"StripedCanopy",new Vector3(0,fasciaY-.26f,0),
                new Vector3(w*.91f/7.8f,1,1));
            if(!shutters && seed%3==1) QuarterModules.Tint(awning,ArtDirection.Hex("D5BC93"));
            if(!shutters && seed%3==2) QuarterModules.Tint(awning,ArtDirection.Hex("BBC9D1"));
            QuarterModules.Add(root,"Drain",new Vector3(w*.44f,paving+.005f,-2.99f),Vector3.one);
            var stock=place.Business;
            Vector3 outside=new Vector3(w*.29f,paving,-.72f);
            string display=stock switch
            {
                "五金" or "农贸" => "Crates", "奶茶" or "快餐" or "修鞋" => "Bench",
                "花店" => "PlantPot", "超市" or "杂货" or "面包" or "便利店" or "烟酒" => "StoreRack", _=>null
            };
            if(display!=null)
                _cur.OpenOnly.Add(QuarterModules.Add(root,display,outside,Vector3.one).gameObject);
            QuarterModules.Add(root,"PlantPot",new Vector3(-w*.39f,paving-.10f,-1.12f),Vector3.one*.84f);
            StarterWallLamp(root,new Vector3(-w*.27f,ground-.88f,-.60f));
            if(place.Id==StarterShop) QuarterModules.Add(root,"Chalkboard",new Vector3(w*.30f,paving,-1.08f),Vector3.one,-8);
            else if(seed%3==0) QuarterModules.Add(root,"Crates",new Vector3(-w*.35f,paving,-.53f),Vector3.one,5);
            var lit=StarterPart(root,"Open indicator",new Vector3(.56f,1.61f,-.29f),new Vector3(.15f,.09f,.045f),UiGreen);
            var shut=StarterPart(root,"Closed indicator",lit.transform.localPosition,new Vector3(.15f,.09f,.045f),ArtDirection.Clay);
            _cur.OpenOnly.Add(lit);
            _cur.ClosedOnly.Add(shut);
        }
    }
}
