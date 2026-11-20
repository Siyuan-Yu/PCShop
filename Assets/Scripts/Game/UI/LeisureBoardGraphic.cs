using System;
using PCShopSell.Core.Session;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    internal sealed class LeisureBoardGraphic : MaskableGraphic
    {
        public PachinkoPlay Pachinko;
        public BilliardsPlay Billiards;
        private const float PoolLeft = .076f, PoolBottom = .14f, PoolWidth = .848f, PoolHeight = .72f;
        private static readonly Color[] PoolColors = {
            ArtDirection.Hex("F4EDDA"), ArtDirection.Hex("D6A554"), ArtDirection.Hex("748EAB"),
            ArtDirection.Hex("B15D49"), ArtDirection.Hex("7D6289"), ArtDirection.Hex("607C4C")
        };

        public static (double X, double Y) ToTable(Vector2 normalized) =>
            ((normalized.x - PoolLeft) / PoolWidth * BilliardsPlay.Width, (normalized.y - PoolBottom) / PoolHeight);

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            if (Pachinko != null) DrawPachinko(mesh, rect, Pachinko);
            if (Billiards != null) DrawBilliards(mesh, rect, Billiards);
        }

        private static void DrawPachinko(VertexHelper mesh, Rect rect, PachinkoPlay play)
        {
            float left = rect.xMin + rect.width * .12f, width = rect.width * .76f;
            float bottom = rect.yMin + rect.height * .065f, height = rect.height * .88f;
            Vector2 P(double x, double y) => new Vector2(left + width * (float)x, bottom + height * (1 - (float)y));
            void Plate(float x, float y, float w, float h, string shade)
                => Quad(mesh, P(x,y+h), P(x+w,y), ArtDirection.Hex(shade));
            Plate(-.043f,-.045f,1.086f,1.11f,"39392C");
            Plate(-.017f,-.022f,1.034f,1.064f,"907658");
            Plate(0,0,1,1,"294137");
            Plate(.035f,.074f,.93f,.88f,"385849");
            var brass = ArtDirection.Hex("BB975F");
            var path = new[] { P(.965,.95), P(.965,.27), P(.92,.154), P(.84,.09), P(.75,.055), P(.24,.055), P(.12,.10), P(.05,.22), P(.05,.95) };
            for (int i = 1; i < path.Length; i++) Segment(mesh, path[i - 1], path[i], 3, brass);
            for (int row = 0; row < 10; row++)
            for (int col = 0; col < 10; col++)
            {
                var pin = P(PachinkoPlay.PegX(row,col), PachinkoPlay.PegY(row));
                Disc(mesh, pin + Vector2.down * 1.5f, height * .010f, ArtDirection.Hex("1F372D"), 8);
                Disc(mesh, pin, height * .0076f, brass, 10);
            }
            var gate = play.RewardFlash > 0 ? ArtDirection.Hex("FBE3A1") : ArtDirection.Hex("C6A166");
            float gateWidth = (float)PachinkoPlay.RewardWidth;
            Quad(mesh, P(.5f - gateWidth * .5f,.958f), P(.5f + gateWidth * .5f,.925f), gate);
            Segment(mesh,P(.36,.898),P(.46,.933),3,brass);
            Segment(mesh,P(.64,.898),P(.54,.933),3,brass);
            foreach (var ball in play.Balls)
            {
                var point = P(ball.X,ball.Y);
                Segment(mesh, P(ball.X-ball.VX*.043,ball.Y-ball.VY*.043), point, 3, ArtDirection.Hex("C5CEBA"));
                Disc(mesh, point, height * .0155f, ArtDirection.Hex("CED5C9"), 14);
                Disc(mesh, point + new Vector2(-2,2), height * .006f, ArtDirection.Hex("FFF2CE"), 9);
            }
            Segment(mesh,P(.07,.988),P(.93,.988),4,brass);
        }

        private static void DrawBilliards(VertexHelper mesh, Rect rect, BilliardsPlay pool)
        {
            Vector2 P(double x,double y) => new Vector2(rect.xMin + rect.width * (PoolLeft + PoolWidth * (float)x / 2),
                rect.yMin + rect.height * (PoolBottom + PoolHeight * (float)y));
            Quad(mesh,P(-.16,-.14),P(2.16,1.12),ArtDirection.Hex("34291F"));
            Quad(mesh,P(-.14,-.11),P(2.14,1.11),ArtDirection.Hex("825D3F"));
            Quad(mesh,P(-.035,-.037),P(2.035,1.037),ArtDirection.Hex("567F62"));
            Quad(mesh,P(0,0),P(2,1),ArtDirection.Hex("3C6650"));
            float scale = rect.height * PoolHeight;
            for(int i=1;i<8;i++)
            {
                Disc(mesh,P(i*.25,-.07),3,ArtDirection.Cream,8);
                Disc(mesh,P(i*.25,1.07),3,ArtDirection.Cream,8);
            }
            foreach(var pocket in BilliardsPlay.Pockets)
            {
                Disc(mesh,P(pocket.X,pocket.Y),scale*.061f,ArtDirection.Hex("294438"),22);
                Disc(mesh,P(pocket.X,pocket.Y),scale*.046f,ArtDirection.Hex("182421"),20);
            }
            var cue = pool.Balls[0];
            if (!cue.Pocketed && !pool.Rolling && !pool.OpponentTurn && !pool.Finished)
            {
                Vector2 origin = P(cue.X, cue.Y);
                Vector2 direction = (P(pool.AimX,pool.AimY)-origin).normalized;
                if (direction.sqrMagnitude > .01f)
                {
                    float radius = scale * (float)BilliardsPlay.Radius;
                    float guide = LimitGuide(rect, pool, P, origin, direction, radius);
                    for (float d = radius + 4; d < guide; d += 18)
                        Segment(mesh,origin + direction * d,origin + direction * Mathf.Min(guide,d+9),2,ArtDirection.Hex("F0D08A"));
                    float offset = radius + 8 + (float)pool.Power*32;
                    Segment(mesh,origin-direction*(offset+138),origin-direction*offset,7,ArtDirection.Hex("CBA36B"));
                    Segment(mesh,origin-direction*(offset+15),origin-direction*offset,6,ArtDirection.Cream);
                }
            }
            for (int i = 0; i < pool.Balls.Length; i++)
            {
                var ball = pool.Balls[i];
                if (ball.Pocketed) continue;
                Vector2 point = P(ball.X,ball.Y);
                float radius = scale * (float)BilliardsPlay.Radius;
                Disc(mesh,point+new Vector2(2,-3),radius,ArtDirection.Hex("293D32"),20);
                Disc(mesh,point,radius,PoolColors[i % PoolColors.Length],20);
                Disc(mesh,point+new Vector2(-radius*.25f,radius*.35f),radius*.24f,ArtDirection.Hex("F4E8C7"),10);
            }
        }

        private static float LimitGuide(Rect rect, BilliardsPlay pool, Func<double,double,Vector2> point,
            Vector2 origin, Vector2 axis, float radius)
        {
            float bound = (axis.x > 0 ? point(2,0).x - origin.x : point(0,0).x - origin.x) / (Mathf.Abs(axis.x) < 1e-4f ? .0001f : axis.x);
            float top = (axis.y > 0 ? point(0,1).y - origin.y : point(0,0).y - origin.y) / (Mathf.Abs(axis.y) < 1e-4f ? .0001f : axis.y);
            float distance = Mathf.Min(rect.width*.57f, Mathf.Abs(bound), Mathf.Abs(top));
            if (Mathf.Abs(axis.x) < 1e-4f) distance = Mathf.Min(rect.width*.57f, Mathf.Abs(top));
            if (Mathf.Abs(axis.y) < 1e-4f) distance = Mathf.Min(rect.width*.57f, Mathf.Abs(bound));
            for(int i=1;i<pool.Balls.Length;i++)
            {
                var ball = pool.Balls[i];
                if (ball.Pocketed) continue;
                Vector2 diff = point(ball.X,ball.Y) - origin;
                float along = Vector2.Dot(diff,axis);
                float side = diff.sqrMagnitude - along*along;
                if (along <= 0 || side >= radius*radius*4) continue;
                distance = Mathf.Min(distance,along-Mathf.Sqrt(Mathf.Max(0,radius*radius*4-side)));
            }
            return Mathf.Max(0,distance);
        }

        private static void Quad(VertexHelper mesh, Vector2 lower, Vector2 upper, Color tint)
        {
            int i = mesh.currentVertCount;
            mesh.AddVert(lower,tint,Vector2.zero);
            mesh.AddVert(new Vector2(upper.x,lower.y),tint,Vector2.zero);
            mesh.AddVert(upper,tint,Vector2.zero);
            mesh.AddVert(new Vector2(lower.x,upper.y),tint,Vector2.zero);
            mesh.AddTriangle(i,i+2,i+1);
            mesh.AddTriangle(i,i+3,i+2);
        }

        private static void Segment(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 edge = b - a;
            Vector2 normal = new Vector2(-edge.y,edge.x).normalized * (width*.5f);
            int i=mesh.currentVertCount;
            mesh.AddVert(a-normal,tint,Vector2.zero);
            mesh.AddVert(b-normal,tint,Vector2.zero);
            mesh.AddVert(b+normal,tint,Vector2.zero);
            mesh.AddVert(a+normal,tint,Vector2.zero);
            mesh.AddTriangle(i,i+2,i+1);
            mesh.AddTriangle(i,i+3,i+2);
        }

        private static void Disc(VertexHelper mesh, Vector2 center, float radius, Color tint, int segments)
        {
            int start=mesh.currentVertCount;
            mesh.AddVert(center,tint,Vector2.zero);
            for(int i=0;i<segments;i++)
            {
                float angle=i*Mathf.PI*2/segments;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,tint,Vector2.zero);
            }
            for(int i=0;i<segments;i++) mesh.AddTriangle(start,start+1+(i+1)%segments,start+i+1);
        }
    }
}
