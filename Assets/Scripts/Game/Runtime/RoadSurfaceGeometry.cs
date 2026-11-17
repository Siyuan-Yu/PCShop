using System;
using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    // Convex planar difference. Each accepted surface owns its area exactly once.
    public static class RoadSurfaceGeometry
    {
        public static float Area(IReadOnlyList<Vector2> p)
        {
            if(p.Count<3)return 0;
            // Translate first: avoid loss of precision for small polygons far from the origin.
            double area=0;var origin=p[0];
            for(int i=1;i+1<p.Count;i++)area+=Cross(p[i]-origin,p[i+1]-origin);
            return (float)Math.Abs(area*.5);
        }
        static double Cross(Vector2 a,Vector2 b)=>(double)a.x*b.y-(double)a.y*b.x;
        public static Vector2[] Strip(Vector2 a,Vector2 b,float width,float cap=0)
        {
            var d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*(width*.5f);
            return new[]{a-d*cap-n,b+d*cap-n,b+d*cap+n,a-d*cap+n};
        }
        public static Vector2[] Road(Vector2 a,Vector2 b,float width)
        {
            var p=new List<Vector2>();float angle=Mathf.Atan2(b.y-a.y,b.x-a.x),r=width*.5f;
            for(int i=0;i<=6;i++){float t=angle-Mathf.PI*.5f+i*Mathf.PI/6;p.Add(b+new Vector2(Mathf.Cos(t),Mathf.Sin(t))*r);}
            for(int i=0;i<=6;i++){float t=angle+Mathf.PI*.5f+i*Mathf.PI/6;p.Add(a+new Vector2(Mathf.Cos(t),Mathf.Sin(t))*r);}
            return p.ToArray();
        }
        static List<Vector2> Half(IReadOnlyList<Vector2> p,Vector2 a,Vector2 b,bool inside)
        {
            var result=new List<Vector2>();if(p.Count==0)return result;
            var previous=p[p.Count-1];double prev=Cross(b-a,previous-a);
            for(int i=0;i<p.Count;i++)
            {
                var current=p[i];double value=Cross(b-a,current-a);
                bool was=inside?prev>=0:prev<=0,keep=inside?value>=0:value<=0;
                if(keep!=was)
                {
                    double t=prev/(prev-value);
                    result.Add(new Vector2((float)(previous.x+(current.x-previous.x)*t),(float)(previous.y+(current.y-previous.y)*t)));
                }
                if(keep)result.Add(current);previous=current;prev=value;
            }
            return result;
        }
        internal static Vector2[] Intersect(IReadOnlyList<Vector2> subject, IReadOnlyList<Vector2> clip)
        {
            IReadOnlyList<Vector2> remaining = subject;
            for (int i = 0; i < clip.Count && remaining.Count >= 3; i++)
                remaining = Half(remaining, clip[i], clip[(i + 1) % clip.Count], true);
            return remaining.Count >= 3 ? new List<Vector2>(remaining).ToArray() : Array.Empty<Vector2>();
        }

        internal static List<Vector2[]> SplitAt(List<Vector2[]> pieces, Vector2 point, Vector2 line)
        {
            var result = new List<Vector2[]>();
            foreach (var piece in pieces)
            {
                var left = Half(piece, point, point + line, true);
                var right = Half(piece, point, point + line, false);
                if (Area(left) > .0001f) result.Add(left.ToArray());
                if (Area(right) > .0001f) result.Add(right.ToArray());
            }
            return result;
        }

        internal static bool ClipSegment(Vector2 a, Vector2 b, IReadOnlyList<Vector2> polygon, out float first, out float last)
        {
            first = 0;
            last = 1;
            for (int i = 0; i < polygon.Count; i++)
            {
                var start = polygon[i];
                var edge = polygon[(i + 1) % polygon.Count] - start;
                double begin = Cross(edge, a - start), end = Cross(edge, b - start);
                if (begin < 0 && end < 0) return false;
                if (begin < 0) first = Mathf.Max(first, (float)(begin / (begin - end)));
                if (end < 0) last = Mathf.Min(last, (float)(begin / (begin - end)));
                if (first >= last) return false;
            }
            return true;
        }
        public static List<Vector2[]> Subtract(IReadOnlyList<Vector2> subject,IReadOnlyList<Vector2> clip)
        {
            var pieces=new List<Vector2[]>();IReadOnlyList<Vector2> remaining=subject;
            for(int i=0;i<clip.Count&&remaining.Count>=3;i++)
            {
                var a=clip[i];var b=clip[(i+1)%clip.Count];
                var outside=Half(remaining,a,b,false);
                if(Area(outside)>.0001f)pieces.Add(outside.ToArray());
                remaining=Half(remaining,a,b,true);
            }
            return pieces;
        }
        public sealed class Index
        {
            const float Cell=32;
            readonly Dictionary<Vector2Int,List<int>> cells=new Dictionary<Vector2Int,List<int>>();
            public readonly List<Vector2[]> Polygons=new List<Vector2[]>();
            static Rect Bounds(Vector2[] p)
            {var min=p[0];var max=min;foreach(var v in p){min=Vector2.Min(min,v);max=Vector2.Max(max,v);}return Rect.MinMaxRect(min.x,min.y,max.x,max.y);}
            static IEnumerable<Vector2Int> Keys(Rect r)
            {for(int x=Mathf.FloorToInt(r.xMin/Cell);x<=Mathf.FloorToInt(r.xMax/Cell);x++)for(int y=Mathf.FloorToInt(r.yMin/Cell);y<=Mathf.FloorToInt(r.yMax/Cell);y++)yield return new Vector2Int(x,y);}
            public void Add(Vector2[] polygon)
            {
                int id=Polygons.Count;Polygons.Add(polygon);
                foreach(var key in Keys(Bounds(polygon))){if(!cells.TryGetValue(key,out var list))cells[key]=list=new List<int>();list.Add(id);}
            }
            public IEnumerable<int> Nearby(Vector2[] polygon)
            {
                var bounds=Bounds(polygon);var ids=new HashSet<int>();
                foreach(var key in Keys(bounds))if(cells.TryGetValue(key,out var list))foreach(int id in list)ids.Add(id);
                var ordered=new List<int>(ids);ordered.Sort();
                foreach(int id in ordered)if(bounds.Overlaps(Bounds(Polygons[id])))yield return id;
            }
            public List<Vector2[]> Cut(List<Vector2[]> pieces,Vector2[] original,Func<int,bool> include=null)
            {
                foreach(int id in Nearby(original))
                {
                    if(include!=null&&!include(id))continue;
                    var next=new List<Vector2[]>();foreach(var p in pieces)next.AddRange(Subtract(p,Polygons[id]));
                    pieces=next;if(pieces.Count==0)break;
                }
                return pieces;
            }
        }
    }
}
