using System.Collections.Generic;
using UnityEngine;

namespace MagicSoft.LoopSort
{
    public sealed class LoopCubePool : MonoBehaviour
    {
        private readonly Stack<LoopCube> _available =
            new Stack<LoopCube>();
        private Material _fallbackMaterial;

        public Material FallbackMaterial => _fallbackMaterial;

        public void Initialize(int preloadCount)
        {
            Shader shader = Shader.Find("Standard");

            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            _fallbackMaterial = new Material(shader)
            {
                name = "LoopCubeRuntimeMaterial",
                enableInstancing = true
            };

            for (int i = 0; i < Mathf.Max(0, preloadCount); i++)
            {
                LoopCube cube = CreateCube();
                cube.MarkPooled();
                _available.Push(cube);
            }
        }

        public LoopCube Get(
            LoopCubeColor color,
            float scale,
            float initialOffset,
            float headDistance,
            Transform parent,
            LoopColorDatabase colorDatabase)
        {
            LoopCube cube = _available.Count > 0
                ? _available.Pop()
                : CreateCube();
            cube.transform.SetParent(parent, true);
            cube.Setup(
                color,
                scale,
                initialOffset,
                headDistance,
                colorDatabase,
                _fallbackMaterial);
            return cube;
        }

        public void Release(LoopCube cube)
        {
            if (cube == null || cube.State == LoopCubeState.Pooled)
                return;

            cube.transform.SetParent(transform, false);
            cube.MarkPooled();
            _available.Push(cube);
        }

        private LoopCube CreateCube()
        {
            GameObject cubeObject = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            cubeObject.name = "LoopCube";
            cubeObject.transform.SetParent(transform, false);

            Collider primitiveCollider =
                cubeObject.GetComponent<Collider>();

            if (primitiveCollider != null)
                Destroy(primitiveCollider);

            LoopCube cube = cubeObject.AddComponent<LoopCube>();
            cube.CacheComponents();
            return cube;
        }

        private void OnDestroy()
        {
            if (_fallbackMaterial != null)
                Destroy(_fallbackMaterial);
        }
    }
}
