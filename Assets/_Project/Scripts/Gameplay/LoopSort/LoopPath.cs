using System.Collections.Generic;
using UnityEngine;

namespace MagicSoft.LoopSort
{
    public sealed class LoopPath : MonoBehaviour
    {
        [Header("Authoring shape")]
        [SerializeField, Min(4f)] private float width = 9f;
        [SerializeField, Min(3f)] private float height = 5.6f;
        [SerializeField, Range(8, 48)] private int controlPointCount = 16;
        [SerializeField] private float pathHeight = 0.42f;

        private readonly List<Vector3> _controlPoints =
            new List<Vector3>();
        private readonly List<Vector3> _samples =
            new List<Vector3>();
        private readonly List<float> _cumulativeLengths =
            new List<float>();

        private LineRenderer _lineRenderer;
        private Material _lineMaterial;

        public float Length { get; private set; }

        public float Width => width;
        public float Height => height;

        public void RebuildAuthoringPath()
        {
            InitializeEllipse(width, height, controlPointCount, pathHeight);
        }

        public void SetAuthoringShape(
            float newWidth,
            float newHeight,
            int newControlPointCount)
        {
            width = Mathf.Max(4f, newWidth);
            height = Mathf.Max(3f, newHeight);
            controlPointCount = Mathf.Clamp(newControlPointCount, 8, 48);
            RebuildAuthoringPath();
        }

        public void InitializeEllipse(
            float width,
            float height,
            int controlPointCount,
            float y = 0.12f)
        {
            this.width = width;
            this.height = height;
            this.controlPointCount = controlPointCount;
            pathHeight = y;
            _controlPoints.Clear();
            int count = Mathf.Clamp(controlPointCount, 8, 48);

            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.PI * 2f * i / count;
                _controlPoints.Add(new Vector3(
                    Mathf.Cos(angle) * width * 0.5f,
                    y,
                    Mathf.Sin(angle) * height * 0.5f));
            }

            RebuildCache(12);
            BuildLineVisual();
        }

        public Vector3 GetPosition(float distance)
        {
            GetSampleSegment(distance, out int index, out float t);

            if (_samples.Count == 0)
                return transform.position;

            int next = Mathf.Min(index + 1, _samples.Count - 1);
            Vector3 localPosition = Vector3.LerpUnclamped(
                _samples[index],
                _samples[next],
                t);
            return transform.TransformPoint(localPosition);
        }

        public Vector3 GetTangent(float distance)
        {
            GetSampleSegment(distance, out int index, out float ignored);

            if (_samples.Count < 2)
                return transform.forward;

            int next = Mathf.Min(index + 1, _samples.Count - 1);
            Vector3 tangent = transform.TransformDirection(
                _samples[next] - _samples[index]);

            return tangent.sqrMagnitude > 0.0001f
                ? tangent.normalized
                : transform.forward;
        }

        public float FindClosestDistance(Vector3 worldPosition)
        {
            if (_samples.Count == 0)
                return 0f;

            float bestSqrDistance = float.MaxValue;
            int bestIndex = 0;

            for (int i = 0; i < _samples.Count; i++)
            {
                Vector3 samplePosition =
                    transform.TransformPoint(_samples[i]);
                float sqrDistance =
                    (samplePosition - worldPosition).sqrMagnitude;

                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    bestIndex = i;
                }
            }

            return _cumulativeLengths[bestIndex];
        }

        private void RebuildCache(int samplesPerSegment)
        {
            _samples.Clear();
            _cumulativeLengths.Clear();
            Length = 0f;

            if (_controlPoints.Count < 4)
                return;

            int pointCount = _controlPoints.Count;
            int subdivisions = Mathf.Max(4, samplesPerSegment);

            for (int segment = 0; segment < pointCount; segment++)
            {
                Vector3 p0 = _controlPoints[
                    WrapIndex(segment - 1, pointCount)];
                Vector3 p1 = _controlPoints[segment];
                Vector3 p2 = _controlPoints[
                    WrapIndex(segment + 1, pointCount)];
                Vector3 p3 = _controlPoints[
                    WrapIndex(segment + 2, pointCount)];

                for (int sample = 0; sample < subdivisions; sample++)
                {
                    float t = sample / (float)subdivisions;
                    AddSample(CatmullRom(p0, p1, p2, p3, t));
                }
            }

            AddSample(_samples[0]);
        }

        private void AddSample(Vector3 position)
        {
            if (_samples.Count > 0)
            {
                Length += Vector3.Distance(
                    _samples[_samples.Count - 1],
                    position);
            }

            _samples.Add(position);
            _cumulativeLengths.Add(Length);
        }

        private void GetSampleSegment(
            float distance,
            out int index,
            out float t)
        {
            index = 0;
            t = 0f;

            if (_samples.Count < 2 || Length <= 0f)
                return;

            float wrapped = Mathf.Repeat(distance, Length);
            int low = 0;
            int high = _cumulativeLengths.Count - 1;

            while (low < high)
            {
                int middle = (low + high) / 2;

                if (_cumulativeLengths[middle] < wrapped)
                    low = middle + 1;
                else
                    high = middle;
            }

            int upper = Mathf.Clamp(low, 1, _samples.Count - 1);
            index = upper - 1;
            float start = _cumulativeLengths[index];
            float end = _cumulativeLengths[upper];
            t = end > start
                ? Mathf.InverseLerp(start, end, wrapped)
                : 0f;
        }

        private void BuildLineVisual()
        {
            _lineRenderer = gameObject.GetComponent<LineRenderer>();

            if (_lineRenderer == null)
                _lineRenderer = gameObject.AddComponent<LineRenderer>();

            Shader shader = Shader.Find("Sprites/Default");
            _lineMaterial = new Material(shader);
            _lineMaterial.color = new Color32(55, 63, 75, 255);

            _lineRenderer.sharedMaterial = _lineMaterial;
            _lineRenderer.useWorldSpace = false;
            _lineRenderer.loop = true;
            _lineRenderer.widthMultiplier = 0.58f;
            _lineRenderer.numCornerVertices = 4;
            _lineRenderer.numCapVertices = 2;
            _lineRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            _lineRenderer.receiveShadows = false;
            _lineRenderer.positionCount =
                Mathf.Max(0, _samples.Count - 1);

            for (int i = 0; i < _lineRenderer.positionCount; i++)
                _lineRenderer.SetPosition(i, _samples[i]);
        }

        private static int WrapIndex(int index, int count)
        {
            index %= count;
            return index < 0 ? index + count : index;
        }

        private static Vector3 CatmullRom(
            Vector3 p0,
            Vector3 p1,
            Vector3 p2,
            Vector3 p3,
            float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f *
                   ((2f * p1) +
                    (-p0 + p2) * t +
                    (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                    (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private void OnDestroy()
        {
            if (_lineMaterial != null)
                Destroy(_lineMaterial);
        }

        private void OnValidate()
        {
            width = Mathf.Max(4f, width);
            height = Mathf.Max(3f, height);
            controlPointCount = Mathf.Clamp(controlPointCount, 8, 48);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.22f, 0.25f, 0.3f, 0.9f);
            const int previewSegments = 64;
            Vector3 previous = transform.TransformPoint(new Vector3(
                width * 0.5f,
                pathHeight,
                0f));

            for (int i = 1; i <= previewSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / previewSegments;
                Vector3 current = transform.TransformPoint(new Vector3(
                    Mathf.Cos(angle) * width * 0.5f,
                    pathHeight,
                    Mathf.Sin(angle) * height * 0.5f));
                Gizmos.DrawLine(previous, current);
                previous = current;
            }
        }
    }
}
