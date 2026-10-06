using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BottomTabBar : MonoBehaviour
{
    [Serializable]
    public class Tab
    {
        [Header("Button")]
        public Button button;
        public LayoutElement buttonLayout;

        [Header("Under Image")]
        public LayoutElement underLayout;

        [Header("Visual")]
        public RectTransform icon;
        public RectTransform label;
    }

    [Header("Tabs")]
    [SerializeField] private List<Tab> tabs = new List<Tab>();

    [Header("Layout Roots")]
    [SerializeField] private RectTransform buttonLayoutRoot;
    [SerializeField] private RectTransform underLayoutRoot;

    [Header("Overlay")]
    [SerializeField] private RectTransform overlaySelected;
    [SerializeField] private float overlayOffsetX = 0f;

    [Header("Tab Width")]
    [SerializeField] private float normalWidth = 198f;
    [SerializeField] private float selectedWidth = 288f;

    [Header("Animation")]
    [SerializeField] private float widthDuration = 0.22f;
    [SerializeField] private float overlayDuration = 0.28f;

    [Header("Selected Icon")]
    [SerializeField] private float selectedIconYOffset = 70f;

    [SerializeField] private float selectedIconScale = 1.4f;

    [SerializeField] private float selectedIconOvershootScale = 1.5f;

    [Header("Selected Label")]
    [SerializeField] private float selectedLabelYOffset = 10f;
    [SerializeField] private float selectedLabelScale = 1.2f;

    [Header("Visual Speed")]
    [SerializeField] private float selectVisualDuration = 0.20f;
    [SerializeField] private float deselectVisualDuration = 0.07f;

    [Header("Default Tab")]
    [SerializeField] private int defaultTabIndex = 1;

    private int _currentTabIndex = -1;

    private Coroutine[] _widthCoroutines;
    private Coroutine _overlayCoroutine;

    private Coroutine[] _iconYCoroutines;
    private Coroutine[] _iconScaleCoroutines;

    private Coroutine[] _labelYCoroutines;
    private Coroutine[] _labelAlphaCoroutines;
    private Coroutine[] _labelScaleCoroutines;

    private readonly List<CanvasGroup> _labelCanvasGroups = new List<CanvasGroup>();
    private readonly List<Vector2> _originalIconAnchors = new List<Vector2>();
    private readonly List<Vector2> _originalLabelAnchors = new List<Vector2>();
    private readonly List<float> _currentLabelAlpha = new List<float>();

    private void Awake()
    {
        int count = tabs.Count;

        _widthCoroutines = new Coroutine[count];
        _iconYCoroutines = new Coroutine[count];
        _iconScaleCoroutines = new Coroutine[count];
        _labelYCoroutines = new Coroutine[count];
        _labelAlphaCoroutines = new Coroutine[count];
        _labelScaleCoroutines = new Coroutine[count];

        for (int i = 0; i < count; i++)
        {
            int index = i; 
            tabs[i].button.onClick.AddListener(() => OnTabClicked(index));

            if (tabs[i].label != null)
            {
                CanvasGroup cg = tabs[i].label.GetComponent<CanvasGroup>();
                if (cg == null)
                    cg = tabs[i].label.gameObject.AddComponent<CanvasGroup>();
                _labelCanvasGroups.Add(cg);
                _currentLabelAlpha.Add(1f);
            }
            else
            {
                _labelCanvasGroups.Add(null);
                _currentLabelAlpha.Add(1f);
            }

            if (tabs[i].icon != null)
                _originalIconAnchors.Add(tabs[i].icon.anchoredPosition);
            else
                _originalIconAnchors.Add(Vector2.zero);

            if (tabs[i].label != null)
                _originalLabelAnchors.Add(tabs[i].label.anchoredPosition);
            else
                _originalLabelAnchors.Add(Vector2.zero);
        }
    }

    private void Start()
    {
        ForceStateImmediate(defaultTabIndex);
    }
    
    // ============================================================
    // ON ENABLE HOOK
    // ============================================================

    private void OnEnable()
    {
        UpdateSelectedTabToMatchPopup();
    }
    
    private void OnDisable()
    {
    }
    
    private void OnHideTabsUI(GameObject obj)
    {
        gameObject.SetActive(false);
    }

    private void UpdateSelectedTabToMatchPopup()
    {
        if (PopupController.Instance == null)
            return;
            
        int targetIndex = PopupController.Instance.GetCurrentMainTabIndex();
        
        if (targetIndex >= 0 && targetIndex != _currentTabIndex && targetIndex < tabs.Count)
        {
            OnTabClicked(targetIndex);
        }
    }

    private void Update()
    {
        ForceRebuildIfDirty();
    }

    private void ForceRebuildIfDirty()
    {
        bool dirty = false;

        for (int i = 0; i < tabs.Count; i++)
        {
            if (_widthCoroutines[i] != null || _overlayCoroutine != null)
            {
                dirty = true;
                break;
            }
        }
    }


    // ============================================================
    // TAB SELECTION
    // ============================================================

    private void OnTabClicked(int index)
    {
        if (_currentTabIndex == index)
            return;

        int oldIndex = _currentTabIndex;
        _currentTabIndex = index;

        AnimateWidths();

        if (oldIndex >= 0)
        {
            AnimateVisuals(oldIndex, false);
        }

        AnimateVisuals(index, true);

        if (index == 0) PopupController.Instance.Show<PopupLeague>();
        else if (index == 1) PopupController.Instance.Show<PopupHome>();

    }



    // ============================================================
    // FORCE STATE IMMEDIATE
    // ============================================================

    public void ForceStateImmediate(int index)
    {
        StopAllTransitions();

        _currentTabIndex = index;

        for (int i = 0; i < tabs.Count; i++)
        {
            bool isSelected = (i == index);

            float width = isSelected ? selectedWidth : normalWidth;
            SetWidthImmediate(i, width);

            SetVisualImmediate(i, isSelected);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(buttonLayoutRoot);
        LayoutRebuilder.ForceRebuildLayoutImmediate(underLayoutRoot);
        
        if (overlaySelected != null)
        {
            Vector2 pos = overlaySelected.anchoredPosition;
            overlaySelected.anchoredPosition = pos;
        }


    }


    private void SetWidthImmediate(int index, float width)
    {
        if (tabs[index].buttonLayout != null)
            tabs[index].buttonLayout.preferredWidth = width;

        if (tabs[index].underLayout != null)
            tabs[index].underLayout.preferredWidth = width;
    }


    private void SetVisualImmediate(int index, bool isSelected)
    {
        if (tabs[index].icon != null)
        {
            Vector2 pos = _originalIconAnchors[index];
            if (isSelected) pos.y += selectedIconYOffset;
            
            tabs[index].icon.anchoredPosition = pos;
            tabs[index].icon.localScale = Vector3.one * (isSelected ? selectedIconScale : 1f);
        }


        if (tabs[index].label != null)
        {
            Vector2 pos = _originalLabelAnchors[index];
            if (isSelected) pos.y += selectedLabelYOffset;
            
            tabs[index].label.anchoredPosition = pos;

            float alpha = isSelected ? 1f : 0f;
            _currentLabelAlpha[index] = alpha;

            if (_labelCanvasGroups[index] != null)
                _labelCanvasGroups[index].alpha = alpha;
                
            tabs[index].label.localScale = Vector3.one * (isSelected ? selectedLabelScale : 1f); 
        }
    }



    // ============================================================
    // STOPPING
    // ============================================================

    private void StopAllTransitions()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            if (_widthCoroutines[i] != null) StopCoroutine(_widthCoroutines[i]);

            if (_iconYCoroutines[i] != null) StopCoroutine(_iconYCoroutines[i]);
            if (_iconScaleCoroutines[i] != null) StopCoroutine(_iconScaleCoroutines[i]);

            if (_labelYCoroutines[i] != null) StopCoroutine(_labelYCoroutines[i]);
            if (_labelAlphaCoroutines[i] != null) StopCoroutine(_labelAlphaCoroutines[i]);
            if (_labelScaleCoroutines[i] != null) StopCoroutine(_labelScaleCoroutines[i]);
        }

        if (_overlayCoroutine != null) StopCoroutine(_overlayCoroutine);
    }



    // ============================================================
    // ANIMATE WIDTHS
    // ============================================================

    private void AnimateWidths()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            bool isSelected = (i == _currentTabIndex);
            float targetW = isSelected ? selectedWidth : normalWidth;

            if (_widthCoroutines[i] != null)
                StopCoroutine(_widthCoroutines[i]);

            _widthCoroutines[i] = StartCoroutine(
                WidthRoutine(i, targetW, widthDuration)
            );
        }
    }


    private IEnumerator WidthRoutine(int index, float targetW, float dur)
    {
        float startBtnW = tabs[index].buttonLayout != null
            ? tabs[index].buttonLayout.preferredWidth
            : targetW;

        float startUnderW = tabs[index].underLayout != null
            ? tabs[index].underLayout.preferredWidth
            : targetW;

        float time = 0f;

        while (time < dur)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / dur);

            float smoothedT = t * t * (3f - 2f * t);

            if (tabs[index].buttonLayout != null)
                tabs[index].buttonLayout.preferredWidth =
                    Mathf.Lerp(startBtnW, targetW, smoothedT);

            if (tabs[index].underLayout != null)
                tabs[index].underLayout.preferredWidth =
                    Mathf.Lerp(startUnderW, targetW, smoothedT);

            yield return null;
        }

        SetWidthImmediate(index, targetW);
        _widthCoroutines[index] = null;
    }



    // ============================================================
    // ANIMATE VISUALS (ICONS, LABELS)
    // ============================================================

    private void AnimateVisuals(int index, bool isSelected)
    {
        float dur = isSelected
            ? selectVisualDuration
            : deselectVisualDuration;


        if (_iconYCoroutines[index] != null) StopCoroutine(_iconYCoroutines[index]);
        if (_iconScaleCoroutines[index] != null) StopCoroutine(_iconScaleCoroutines[index]);

        if (tabs[index].icon != null)
        {
            float targetY = _originalIconAnchors[index].y +
                            (isSelected ? selectedIconYOffset : 0f);

            _iconYCoroutines[index] = StartCoroutine(
                LocalMoveYCurve(tabs[index].icon, targetY, dur, isSelected ? 4 : 5)
            );


            float targetScale = isSelected ? selectedIconScale : 1f;

            if (isSelected)
            {
                _iconScaleCoroutines[index] = StartCoroutine(
                    ScaleOvershootRoutine(tabs[index].icon, targetScale, dur,
                        selectedIconOvershootScale)
                );
            }
            else
            {
                _iconScaleCoroutines[index] = StartCoroutine(
                    ScaleCurveRoutine(tabs[index].icon, targetScale, dur, 1)
                );
            }
        }

        if (_labelYCoroutines[index] != null) StopCoroutine(_labelYCoroutines[index]);
        if (_labelAlphaCoroutines[index] != null) StopCoroutine(_labelAlphaCoroutines[index]);
        if (_labelScaleCoroutines[index] != null) StopCoroutine(_labelScaleCoroutines[index]);

        if (tabs[index].label != null)
        {
            float targetY = _originalLabelAnchors[index].y +
                            (isSelected ? selectedLabelYOffset : 0f);

            _labelYCoroutines[index] = StartCoroutine(
                LocalMoveYCurve(tabs[index].label, targetY, dur,
                    isSelected ? 4 : 5) 
            );

            float targetAlpha = isSelected ? 1f : 0f;
            _labelAlphaCoroutines[index] = StartCoroutine(
                AlphaRoutine(index, targetAlpha, dur)
            );
            
            float targetScale = isSelected ? selectedLabelScale : 1f; 
            _labelScaleCoroutines[index] = StartCoroutine(
                ScaleCurveRoutine(tabs[index].label, targetScale, dur, 1)
            );

        }
    }


    private IEnumerator LocalMoveYCurve(RectTransform rt, float targetY, float dur, int curveType)
    {
        if (rt == null) yield break;

        float startY = rt.anchoredPosition.y;
        float time = 0f;

        while (time < dur)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / dur);

            float easeT = GetEase(t, curveType);

            Vector2 pos = rt.anchoredPosition;
            pos.y = Mathf.Lerp(startY, targetY, easeT);
            rt.anchoredPosition = pos;

            yield return null;
        }

        Vector2 finalPos = rt.anchoredPosition;
        finalPos.y = targetY;
        rt.anchoredPosition = finalPos;
    }


    private IEnumerator ScaleCurveRoutine(RectTransform rt, float targetScale, float dur, int curveType)
    {
        if (rt == null) yield break;

        Vector3 startScale = rt.localScale;
        Vector3 finalScale = Vector3.one * targetScale;

        float time = 0f;
        while (time < dur)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / dur);

            float easeT = GetEase(t, curveType);
            rt.localScale = Vector3.LerpUnclamped(startScale, finalScale, easeT);

            yield return null;
        }

        rt.localScale = finalScale;
    }


    private IEnumerator ScaleOvershootRoutine(RectTransform rt, float finalTarget, float dur, float overshootPeak)
    {
        if (rt == null) yield break;

        Vector3 startScale = rt.localScale;
        Vector3 peakScale = Vector3.one * overshootPeak;
        Vector3 endScale = Vector3.one * finalTarget;

        float halfDur = dur * 0.6f; 
        float restDur = dur - halfDur;


        float t = 0f;
        while (t < halfDur)
        {
            t += Time.deltaTime;
            float pct = Mathf.Clamp01(t / halfDur);

            float easeSq = pct * (2f - pct); 
            rt.localScale = Vector3.LerpUnclamped(startScale, peakScale, easeSq);
            yield return null;
        }
        
        t = 0f;
        while (t < restDur)
        {
            t += Time.deltaTime;
            float pct = Mathf.Clamp01(t / restDur);

            float easeSq = pct * (2f - pct);
            rt.localScale = Vector3.LerpUnclamped(peakScale, endScale, easeSq);
            yield return null;
        }

        rt.localScale = endScale;
    }


    private IEnumerator AlphaRoutine(int index, float targetA, float dur)
    {
        float startA = _currentLabelAlpha[index];
        float time = 0f;

        CanvasGroup cg = _labelCanvasGroups[index];

        while (time < dur)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / dur);

            float smoothedT = t * t * (3f - 2f * t);
            float a = Mathf.Lerp(startA, targetA, smoothedT);

            _currentLabelAlpha[index] = a;

            if (cg != null) cg.alpha = a;

            yield return null;
        }

        _currentLabelAlpha[index] = targetA;
        if (cg != null) cg.alpha = targetA;

        _labelAlphaCoroutines[index] = null;
    }



    private float GetEase(float t, int curveType)
    {
        return t * t * (3f - 2f * t);
    }
}