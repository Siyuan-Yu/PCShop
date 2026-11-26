using System;
using System.Collections.Generic;
using UnityEngine;
using static PCShopSell.Game.Runtime.PropForms;

namespace PCShopSell.Game.Runtime
{
    internal static partial class VehiclePropArt
    {
        private delegate Vector3 CarSurface(float u, float v);

        private static Transform BuildPassenger(Transform root, Body form, Color color)
        {
            var shape = new PassengerShape(form);
            string key = "passenger:" + form;
            var enamel = Mats.LitTex(color, new Vector2(2, 1.6f), shape.IsSport ? .32f : .25f);
            var paintedEdge = Mats.LitTex(Color.Lerp(color, ArtDirection.Hex("312D2A"), .20f), Vector2.one, .24f);
            var seal = Mats.Lit(ArtDirection.Hex("343737"), .18f);
            var glass = Mats.Lit(ArtDirection.Hex("41494D"), .40f);
            var lens = Mats.Lit(ArtDirection.Hex("858C88"), .40f);
            var reflector = Mats.Lit(ArtDirection.Hex("D2CFB6"), .37f);
            var grille = Mats.Lit(ArtDirection.Hex("313331"), .15f);
            var archLiner = Mats.Lit(ArtDirection.Hex("504942"), .16f);
            var seam = Mats.Lit(Color.Lerp(color, ArtDirection.Hex("2F2925"), .43f), .21f);

            Patch(root, key, "连续漆面", enamel, shape.BodyPanel, 96, 44);
            Patch(root, key, "连续车顶", enamel, shape.Canopy, 40, 42);
            foreach (int end in new[] { -1, 1 })
            {
                int facing = end;
                if (end > 0 && shape.HasCutNose)
                {
                    BuildCutNose(root, key, shape, enamel, paintedEdge, grille);
                    continue;
                }
                CarSurface face = (u, v) => shape.Fascia(facing, u, v);
                Patch(root, key, facing > 0 ? "前脸包角" : "尾部包角", enamel, face, 15, 44, flip: end > 0,
                    normalAt: (u, v) => Vector3.Lerp(SurfaceNormal(face,u,v),
                        SurfaceNormal(shape.BodyPanel,facing > 0 ? 1 : 0,v) * -facing,
                        Mathf.SmoothStep(0,1,Mathf.InverseLerp(.73f,1,u))).normalized);
                if (facing > 0)
                {
                    float grilleWidth = form == Body.Fastback ? .77f : .66f;
                    void FrontSlot(string name, Material material, float height, float slotWidth, float lift, float opening = .35f)
                    {
                        Patch(root, key, name, material, (u, v) =>
                        {
                            float x = (v * 2 - 1) * slotWidth;
                            float roundedCorner = Mathf.Sqrt(Mathf.Clamp01((1 - v) * v * 18));
                            float y = height + (u - .5f) * opening * roundedCorner + .039f * x * x;
                            return face(y, .5f + x * .5f) + Vector3.forward * lift;
                        }, 4, 30, flip:true);
                    }
                    FrontSlot("内收的下格栅", grille, shape.IsSport ? .30f : .37f, grilleWidth, .010f);
                    if (form == Body.Hatch)
                        FrontSlot("上格栅", grille, .71f, .43f, .010f, .17f);
                    if (form is Body.RoundCoupe or Body.TouringCoupe)
                        foreach (int side in new[] {-1,1})
                        {
                            int ventSide = side;
                            Patch(root, key, "侧保险杠进气口" + side, grille, (u, v) =>
                            {
                                float z = Mathf.Lerp(.61f,.89f,v);
                                float y = .26f + .39f * u * Mathf.Sin(v * Mathf.PI);
                                return face(y,.5f + .5f*ventSide*z) + Vector3.forward*.013f;
                            }, 5, 12, flip: side > 0);
                        }
                }
                else
                {
                    Patch(root, key, "尾门下分色", paintedEdge, (u, v) =>
                        face(.25f + .16f * u, .09f + .82f * v) + Vector3.back * .008f, 3, 26);
                }
            }

            // Windscreen and side glass sample the same canopy as the painted pillars.
            void Windscreen(string name, float start, float stop, float width, bool rear)
            {
                void Band(string label, Material material, float scale, float lift)
                {
                    Patch(root, key, name + label, material, (u, v) =>
                    {
                        float t = Mathf.Lerp(start, stop, .5f + (u - .5f) * scale);
                        float corner = Mathf.Sqrt(Mathf.Clamp01(Mathf.Min(u, 1 - u) * 11f));
                        float c = (v * 2 - 1) * width * scale * Mathf.Lerp(.85f, 1, corner) * Mathf.Lerp(rear ? 1 : .78f, rear ? .81f : 1, u);
                        return Offset(shape.Canopy, t, .5f + c * .5f, lift);
                    }, 22, 27);
                }
                Band("胶边", seal, 1f, .005f);
                Band("玻璃", glass, .941f, .0089f);
            }

            Windscreen("前挡", shape.FrontGlass, .969f, .589f, false);
            Windscreen("后挡", .035f, shape.RearGlass, form == Body.Hatch ? .61f : .59f, true);

            foreach (int side in new[] { -1, 1 })
            {
                int facing = side;
                void SideWindow(string name, float start, float stop, bool rear)
                {
                    void Pane(string label, Material material, float scale, float lift)
                    {
                        Patch(root, key, name + label + facing, material, (u, v) =>
                        {
                            float t = Mathf.Lerp(start, stop, .5f + (u - .5f) * scale);
                            float height = (shape.IsSport ? .326f : .376f) * scale *
                                Mathf.Lerp(.66f, 1f, Mathf.SmoothStep(0, 1, rear ? u : 1-u));
                            float c = Mathf.Lerp(.773f, .787f, rear ? 1-u : u) + (v-.5f)*height;
                            return Offset(shape.Canopy, t, .5f + .5f * facing * c, lift);
                        }, 19, 9, flip: facing < 0);
                    }
                    Pane("胶边", seal, 1f, .005f);
                    Pane("玻璃", glass, .933f, .0091f);
                }
                float pillar = shape.IsSport ? .45f : .52f;
                SideWindow("后侧窗", shape.IsSport ? .135f : .108f, pillar-.019f, true);
                SideWindow("前侧窗", pillar+.018f, form == Body.Hatch ? .864f : .913f, false);

                foreach (int axleSide in new[] { -1, 1 })
                {
                    float axle = shape.Axle * axleSide;
                    float tireWidth = shape.IsSport ? .194f : .158f;
                    Wheel(root, new Vector3(facing * (shape.HalfWidth - .5f*tireWidth - .027f), shape.WheelY, axle),
                        shape.TireRadius, tireWidth, facing, shape.IsSport, roundedTire:true);

                    float from = shape.Longitudinal(axle - shape.ArchSpan);
                    float to = shape.Longitudinal(axle + shape.ArchSpan);
                    Patch(root, key, "轮拱翻边" + facing + axleSide, archLiner, (u, v) =>
                    {
                        Vector3 edge = shape.BodyPanel(Mathf.Lerp(from, to, u), facing > 0 ? 1 : 0);
                        return edge + new Vector3(-facing * .105f * v, .027f*v, 0);
                    }, 36, 3, flip: facing < 0);
                }

                void BodyLine(string name, float start, float stop, float acrossStart, float acrossEnd)
                {
                    Patch(root, key, name + facing, seam, (u,v) =>
                        Offset(shape.BodyPanel, Mathf.Lerp(start, stop, u),
                            .5f + .5f*facing*(Mathf.Lerp(acrossStart, acrossEnd, u) + (v-.5f)*.009f), .006f),
                        17, 1, flip: facing < 0);
                }

                BodyLine("机盖接缝", shape.IsSport ? .743f : .759f, shape.HasCutNose ? .975f : .944f, .468f, .43f);
                BodyLine("侧裙接缝", .307f, .689f, .936f, .935f);
                Patch(root, key, "门板后竖缝" + facing, seam, (u,v) =>
                {
                    float t = (shape.IsSport ? .409f : .497f) + .031f*u + (v-.5f)*.0028f;
                    return Offset(shape.BodyPanel, t, .5f + .5f*facing*Mathf.Lerp(.788f,.925f,u), .0048f);
                }, 10, 1, flip: facing > 0);

                float doorU = shape.IsSport ? .60f : .585f;
                Vector3 handle = Offset(shape.BodyPanel, doorU, .5f + facing*.422f, .007f);
                var grip = Prims.Shape(PrimitiveType.Sphere, root, "嵌入式门把", handle,
                    new Vector3(.031f,.023f,.125f), paintedEdge);
                grip.transform.localRotation = Quaternion.Euler(0, facing*6, 0);
                if (!shape.IsSport)
                    Prims.Shape(PrimitiveType.Sphere, root, "后门把", Offset(shape.BodyPanel,.397f,.5f + facing*.421f,.007f),
                        new Vector3(.027f,.021f,.105f), paintedEdge);

                float mirrorU = shape.Longitudinal(shape.CabinFront - .31f);
                Vector3 mirrorAt = Offset(shape.BodyPanel, mirrorU, .5f + facing*.362f, -.015f);
                Vector3 mirrorEnd = mirrorAt + new Vector3(facing * .166f, .131f,.031f);
                Tube(root, "镜脚", mirrorAt, mirrorEnd, .032f, seal);
                Prims.Shape(PrimitiveType.Sphere, root, "圆角镜壳", mirrorEnd,
                    new Vector3(.146f,.077f,.120f), enamel).transform.localRotation = Quaternion.Euler(0, facing*19,0);

                void LampShape(string name, bool rear, Material mat, float scale, float lift)
                {
                    float center = rear ? .048f : form == Body.Hatch ? .924f : form == Body.RoundCoupe ? .885f : .914f;
                    float length = rear ? .061f : form == Body.RoundCoupe ? .125f : form == Body.Hatch ? .080f : .106f;
                    float span = rear ? .096f : form == Body.Fastback || form == Body.Supercar ? .072f : .122f;
                    Patch(root, key, name + facing, mat, (u, v) =>
                    {
                        float edge = (u - .5f) * 2f;
                        float taper = Mathf.Sqrt(Mathf.Max(0, 1 - edge * edge));
                        float across = (rear ? .691f : .638f) + (u - .5f) * (form == Body.Hatch ? .048f : -.21f);
                        float lateral = across + (v * 2 - 1) * span * taper * scale;
                        return Offset(shape.BodyPanel, center + (u - .5f) * length * scale, .5f + facing*lateral*.5f, lift);
                    }, 18, 9, flip: facing < 0);
                }
                if (shape.HasCutNose)
                    BuildCutHeadlamp(root, key, shape, facing, paintedEdge, seal, lens);
                else
                {
                    LampShape("灯组边框", false, seal, 1, .006f);
                    LampShape("灯组透镜", false, lens, .84f, .010f);
                    LampShape("灯内反射面", false, reflector, form == Body.Hatch ? .57f : .51f, .012f);
                }
                LampShape("尾灯轮廓", true, Tail, 1, .007f);

                if (form is Body.Wedge or Body.Supercar)
                    Patch(root, key, "后翼子板进气口" + facing, grille, (u, v) =>
                        Offset(shape.BodyPanel, Mathf.Lerp(.282f,.421f,u),
                            .5f + .5f*facing*(.838f + (.10f + .018f * (1-u)) * v - .037f*u +
                                .026f*Mathf.Pow((2*v-1)*(2*u-1),4)), .006f),
                        17, 6, flip: facing < 0);
            }

            if (form == Body.TouringCoupe)
                Box(root, "微翘尾翼", new Vector3(0,.685f,-shape.HalfLength+.21f), new Vector3(1.43f,.043f,.205f), paintedEdge);
            return root;
        }

        internal static void PassengerSponsorSticker(Transform root, string modelId)
        {
            Body form = FormFor(modelId);
            if (form is Body.BoxVan or Body.PanelVan or Body.Taxi)
            {
                Box(root, "PhoneShopSponsorSticker",new Vector3(.78f,.85f,0),new Vector3(.025f,.32f,.65f),Mats.Lit(ArtDirection.Brass));
                return;
            }
            var shape = new PassengerShape(form);
            foreach (int side in new[] { -1, 1 })
            {
                int facing = side;
                Patch(root, "passenger:" + form, "PhoneShopSponsorSticker" + side, Mats.Lit(ArtDirection.Brass), (u,v) =>
                    Offset(shape.BodyPanel, Mathf.Lerp(.468f,.587f,u), .5f + .5f*facing*Mathf.Lerp(.815f,.917f,v), .005f),
                    12, 8, flip: side < 0);
            }
        }

        private sealed class PassengerShape
        {
            // Each body station records half-width, deck crown and shoulder height above the sill.
            private static readonly float[] BodyKnots = { 0, .036f, .095f, .187f, .353f, .535f, .701f, .824f, .910f, .969f, 1 };
            private static readonly float[] CanopyKnots = { 0, .12f, .25f, .39f, .53f, .68f, .81f, .92f, 1 };
            private static readonly float[] AcrossKnots = { 0, .28f, .53f, .70f, .84f, 1 };
            private readonly Vector3[] _body;
            private readonly Vector3[] _canopy;
            private readonly float _roof, _sill, _corner, _roofExponent;
            private readonly float _cabinRear;

            internal readonly bool IsSport, HasCutNose, IsWedge;
            internal readonly float HalfWidth, HalfLength, Axle, TireRadius, WheelY, ArchSpan;
            internal readonly float FrontGlass, RearGlass, CabinFront;

            internal PassengerShape(Body form)
            {
                IsSport = form is Body.RoundCoupe or Body.TouringCoupe or Body.Wedge or Body.Supercar;
                bool hatch = form == Body.Hatch;
                bool wedge = IsWedge = form == Body.Wedge;
                HasCutNose = wedge || form == Body.Supercar;
                bool round = form is Body.RoundCoupe or Body.TouringCoupe;
                HalfWidth = hatch ? .84f : wedge ? .95f : .92f;
                HalfLength = hatch ? 1.72f : form == Body.Fastback ? 1.98f : 1.87f;
                Axle = hatch ? 1.031f : form == Body.Fastback ? 1.246f : 1.138f;
                TireRadius = hatch ? .270f : IsSport ? .281f : .284f;
                WheelY = TireRadius + .009f;
                ArchSpan = TireRadius + .081f;
                _sill = IsSport ? .154f : .186f;
                _corner = hatch ? .265f : round ? .33f : wedge ? .31f : .26f;
                _roof = hatch ? 1.387f : round ? 1.163f : wedge ? 1.045f : IsSport ? 1.106f : 1.207f;
                _cabinRear = hatch ? -1.48f : form == Body.Fastback ? -1.50f : round ? -1.405f : -1.29f;
                CabinFront = hatch ? .885f : IsSport ? .799f : .948f;
                _roofExponent = hatch ? .39f : wedge ? .62f : .46f;
                FrontGlass = hatch ? .680f : form == Body.Fastback ? .577f : .60f;
                RearGlass = hatch ? .226f : wedge ? .379f : .335f;

                float[] widths = hatch
                    ? new[]{.79f,.89f,.963f,1f,.981f,.965f,.989f,1f,.942f,.842f,.718f}
                    : new[]{.79f,.916f,.990f,1f,.952f,.934f,.979f,1f,.943f,.847f,wedge?.67f:.72f};
                float[] crown = hatch
                    ? new[]{.630f,.688f,.747f,.798f,.805f,.802f,.735f,.693f,.639f,.567f,.519f}
                    : round
                        ? new[]{.624f,.667f,.725f,.741f,.707f,.695f,.669f,.612f,.568f,.478f,.431f}
                        : wedge
                            ? new[]{.651f,.683f,.719f,.729f,.709f,.693f,.649f,.578f,.505f,.424f,.366f}
                            : IsSport
                                ? new[]{.655f,.694f,.744f,.727f,.721f,.704f,.666f,.608f,.530f,.445f,.385f}
                                : new[]{.663f,.704f,.743f,.76f,.754f,.773f,.711f,.638f,.575f,.493f,.426f};
                float[] shoulders = hatch
                    ? new[]{.571f,.639f,.707f,.753f,.742f,.728f,.730f,.724f,.652f,.553f,.480f}
                    : new[]{.525f,.624f,.697f,.719f,.652f,.647f,.675f,.702f,.624f,.494f,.367f};
                _body = new Vector3[BodyKnots.Length];
                for (int i = 0; i < _body.Length; i++) _body[i] = new Vector3(widths[i]*HalfWidth,crown[i],shoulders[i]);

                float[] crownRise = hatch ? new[]{0f,.61f,.87f,.965f,1f,.94f,.627f,.27f,0f}
                    : wedge ? new[]{0f,.20f,.48f,.76f,.96f,1f,.63f,.28f,0f}
                    : new[]{0f,.26f,.58f,.82f,.983f,1f,.66f,.30f,0f};
                _canopy = new Vector3[CanopyKnots.Length];
                float[] span = { .634f,.697f,.733f,.748f,.744f,.726f,.694f,.658f,.604f };
                for (int i = 0; i < _canopy.Length; i++) _canopy[i] = new Vector3(span[i],crownRise[i],0);
            }

            internal float Longitudinal(float z) => (z / HalfLength + 1) * .5f;

            internal Vector3 BodyPanel(float u, float v)
            {
                float z = Mathf.Lerp(-HalfLength, HalfLength, u);
                float slope = (v - .5f) * 2;
                float across = Mathf.Abs(slope);
                Vector3 body = Profile(BodyKnots, _body, u);
                float dist = Mathf.Abs(Mathf.Abs(z) - Axle);
                float archT = dist / ArchSpan;
                float arch = archT < 1 ? Mathf.Pow(1 - archT * archT, .67f) : 0f;
                float lower = Mathf.Min(body.z - .053f, Mathf.Lerp(_sill, WheelY + TireRadius + .048f, arch));
                float Deck(float at) => SmoothAcross(at, body.y, body.y+.003f, Mathf.Lerp(body.y,body.z,.34f),
                    body.z, Mathf.Lerp(body.z,_sill,.39f), _sill);
                if (arch > 0 && across > .70f)
                {
                    // Trim the lower section on the wheel opening without changing the sheet-metal surface above it.
                    float first = .70f, last = 1f;
                    for (int sample = 0; sample < 10; sample++)
                    {
                        float at = (first + last) * .5f;
                        if (Deck(at) > lower) first = at;
                        else last = at;
                    }
                    across = Mathf.Lerp(.70f,(first+last)*.5f,(across-.70f)/.30f);
                }
                float flare = Mathf.Exp(-dist*dist*8) * .019f;
                float x = SmoothAcross(across, 0, .331f, .637f, .892f, 1f, .982f) * (body.x + flare);
                float y = Deck(across);
                float ends = Mathf.Pow(Mathf.Abs(u*2-1),7);
                z -= Mathf.Sign(z) * _corner * ends * across * across;
                if (HasCutNose && u > .906f)
                {
                    float blend = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.906f,1,u));
                    float edge = IsWedge ? .56f : .63f;
                    float sweep = across < edge
                        ? Mathf.Lerp(0,IsWedge ? .072f : .052f,across/edge)
                        : Mathf.Lerp(IsWedge ? .072f : .052f,IsWedge ? .289f : .242f,(across-edge)/(1-edge));
                    float targetNose = HalfLength + (IsWedge ? .021f : .011f) - sweep;
                    z += blend * (targetNose - (HalfLength - _corner * across * across));
                    float ridge = across < edge ? across/edge : Mathf.Lerp(1,0,(across-edge)/(1-edge));
                    y += ridge * blend * (IsWedge ? .044f : .035f);
                }
                return new Vector3(Mathf.Sign(slope) * x, y, z);
            }

            internal Vector3 Canopy(float u, float v)
            {
                float z = Mathf.Lerp(_cabinRear,CabinFront,u);
                Vector3 shell = Profile(CanopyKnots,_canopy,u);
                float cross = (v - .5f) * 2f;
                float angle = cross * Mathf.PI * .5f;
                float baseY = BodyPanel(Longitudinal(z),.5f + .5f * Mathf.Abs(cross)*.43f).y - .014f;
                float y = baseY + (_roof - baseY) * shell.y * Mathf.Pow(Mathf.Max(0,Mathf.Cos(angle)), _roofExponent);
                float x = Mathf.Sin(angle) * HalfWidth * shell.x;
                float endBow = Mathf.Pow(Mathf.Abs(2*u-1),3);
                z -= Mathf.Sign(2*u-1) * endBow * cross*cross * .085f;
                return new Vector3(x,y,z);
            }

            internal Vector3 Fascia(int direction, float height, float v)
            {
                Vector3 top=BodyPanel(direction > 0 ? 1 : 0,v);
                float span=(v-.5f)*2;
                float raise=Mathf.Lerp(_sill+.038f,top.y,height);
                float z=top.z + direction*(Mathf.Sin(height*Mathf.PI)*.043f*(1-.42f*span*span));
                return new Vector3(top.x,raise,z);
            }

            private static float SmoothAcross(float t,float a,float b,float c,float d,float e,float f)
            {
                int i=Interval(AcrossKnots,t);
                float At(int n) => n switch {0=>a,1=>b,2=>c,3=>d,4=>e,_=>f};
                return Smooth(AcrossKnots, i, t, At(Mathf.Max(0,i-1)), At(i), At(i+1), At(Mathf.Min(5,i+2)));
            }

            private static Vector3 Profile(float[] knots,Vector3[] values,float t)
            {
                int i=Interval(knots,t);
                Vector3 a=values[Mathf.Max(0,i-1)], b=values[i], c=values[i+1], d=values[Mathf.Min(values.Length-1,i+2)];
                return new Vector3(Smooth(knots,i,t,a.x,b.x,c.x,d.x),Smooth(knots,i,t,a.y,b.y,c.y,d.y),
                    Smooth(knots,i,t,a.z,b.z,c.z,d.z));
            }

            private static int Interval(float[] knots,float t)
            {
                int i=0;
                while (i<knots.Length-2 && t>knots[i+1]) i++;
                return i;
            }

            private static float Smooth(float[] knots,int i,float t,float a,float b,float c,float d)
            {
                float width=knots[i+1]-knots[i], p=Mathf.Clamp01((t-knots[i])/width);
                float m0=(c-a)/(knots[i+1]-knots[Mathf.Max(0,i-1)])*width;
                float m1=(d-b)/(knots[Mathf.Min(i+2,knots.Length-1)]-knots[i])*width;
                return b + p * (m0 + p * (3*(c-b)-2*m0-m1 + p*(2*(b-c)+m0+m1)));
            }
        }

        private static Vector3 SurfaceNormal(CarSurface surface,float u,float v)
        {
            const float step = .0016f;
            Vector3 du = surface(Mathf.Min(1,u+step),v) - surface(Mathf.Max(0,u-step),v);
            Vector3 dv = surface(u,Mathf.Min(1,v+step)) - surface(u,Mathf.Max(0,v-step));
            Vector3 cross = Vector3.Cross(du,dv);
            float length = cross.magnitude;
            return length > 1e-11f ? cross / length : Vector3.zero;
        }

        private static Vector3 Offset(CarSurface surface,float u,float v,float depth) =>
            surface(u,v)+SurfaceNormal(surface,u,v)*depth;

        private static void Patch(Transform parent,string key,string name,Material material,CarSurface surface,int rows,int cols,bool flip=false, CarSurface normalAt=null)
        {
            Mesh(parent,name,key+":"+name,material,() =>
            {
                int count=(rows+1)*(cols+1);
                var vertices=new Vector3[count];
                var normals=new Vector3[count];
                var uv=new Vector2[count];
                var triangles=new List<int>(rows*cols*6);
                for (int u=0;u<=rows;u++)
                for (int v=0;v<=cols;v++)
                {
                    float t=u/(float)rows, s=v/(float)cols;
                    int at=u*(cols+1)+v;
                    vertices[at]=surface(t,s);
                    Vector3 normal=normalAt != null ? normalAt(t,s) : SurfaceNormal(surface,t,s);
                    if(normal.sqrMagnitude<.1f)
                        normal=SurfaceNormal(surface,Mathf.Clamp(t,.003f,.997f),Mathf.Clamp(s,.003f,.997f));
                    normals[at]=flip?-normal:normal;
                    uv[at]=new Vector2(s,t);
                    if(u==rows||v==cols) continue;
                    int next=at+cols+1;
                    triangles.Add(at);triangles.Add(flip?at+1:next);triangles.Add(flip?next:at+1);
                    triangles.Add(at+1);triangles.Add(flip?next+1:next);triangles.Add(flip?next:next+1);
                }
                var mesh=new Mesh();
                mesh.vertices=vertices;
                mesh.normals=normals;
                mesh.uv=uv;
                mesh.SetTriangles(triangles,0);
                mesh.RecalculateBounds();
                return mesh;
            });
        }

        private static void RoundedTire(Transform root,Vector3 center,float radius,float width)
        {
            var tyre=Group(root,"圆肩轮胎",center);
            tyre.localScale=new Vector3(width,radius,radius);
            const string key="passenger:tire";
            Patch(tyre,key,"胎面和胎肩",Rubber,(u,v) =>
            {
                float theta=v*Mathf.PI*2;
                float cross=Mathf.Lerp(-Mathf.PI*.5f,Mathf.PI*.5f,u);
                float r=.666f + .334f*Mathf.Pow(Mathf.Max(0,Mathf.Cos(cross)),.62f);
                return new Vector3(Mathf.Sin(cross)*.5f, r*Mathf.Cos(theta),r*Mathf.Sin(theta));
            },12,40,flip:true);
        }
    }
}
