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

        private static readonly Color[] PachinkoColors =
        {
            ArtDirection.Hex("DB976F"), ArtDirection.Hex("E7E8EA"),
            ArtDirection.Hex("E7C05D"), ArtDirection.Hex("C8A5EB")
        };

        public static Color PachinkoBeadColor(int tier) => PachinkoColors[tier];

        private static readonly Color[] PachinkoFelt =
        {
            ArtDirection.Hex("344F47"), ArtDirection.Hex("3F4C62"), ArtDirection.Hex("5A4145")
        };

        private static void DrawPachinko(VertexHelper mesh, Rect rect, PachinkoPlay play)
        {
            float left = rect.xMin + rect.width * .12f, width = rect.width * .76f;
            float bottom = rect.yMin + rect.height * .065f, height = rect.height * .88f;
            Vector2 P(double x, double y) => new Vector2(left + width * (float)x, bottom + height * (1 - (float)y));
            void Plate(double x, double y, double w, double h, Color shade)
                => Quad(mesh, P(x, y + h), P(x + w, y), shade);

            var board = play.Board;
            var brass = ArtDirection.Hex("C5A36D");
            var dark = ArtDirection.Hex("242B29");
            Plate(-.043, -.04, 1.086, 1.08, ArtDirection.Hex("302924"));
            Plate(-.017, -.02, 1.034, 1.04, ArtDirection.Hex("907658"));
            Plate(0, 0, 1, 1, PachinkoFelt[play.Machine]);
            Segment(mesh, P(.053, .071), P(.053, PachinkoBoard.SlotY), 4, brass);
            Segment(mesh, P(.947, .071), P(.947, PachinkoBoard.SlotY), 4, brass);
            Segment(mesh, P(.053, .071), P(.947, .071), 4, brass);

            foreach (var peg in board.Pegs)
            {
                var point = P(peg.X, peg.Y);
                Disc(mesh, point + Vector2.down * 1.5f, height * .010f, dark, 8);
                Disc(mesh, point, height * (float)PachinkoBoard.PegRadius, brass, 10);
            }
            for (int i = 0; i < board.BarCount; i++)
            {
                var bar = board.Bar(i, play.BoardTime);
                var from = P(bar.X1, bar.Y1);
                var to = P(bar.X2, bar.Y2);
                Segment(mesh, from + Vector2.down * 2, to + Vector2.down * 2, height * .022f, dark);
                Segment(mesh, from, to, height * .014f, ArtDirection.Hex("E5CA8E"));
            }

            var pivot = P(PachinkoBoard.OutletX, PachinkoBoard.OutletY);
            var tip = P(play.LaunchX, play.LaunchY);
            Segment(mesh, pivot, tip, height * .046f, dark);
            Segment(mesh, pivot, tip, height * .026f, brass);
            Disc(mesh, pivot, height * .028f, ArtDirection.Hex("8B724B"), 18);
            Disc(mesh, tip, height * .015f, PachinkoBeadColor(play.Tier), 12);

            Plate(.053, PachinkoBoard.SlotY, .894, .10, dark);
            for (int i = 0; i < board.Slots.Length; i++)
            {
                var slot = board.Slots[i];
                double edge = slot.Width * .5;
                double y = PachinkoBoard.SlotY;
                var glow = play.RewardFlash > 0 && play.LastSlot == i ? ArtDirection.Hex("FFF2BE") : brass;
                Plate(slot.X - edge, y - .021, slot.Width, .056, ArtDirection.Hex("172420"));
                Segment(mesh, P(slot.X - edge, y - .022), P(slot.X - edge, y + .035), 4, glow);
                Segment(mesh, P(slot.X + edge, y - .022), P(slot.X + edge, y + .035), 4, glow);
                Segment(mesh, P(slot.X - edge, y + .035), P(slot.X + edge, y + .035), 4, glow);
            }
            foreach (var ball in play.Balls)
            {
                var point = P(ball.X, ball.Y);
                Color color = PachinkoBeadColor(ball.Tier);
                Segment(mesh, P(ball.X - ball.VX * .037, ball.Y - ball.VY * .037), point, 3, color);
                Disc(mesh, point, height * (float)PachinkoPlay.BallRadius, color, 14);
                Disc(mesh, point + new Vector2(-2, 2), height * .004f, ArtDirection.Hex("FFF5DF"), 9);
            }
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
