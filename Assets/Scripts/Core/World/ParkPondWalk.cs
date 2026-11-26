using System;

namespace PCShopSell.Core.World
{
    // The same bank, gate and surface are used for the visible paving and manual walking.
    public readonly struct ParkPondWalk
    {
        public const int Segments=64;
        public const float Top=.29f, EntranceStart=-4.65f, EntranceWidth=2.15f;
        private static readonly double WaterApothemSquared=Math.Pow(Math.Cos(Math.PI/Segments),2);
        public readonly double RadiusX,RadiusZ,CenterZ;
        private readonly MapPoint _door;
        private readonly double _cos,_sin;
        private readonly MapPoint[] _inner,_outer;
        public ParkPondWalk(double width,double depth,double heading=0,MapPoint door=default)
        {
            RadiusX=width*.336;RadiusZ=depth*.306;CenterZ=depth*.55;_door=door;
            double angle=heading*Math.PI/180;_cos=Math.Cos(angle);_sin=Math.Sin(angle);
            _inner=new MapPoint[Segments];_outer=new MapPoint[Segments];
            for(int i=0;i<Segments;i++)
            {
                double t=i*2*Math.PI/Segments;
                _inner[i]=Edge(t,0);_outer[i]=Edge(t,WidthAt(t));
            }
        }
        public static bool Applies(Place place)=>place.ExplicitEntrance && place.Business=="venue_park" && place.Width>=30;
        public double WidthAt(double angle)=>1.88+.14*Math.Sin(3*angle);
        public MapPoint Edge(double angle,double offset)
        {
            double c=Math.Cos(angle),s=Math.Sin(angle),nx=c/RadiusX,nz=s/RadiusZ;
            double length=Math.Sqrt(nx*nx+nz*nz);
            return new MapPoint(c*RadiusX+offset*nx/length,CenterZ+s*RadiusZ+offset*nz/length);
        }
        public MapPoint Inner(int index)=>_inner[index%Segments];
        public MapPoint Outer(int index)=>_outer[index%Segments];
        public MapPoint Centerline(int index)
        {
            var a=Inner(index);var b=Outer(index);
            return new MapPoint((a.X+b.X)*.5,(a.Y+b.Y)*.5);
        }
        public MapPoint World(MapPoint local)=>new MapPoint(_door.X+local.X*_cos+local.Y*_sin,_door.Y-local.X*_sin+local.Y*_cos);
        private static double Side(MapPoint a,MapPoint b,double x,double z)=>(b.X-a.X)*(z-a.Y)-(b.Y-a.Y)*(x-a.X);
        public float HeightAt(double worldX,double worldZ)
        {
            double dx=worldX-_door.X,dz=worldZ-_door.Y;
            double x=dx*_cos-dz*_sin,z=dx*_sin+dz*_cos;
            if(Math.Abs(x)>RadiusX+2.3 || z<EntranceStart || z>CenterZ+RadiusZ+2.3)return 0;
            if(Math.Abs(x)<=EntranceWidth*.5 && z<=Centerline(3*Segments/4).Y)
                return z>=0?Top:(float)(.027+(Top-.027)*(z-EntranceStart)/-EntranceStart);
            double y=z-CenterZ;
            // Everything inside the inscribed polygon's apothem is water. Most actors are
            // rejected by the park bounds above; only positions near this bank visit its cells.
            if(x*x/(RadiusX*RadiusX)+y*y/(RadiusZ*RadiusZ)<WaterApothemSquared-1e-8)return 0;
            const double tolerance=1e-7;
            for(int i=0;i<Segments;i++)
            {
                var a=Inner(i);var b=Outer(i);var c=Outer(i+1);var d=Inner(i+1);
                if(Side(a,b,x,z)>=-tolerance && Side(b,c,x,z)>=-tolerance &&
                    Side(c,d,x,z)>=-tolerance && Side(d,a,x,z)>=-tolerance)return Top;
            }
            return 0;
        }
    }
}
