using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lean.Pool;
using CustomTween;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public abstract class ResourceHandler : MonoBehaviour
{
    [Header("Common Settings")]
    [SerializeField] protected int numberObject = 5;
    [SerializeField] protected float spawnInterval = .1f;
    [SerializeField] protected float animStagger = .02f;
    [SerializeField] protected float durationTarget = .6f;
    [SerializeField] protected Ease easeTarget = Ease.InOutQuad;
    [SerializeField] protected float scale = 1;
    [SerializeField] protected float bezierCurve = 60f;

    [Header("Runtime")]
    protected Vector3? from;
    protected Canvas canvas;

    protected List<Resource> visibleResources = new List<Resource>();

    // -------- ABSTRACT: mỗi resource khác nhau ----------
    protected abstract Resource Prefab { get; }
    protected abstract GameObject Target { get; }
    protected abstract TextMeshProUGUI AmountText { get; }
    protected abstract GameObject Bar { get; }
    protected abstract int Cache { get; set; }
    protected abstract int CurrentValue { get; }

    // -------- ABSTRACT: event riêng vàng/kim cương -------
    protected abstract void SubscribeEvents();
    protected abstract void UnsubscribeEvents();
    protected abstract void OnCollectedEffect(GameObject target);

    protected virtual void Awake()
    {
        canvas = PopupController.Instance.CanvasTransform.GetComponent<Canvas>();
    }

    protected virtual void OnEnable()
    {
        SubscribeEvents();
        Observer.SpawnResourcesChanged += SpawnResourcesChanged;
    }

    protected virtual void OnDisable()
    {
        UnsubscribeEvents();
        Observer.SpawnResourcesChanged -= SpawnResourcesChanged;
    }

    private void SpawnResourcesChanged(Vector3 position)
    {
        from = position;
    }

    protected virtual void Start()
    {
        ResetCache();
    }

    public virtual void ResetCache()
    {
        Cache = CurrentValue;
        AmountText.text = Cache.ToString();
    }

    // ============================================================
    // INCREASE / DECREASE
    // ============================================================

    public void Increase(int value)
    {
        GenerateResource(value);
    }

    public async void Decrease(int amount)
    {
        int step = (amount % numberObject == 0) ? numberObject : 1;
        int perStep = amount / step;

        for (int i = 0; i < step; i++)
        {
            await Task.Delay(TimeSpan.FromSeconds(spawnInterval * i));

            Cache -= perStep;
            Cache = Mathf.Max(0, Cache);
            AmountText.text = Cache.ToString();

            if (Cache == 0) return;
        }
    }

    // ============================================================
    // SPAWN + MOVE
    // ============================================================

    protected void GenerateResource(int amount)
    {
        if (Target == null) return;

        int objects = 1;

        for (int i = 10; i >= 0; i--)
            if (i > 0 && amount % i == 0)
            {
                objects = i;
                break;
            }

        int perStep = amount / objects;

        float zCanvas = canvas.planeDistance;
        Vector3 startPos = from ?? Vector3.zero;
        startPos.z = zCanvas;

        // Phase 1: Spawn tất cả ngay lập tức
        // i lẻ: Animator chạy ngay | i chẵn: Animator bị delay animStagger để xen kẽ
        List<Resource> batch = new List<Resource>(objects);
        for (int i = 0; i < objects; i++)
        {
            Resource r = LeanPool.Spawn(Prefab, PopupController.Instance.CanvasTransform);
            r.transform.localScale = Vector3.one * scale;
            r.transform.position = startPos;
            visibleResources.Add(r);
            batch.Add(r);

            if (i % 2 == 0)
            {
                Animator anim = r.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.enabled = false;
                    Tween.Delay<Animator>(anim, animStagger, a => a.enabled = true);
                }
            }
        }

        // Phase 2: Di chuyển lần lượt, mỗi cái cách nhau spawnInterval
        for (int i = 0; i < objects; i++)
        {
            Resource r = batch[i];
            float moveDelay = spawnInterval * i;

            MoveBezierToTarget(r.gameObject, Target, moveDelay).OnComplete(() =>
            {
                LeanPool.Despawn(r);
                visibleResources.Remove(r);

                if (visibleResources.Count == 0)
                    from = null;

                Cache += perStep;
                AmountText.text = Cache.ToString();

                OnCollectedEffect(Target);

                Tween.PunchScale(Bar.transform, Vector3.one, .2f, 0.2f);
            });
        }
    }

    private Tween MoveBezierToTarget(GameObject obj, GameObject target, float startDelay = 0f)
    {
        Vector3 p0 = obj.transform.position;
        Vector3 p2 = target.transform.position;

        // Control point: midpoint + small perpendicular offset
        Vector3 mid = (p0 + p2) * 0.5f;
        Vector3 dir = (p2 - p0).normalized;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
        // Alternate curve direction per resource for variety
        float side = 1f;
        Vector3 p1 = mid + perp * (bezierCurve * side);

        return Tween.Custom<Transform>(obj.transform, 0f, 1f, durationTarget,
            (t, v) =>
            {
                float u = 1f - v;
                t.position = u * u * p0 + 2f * u * v * p1 + v * v * p2;
            }, easeTarget, startDelay: startDelay,useUnscaledTime:true);
    }
}
