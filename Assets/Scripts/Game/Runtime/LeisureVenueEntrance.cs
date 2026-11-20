using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    internal static class LeisureVenueEntrance
    {
        public static void Build(Transform root, Place place, float width, float depth)
        {
            var type = CityLifeCatalog.LeisureAt(place.Id);
            var wood = Mats.Lit(ArtDirection.Wood);
            var dark = Mats.Lit(ArtDirection.Ink);
            var cream = Mats.Lit(ArtDirection.Cream);
            var felt = Mats.Lit(ArtDirection.Hex("42795D"));
            Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
            void Box(string name, Vector3 at, Vector3 size, Material mat) => Prims.Box(root, name, at, size, mat);
            float back = depth - 2;
            switch (type)
            {
                case LeisureGame.Pachinko:
                    for (int i = -1; i <= 1; i++)
                    {
                        float x = i * 2.6f;
                        Box("PachinkoCabinet", V(x,1.35f,back), V(2.2f,2.7f,.82f), wood);
                        Box("PegBoard", V(x,1.82f,back-.43f), V(1.89f,1.46f,.08f), felt);
                        Box("BallTray", V(x,.85f,back-.57f), V(1.84f,.12f,.28f), dark);
                    }
                    break;
                case LeisureGame.Poker:
                    for (int i = 0; i < 5; i++)
                        Box("BasementStep", V(0,.12f*(i+1),back-2+i*.42f),V(2.8f,.24f*(i+1),.42f),dark);
                    Box("BasementDoor", V(0,2.10f,back+.11f),V(2.55f,2.18f,.18f),wood);
                    Prims.SignText(root,"地下室入口",V(0,3.45f,back-.05f),3f,.35f,ArtDirection.Cream);
                    break;
                case LeisureGame.Claw:
                    for (int i = -1; i <= 1; i += 2)
                    {
                        float x = i * 1.65f;
                        Box("ClawBase", V(x,.43f,back), V(2.65f,.86f,1.56f), felt);
                        Box("ClawRoof", V(x,2.85f,back), V(2.65f,.29f,1.56f), felt);
                        Box("ClawBack", V(x,1.86f,back+.74f), V(2.65f,2.0f,.09f),dark);
                        for (int side = -1; side <= 1; side += 2)
                            Box("WindowEdge",V(x+side*1.25f,1.82f,back-.72f),V(.10f,1.92f,.09f),cream);
                    }
                    break;
                case LeisureGame.Billiards:
                    foreach (float x in new[] {-3.2f,3.2f})
                    {
                        Box("PoolPedestal", V(x,.45f,back-3), V(2.0f,.90f,2.05f), wood);
                        Box("PoolTable", V(x,.98f,back-3), V(4.92f,.18f,2.70f), wood);
                        Box("PoolFelt", V(x,1.08f,back-3), V(4.57f,.04f,2.36f), felt);
                    }
                    break;
                case LeisureGame.Cinema:
                    Box("CinemaCounter", V(width*.25f,.55f,back-2.9f), V(3.7f,1.10f,1.2f),wood);
                    Box("Screen", V(-1.7f,2.1f,depth-.14f), V(7.0f,2.6f,.08f),cream);
                    var seat = Mats.Lit(ArtDirection.Hex("855665"));
                    for(int row=0;row<3;row++)
                    for(int col=0;col<6;col++)
                    {
                        float x=-4.9f+col*1.33f,z=back-5+row*1.5f;
                        Box("CinemaSeat",V(x,.46f,z),V(.83f,.24f,.72f),seat);
                        Box("CinemaSeatBack",V(x,.76f,z-.35f),V(.83f,.84f,.15f),seat);
                    }
                    break;
            }
        }
    }
}
