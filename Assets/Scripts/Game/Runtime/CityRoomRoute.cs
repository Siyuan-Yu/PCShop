using System;
using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public static class CityRoomRoute
    {
        public static List<Vector3> Find(Vector3 from,Vector3 to,float width,float depth,Func<Vector3,bool> blocked)
        {
            const float cell=.25f;
            int nx=Mathf.CeilToInt(width/cell)+1,nz=Mathf.CeilToInt(depth/cell)+1;
            Vector3 Point(int id)=>new Vector3(id%nx*cell,0,id/nx*cell);
            int start=Mathf.Clamp(Mathf.RoundToInt(from.x/cell),0,nx-1)+nx*Mathf.Clamp(Mathf.RoundToInt(from.z/cell),0,nz-1);
            var prev=new int[nx*nz];for(int i=0;i<prev.Length;i++)prev[i]=-1;
            var queue=new Queue<int>();queue.Enqueue(start);prev[start]=start;int goal=-1;
            while(queue.Count>0)
            {
                int cur=queue.Dequeue();var at=Point(cur);
                if((at-to).sqrMagnitude<.2f&&!blocked(at)){goal=cur;break;}
                foreach(var delta in new[]{new Vector2Int(1,0),new Vector2Int(-1,0),new Vector2Int(0,1),new Vector2Int(0,-1)})
                {
                    int x=cur%nx+delta.x,z=cur/nx+delta.y;
                    if(x<1||z<2||x>=nx-1||z>=nz-1)continue;
                    int next=x+nx*z;if(prev[next]>=0||blocked(Point(next)))continue;
                    prev[next]=cur;queue.Enqueue(next);
                }
            }
            if(goal<0)return null;
            var result=new List<Vector3>();for(int p=goal;p!=start;p=prev[p])result.Add(Point(p));result.Reverse();return result;
        }
    }
}
