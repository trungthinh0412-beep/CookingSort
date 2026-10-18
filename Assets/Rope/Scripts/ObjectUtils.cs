using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ObjectUtils
{
    /// <summary>
    /// Get all assets of type <typeparamref name="T"/> in the current scene and/or in all assets. The value string is the location of the object: scene - or - asset
    /// </summary>
    public static List<KeyValuePair<T, string>> GetObjectsOfType<T>(
        bool scene = true,
        bool assets = true)
        where T : Object
    {
        List<KeyValuePair<T, string>> l = new List<KeyValuePair<T, string>>();
        if (scene)
        {
            foreach (T t0 in Object.FindObjectsByType<T>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.InstanceID
                     ))
            {
                l.Add(new KeyValuePair<T, string>(t0, "scene"));
            }
        }
#if UNITY_EDITOR
        if (assets)
        {
            foreach (T t0 in AssetUtils.FindAllAssets<T>())
            {
                l.Add(new KeyValuePair<T, string>(t0, "asset"));
            }
        }
#endif
        return l;
    }


    /// <summary>
    /// Get the first object of type T in the active scene, including inactive
    /// </summary>
    public static T GetSceneObjectOfType<T>()
        where T : Component
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene != null)
        {
            var rootObjs = scene.GetRootGameObjects();
            var obj = rootObjs.Select(ro => ro.GetComponentInChildren<T>(true))
                .FirstOrDefault(u => u);
            return obj;
        }

        return default;
    }

    /// <summary>
    /// Get scene objects of type T in the active scene, including inactive
    /// </summary>
    public static IEnumerable<T> GetSceneObjectsOfType<T>()
        where T : Component
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene != null)
        {
            var rootObjs = scene.GetRootGameObjects();
            var objs = rootObjs.SelectMany(ro => ro.GetComponentsInChildren<T>(true));
            return objs;
        }

        return default;
    }


    /// <summary>
    /// Instantiate object and connect prefab if possible
    /// </summary>
    public static T InstantiatePrefab<T>(
        T prefab,
        Transform parent,
        bool connectPrefab = true)
        where T : Object
    {
#if UNITY_EDITOR
        if (!connectPrefab || Application.isPlaying) return Object.Instantiate(prefab, parent);
        return PrefabUtility.InstantiatePrefab(prefab, parent) as T;
#else
            return Object.Instantiate(prefab, parent);
#endif
    }


    public static Bounds GetRendererBounds(
        GameObject go,
        bool includeInactive = true)
    {
        return GetBounds<Renderer>(go, includeInactive);
    }

    public static Bounds GetColliderBounds(
        GameObject go,
        bool includeInactive = true)
    {
        return GetBounds<Collider>(go, includeInactive);
    }

    public static Bounds GetCollider2DBounds(
        GameObject go,
        bool includeInactive = true)
    {
        return GetBounds<Collider2D>(go, includeInactive);
    }

    public static Bounds GetBounds<T>(
        GameObject go,
        bool includeInactive = true,
        System.Func<T, Bounds> getBounds = null)
        where T : Component
    {
        if (getBounds == null)
            getBounds = (
                t) => (t as Collider)?.bounds ?? (t as Collider2D)?.bounds ?? (t as Renderer)?.bounds ?? default;
        var comps = go.GetComponentsInChildren<T>(includeInactive);

        Bounds b0 = default;
        bool found = false;

        foreach (var comp in comps)
        {
            if (comp)
            {
                if (!includeInactive)
                {
                    if (!(comp as Collider)?.enabled ?? false) continue;
                    if (!(comp as Collider2D)?.enabled ?? false) continue;
                    if (!(comp as Renderer)?.enabled ?? false) continue;
                    if (!(comp as MonoBehaviour)?.enabled ?? false) continue;
                }

                if (!found || b0.size == Vector3.zero)
                {
                    b0 = getBounds(comp);
                    found = true;
                }
                else b0.Encapsulate(getBounds(comp));
            }
        }

        return b0;
    }

    public static Bounds GetBounds<T>(
        bool includeInactive = true,
        System.Func<T, Bounds> getBounds = null,
        params GameObject[] gos)
        where T : Component
    {
        Bounds b0 = default;
        bool found = false;

        foreach (var go in gos)
        {
            if (go != null)
            {
                if (!found)
                {
                    b0 = GetBounds<T>(go, includeInactive, getBounds);
                    found = true;
                }
                else b0.Encapsulate(GetBounds<T>(go, includeInactive, getBounds));
            }
        }

        return b0;
    }


    /// <summary>
    /// Distribute objects using there renderer bounds among X and Y
    /// </summary>
    public static void DistributeObjects<T>(
        List<T> list,
        Transform parent,
        Vector2 spacing,
        float minRowX = 0)
        where T : Component
    {
        float sumLengthX = list.Sum(item => GetRendererBounds(item.gameObject)
            .size.x + spacing.x);
        float rowX = Mathf.Max(minRowX, Mathf.Pow(sumLengthX, 0.5f));
        float usedX = 0;
        float usedY = 0;
        float thisRowMaxY = 0;

        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            Bounds bounds = GetRendererBounds(item.gameObject);
            var size = bounds.size;
            float useUpX = size.x + spacing.x;

            if (usedX + useUpX > rowX)
            {
                usedX = 0;
                usedY += thisRowMaxY + spacing.y;
                thisRowMaxY = 0;
            }

            Vector3 botLeftCorner = bounds.min;
            Vector3 offset = new Vector3(usedX, usedY) - botLeftCorner;
            item.transform.position += offset;

            usedX += useUpX;
            thisRowMaxY = Mathf.Max(thisRowMaxY, size.y);
        }

        Bounds totalBounds = GetRendererBounds(parent.gameObject);
        Vector3 offsetAll = parent.position - totalBounds.center;
        foreach (var item in list)
        {
            item.transform.position += offsetAll;
        }
    }


    /// <summary>
    /// Copy configuration from a RB to another
    /// </summary>
    public static void CopyRigidbody(
        Rigidbody from,
        Rigidbody to)
    {
        to.mass = from.mass;
        to.linearDamping = from.linearDamping;
        to.angularDamping = from.angularDamping;
        to.constraints = from.constraints;
        to.freezeRotation = from.freezeRotation;
        to.useGravity = from.useGravity;
        to.isKinematic = from.isKinematic;
        to.collisionDetectionMode = from.collisionDetectionMode;
    }

    public static void AutoGet<T>(
        this Component o,
        out T target,
        GetDir dir = GetDir.Self,
        bool inactiveChild = false)
        where T : Component
    {
        switch (dir)
        {
            case GetDir.Child:
                target = o.GetComponentInChildren<T>(inactiveChild);
                break;
            case GetDir.Parent:
                target = o.GetComponentInParent<T>();
                break;
            default:
                target = o.GetComponent<T>();
                break;
        }
    }

    /// <summary>
    /// Destroy all children having certain type <typeparamref name="T"/>
    /// </summary>
    public static void ClearChildrenOfType<T>(
        this Transform t,
        bool immediate = false,
        bool asset = false)
        where T : Component
    {
        T[] ts = t.GetComponentsInChildren<T>();
        for (int i = ts.Length - 1; i >= 0; i--)
        {
            if (ts[i] != null && ts[i]
                .gameObject != null)
            {
                if (immediate)
                    Object.DestroyImmediate(ts[i]
                            .gameObject,
                        asset);
                else
                    Object.Destroy(ts[i]
                        .gameObject);
            }
        }
    }

    public static T GetRepeat<T>(
        this IList<T> l,
        int i)
    {
        return l[Repeat(i, l.Count)];
    }

    /// <summary>
    /// count must be >=0, return from 0->count-1
    /// </summary>
    public static int Repeat(
        int value,
        int count)
    {
        if (count <= 0) return 0;
        while (value < 0) value += count;
        if (value < count) return value;
        return value % count;
    }

    /// <summary>
    /// Calculate average of vectors. Vector zero if empty list
    /// </summary>
    public static Vector3 AverageVectors(
        List<Vector3> vs)
    {
        if (vs == null || vs.Count == 0) return Vector3.zero;
        int count = vs.Count;
        var sum = vs.Aggregate(Vector3.zero,
            (
                v1,
                v2) => v1 + v2);
        return sum / count;
    }

    public static Rect FromCenter(
        this Rect r,
        Vector2 center,
        Vector2 size)
    {
        return new Rect(center - size / 2f, size);
    }
    
    /// <summary>
    /// Return a bounds containing all vectors, zero bounds if no vectors found
    /// </summary>
    public static Bounds GetBounds(IList<Vector3> vs)
    {
        if (vs.Count > 0)
        {
            var min = new Vector3(vs.Min(v => v.x), vs.Min(v => v.y), vs.Min(v => v.z));
            var max = new Vector3(vs.Max(v => v.x), vs.Max(v => v.y), vs.Max(v => v.z));
            var b = new Bounds((min + max) / 2f, max - min);
            return b;
        }
        else
        {
            return new Bounds();
        }
    }

#if UNITY_EDITOR
    public static void ModifyTransformWithoutChildren(Transform tr, System.Action<Transform> modification)
    {
        List<Transform> childs = new List<Transform>();
        for (int i = 0; i < tr.childCount; i++)
        {
            childs.Add(tr.GetChild(i));
        }

        foreach (var c in childs)
        {
            Undo.SetTransformParent(c, tr.parent, "Modify parent");
        }

        Undo.RegisterCompleteObjectUndo(tr, "Modify parent");
        modification?.Invoke(tr);

        foreach (var c in childs)
        {
            Undo.SetTransformParent(c, tr, "Modify parent");
            EditorUtility.SetDirty(c);
        }

        EditorUtility.SetDirty(tr);
    }
#endif

    
    /// <summary>
    /// Mono-wait routine which yield anything specified. YieldInstruction, CustomYieldInstruction,...
    /// </summary>
    public static Coroutine TWLEWait(this MonoBehaviour c, object yield, System.Action onComplete)
    {
        return c.StartCoroutine(WaitRoutine(yield, onComplete));
    }

    static IEnumerator WaitRoutine(object yield, System.Action onComplete)
    {
        yield return yield;
        onComplete?.Invoke();
    }
}

public enum GetDir
{
    Self,
    Child,
    Parent
}
