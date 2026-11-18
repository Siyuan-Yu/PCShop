using System;
using System.Collections.Generic;
using UnityEngine;
namespace PCShopSell.Game.Runtime
{
    [PreferBinarySerialization]
    public sealed class BakedDetailLayout : ScriptableObject
    {
        public Group[] Groups;
        [Serializable] public sealed class Group {public Material Material;public List<Part> Parts;}
        [Serializable] public sealed class Part
        {public Mesh Mesh;public Vector3 Position;public Vector3 Scale=Vector3.one;public Quaternion Rotation=Quaternion.identity;}

        public static Quaternion ValidRotation(Quaternion rotation)
        {
            // Quaternion.operator== compares orientation via a dot product, so
            // even two zero quaternions do not compare equal. Check length instead.
            float length = Quaternion.Dot(rotation, rotation);
            if (float.IsNaN(length) || float.IsInfinity(length) || length < 1e-12f)
                return Quaternion.identity;
            if (Mathf.Abs(length - 1) < 1e-6f) return rotation;
            float inverseLength = 1 / Mathf.Sqrt(length);
            return new Quaternion(rotation.x * inverseLength, rotation.y * inverseLength,
                rotation.z * inverseLength, rotation.w * inverseLength);
        }
    }
}
