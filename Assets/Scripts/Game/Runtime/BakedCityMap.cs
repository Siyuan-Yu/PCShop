using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.Runtime
{
    // Only fixed presentation is baked. Session, guests and player furniture are never stored here.
    public sealed class BakedCityMap : MonoBehaviour
    {
        public const int CurrentVersion=16;
        public const string ResourcePath="BakedCity/Map";
        public int Version=CurrentVersion;
        public string LayoutHash,SourceHash;
        public bool Trial;
        public Bounds Bounds;
        public PlaceRecord[] Places;
        public MarkerRecord[] Markers;
        public GameObject[] GroundMarkers;
        public Light[] NightLights;
        public GlowRecord[] Glows;
        public RampRecord[] Ramps;
        public RaceRecord[] Races;
        public ExpoRecord[] Expos;
        public GameObject[] StaticBatches;
        public Transform[] DetailRoots;
        public BakedDetailLayout Details;
        public Transform ForestRoot;
        public BakedForestLayout ForestData;
        [NonSerialized] public ForestChunk[] Forest;
        [Serializable] public sealed class PlaceRecord
        {
            public string Id;
            public Vector3 Entrance,LabelPos;
            public GameObject Root,Exterior,Interior;
            public Text Sign;
            public GameObject[] OpenOnly,ClosedOnly;
            public Light Neon;
        }
        [Serializable] public sealed class MarkerRecord { public GameObject Target;public string PlaceId; }
        [Serializable] public sealed class GlowRecord { public Material Material;public Color Emission;public float Daylight; }
        [Serializable] public sealed class RampRecord
        {
            public Vector2 A,B;
            public float Width,PavedWidth,Deck,DryStart,DryEnd,RampLength;
            public bool Raised,GroundAtA,GroundAtB;
        }
        [Serializable] public sealed class RaceRecord { public GameObject Root;public string PlaceId; }
        [Serializable] public sealed class ExpoRecord { public GameObject Root;public float Width,Depth; }
        [Serializable] public sealed class ForestChunk
        {
            public string Name;
            public List<ForestPart> Parts=new List<ForestPart>();
        }
        [Serializable] public sealed class ForestPart
        {
            public Mesh Mesh;
            public Vector3 Position;
            public int Color;
        }
        public static string HashLayout(string json)
        {
            using(var sha=System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json))).Replace("-","");
        }
    }
}
