// Module 4 | Sentinels | University of Moratuwa | 2026

using UnityEngine;
using Newtonsoft.Json;

namespace TeamSentinels.Module4.Data
{
    /// <summary>Serializable substitute for UnityEngine.Vector3.</summary>
    [System.Serializable]
    public struct Vector3Serializable
    {
        #region Data

        [JsonProperty("x")] public float x;
        [JsonProperty("y")] public float y;
        [JsonProperty("z")] public float z;

        #endregion

        #region Public API

        public Vector3Serializable(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static implicit operator Vector3(Vector3Serializable v) =>
            new Vector3(v.x, v.y, v.z);

        public static implicit operator Vector3Serializable(Vector3 v) =>
            new Vector3Serializable(v.x, v.y, v.z);

        public override string ToString() => $"({x:F2}, {y:F2}, {z:F2})";

        #endregion
    }
}
