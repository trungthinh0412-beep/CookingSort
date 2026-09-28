#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
public static class SceneViewUtils
{
    /// <summary>
    /// Do the sceneView reposition of a localPosition field of a component. Also call a setter if the value is changed
    /// </summary>
    /// <param name="c"></param>
    /// <param name="localPos"></param>
    /// <param name="label"></param>
    /// <param name="undoText"></param>
    /// <param name="getter"></param>
    /// <param name="setter"></param>
    public static void SceneJointAnchor(
        Component c,
        ref Vector2 localPos,
        string label = null,
        string undoText = null,
        System.Func<Vector2?> getter = null,
        System.Action<Vector2> setter = null)
    {
        if (Tools.current != Tool.Move)
        {
            Vector3 worldPos = c.transform.TransformPoint(localPos);
            //Handles.DrawWireDisc(worldPos, Vector3.forward, 0.1f);
            //Handles.DrawLine(c.transform.position, worldPos);
            if (!string.IsNullOrEmpty(label)) Handles.Label(worldPos, label);
            worldPos = Handles.DoPositionHandle(worldPos, c.transform.rotation);

            Vector2 newLocalPos = c.transform.InverseTransformPoint(worldPos);
            if ((newLocalPos - localPos).sqrMagnitude > 0.000001f)
            {
                Undo.RecordObject(c, undoText);
                localPos = newLocalPos;

                EditorUtility.SetDirty(c);
            }
        }

        if (!Application.isPlaying)
        {
            if (getter != null && setter != null && (getter() != localPos))
            {
                setter(localPos);
            }
        }
    }


    /// <summary>
    /// Render an object on sceneView
    /// </summary>
    public static void FakeRender(
        GameObject obs,
        Vector3 position,
        Vector3 scale,
        Quaternion rotation)
    {
        var matrixBase = Matrix4x4.TRS(position, rotation, scale);
        int layer = obs.gameObject.layer;
        var mfs = obs.GetComponentsInChildren<MeshFilter>();
        foreach (var mf in mfs)
        {
            Mesh mesh = mf.sharedMesh;
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            Material[] materials = mr?.sharedMaterials;
            if (mesh && materials != null)
            {
                var matrix = matrixBase * Matrix4x4.TRS(mf.transform.localPosition, mf.transform.localRotation, mf.transform.localScale);
                foreach (var material in materials)
                {
                    if (material)
                    {
                        //Graphics.DrawMesh(mesh, matrix, material, layer);
                        for (int i = 0; i < material.passCount; i++)
                        {
                            material.SetPass(i);
                            Graphics.DrawMeshNow(mesh, matrix);
                        }
                    }
                }
            }

            EditorUtility.SetDirty(mesh);
        }
    }

    /// <summary>
    /// Render an object on sceneView using its SkinnedMeshRenderers
    /// </summary>
    public static void FakeRenderSkinnedMesh(
        GameObject obs,
        Vector3 position,
        Vector3 scale,
        Quaternion rotation)
    {
        var matrixBase = Matrix4x4.TRS(position, rotation, scale);
        var smrs = obs.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (var smr in smrs)
        {
            Mesh mesh = new Mesh();
            smr.BakeMesh(mesh);

            Material[] materials = smr.sharedMaterials;
            if (mesh && materials != null)
            {
                var matrix = matrixBase * Matrix4x4.TRS(smr.transform.localPosition, smr.transform.localRotation, smr.transform.localScale);
                foreach (var material in materials)
                {
                    if (material)
                    {
                        for (int i = 0; i < material.passCount; i++)
                        {
                            material.SetPass(i);
                            Graphics.DrawMeshNow(mesh, matrix);
                        }
                    }
                }
            }

            Object.DestroyImmediate(mesh);
        }
    }


    /// <summary>
    /// Render an object on sceneView using line renderer
    /// </summary>
    public static void FakeRenderLine(
        GameObject obs,
        Vector3 position,
        Vector3 scale,
        Quaternion rotation)
    {
        var matrixBase = Matrix4x4.TRS(position, rotation, scale) * Matrix4x4.Inverse(obs.transform.localToWorldMatrix);
        var rends = obs.GetComponentsInChildren<LineRenderer>();
        foreach (var rend in rends)
        {
            Mesh mesh = new Mesh();
            rend.BakeMesh(mesh);

            Material[] materials = rend.sharedMaterials;
            if (mesh && materials != null)
            {
                var matrix = matrixBase;
                if (!rend.useWorldSpace) matrix *= Matrix4x4.TRS(rend.transform.position, rend.transform.rotation, rend.transform.localScale);

                foreach (var material in materials)
                {
                    if (material)
                    {
                        for (int i = 0; i < material.passCount; i++)
                        {
                            material.SetPass(i);
                            Graphics.DrawMeshNow(mesh, matrix);
                        }
                    }
                }
            }

            Object.DestroyImmediate(mesh);
        }
    }

    /// <summary>
    /// Render an object on sceneView using sprite renderers
    /// </summary>
    public static void FakeRenderSprite(
        GameObject obs,
        Vector3 position,
        Vector3 scale,
        Quaternion rotation)
    {
        var rends = obs.GetComponentsInChildren<SpriteRenderer>();
        foreach (var rend in rends)
        {
            var bounds = rend.bounds;
            var pos = rend.transform.position - obs.transform.position + position;
            HandlesUtils.DrawSprite(rend.sprite, pos, bounds.size.Mult(scale));
        }
    }


    public static bool Get2DMouseScenePosition(
        out Vector2 omp)
    {
        omp = Vector2.zero;

        Camera cam = Camera.current;
        var mp = Event.current.mousePosition;
        mp.y = cam.pixelHeight - mp.y;
        var ray = cam.ScreenPointToRay(mp);
        if (ray.direction != Vector3.forward) return false;

        omp = ray.origin;
        return true;
    }

    public static bool GetTopdownMouseScenePosition(
        out Vector3 mousePostition)
    {
        mousePostition = Vector3.zero;

        Camera cam = Camera.current;
        var mp = Event.current.mousePosition;
        mp.y = cam.pixelHeight - mp.y;
        var ray = cam.ScreenPointToRay(mp);
        //if (ray.direction != Vector3.down) return false;

        mousePostition = ray.origin;
        mousePostition.y = 0;
        return true;
    }

    /// <summary>
    /// Calculate mouseCast on sceneView, using Event.current
    /// </summary>
    /// <param name="maxDist"></param>
    /// <returns></returns>
    public static MouseCastData MouseCast(
        float maxDist = 20,
        int? layerMask = null)
    {
        Camera cam = Camera.current;
        MouseCastData mcd = new MouseCastData();
        mcd.screenPos = Event.current.mousePosition;
        mcd.screenPos.y = cam.pixelHeight - mcd.screenPos.y;
        mcd.ray = cam.ScreenPointToRay(mcd.screenPos);

        if (Physics.Raycast(mcd.ray, out var hit, maxDist, layerMask ?? Physics.AllLayers))
        {
            mcd.isHit = true;
            mcd.hit = hit;
            mcd.hitPoint = hit.point;
        }
        else
        {
            mcd.isHit = false;
        }

        mcd.groundPos = mcd.CalculatePositionAtY(0, 1000);
        return mcd;
    }

    public class MouseCastData
    {
        public Vector2 screenPos;
        public Ray ray;
        public bool isHit;
        public RaycastHit hit;
        public Vector3 hitPoint;
        public Vector3? groundPos;

        public Vector3? CalculatePositionAtY(
            float y,
            float maxDist = -1)
        {
            if (y == ray.origin.y) return ray.origin;
            if (ray.direction.y == 0) return null;

            float mult = (y - ray.origin.y) / ray.direction.y;
            if (mult < 0) return null;

            Vector3 v = ray.origin + ray.direction * mult;
            if (maxDist >= 0 && (v - ray.origin).sqrMagnitude > maxDist * maxDist) return null;

            return v;
        }
    }
}
#endif