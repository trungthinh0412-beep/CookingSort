using System.Collections.Generic;
using UnityEngine;

namespace CookingSort2D
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    [AddComponentMenu("CookingBlast/Camera Outline Target")]
    public sealed class CameraOutlineTarget : MonoBehaviour
    {
        internal static readonly HashSet<CameraOutlineTarget> Active = new HashSet<CameraOutlineTarget>();
        internal MeshRenderer Renderer { get; private set; }
        internal MeshFilter Filter { get; private set; }
        private void OnEnable()
        {
            Renderer = GetComponent<MeshRenderer>();
            Filter = GetComponent<MeshFilter>();
            Active.Add(this);
        }
        private void OnDisable() { Active.Remove(this); }
    }
}
