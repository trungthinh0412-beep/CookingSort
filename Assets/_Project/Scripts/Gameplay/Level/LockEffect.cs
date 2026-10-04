using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LockEffect : MonoBehaviour
{
    [Header("Fade Targets")]
    [SerializeField] private List<SpriteRenderer> listSprite = new List<SpriteRenderer>();
    [SerializeField] private List<Image> listImage = new List<Image>();

    [Header("Fade Config")]
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.35f;
    [SerializeField] private AnimationCurve fadeCurve =
        AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    private readonly List<Color> _spriteAuthoredColors = new List<Color>();
    private readonly List<Color> _imageAuthoredColors = new List<Color>();
    private bool _hasCachedColors;

    public float FadeDuration => Mathf.Max(0.01f, fadeDuration);

    private void Awake()
    {
        CacheAuthoredColors();
    }

    public void RestoreAlpha()
    {
        EnsureColorCache();

        for (int i = 0; i < listSprite.Count; i++)
        {
            SpriteRenderer target = listSprite[i];
            if (target != null && i < _spriteAuthoredColors.Count)
                target.color = _spriteAuthoredColors[i];
        }

        for (int i = 0; i < listImage.Count; i++)
        {
            Image target = listImage[i];
            if (target != null && i < _imageAuthoredColors.Count)
                target.color = _imageAuthoredColors[i];
        }
    }

    public void SetFadeProgress(float progress)
    {
        EnsureColorCache();

        float normalizedProgress = Mathf.Clamp01(progress);
        float alphaMultiplier = fadeCurve != null && fadeCurve.length > 0
            ? Mathf.Clamp01(fadeCurve.Evaluate(normalizedProgress))
            : 1f - normalizedProgress;

        for (int i = 0; i < listSprite.Count; i++)
        {
            SpriteRenderer target = listSprite[i];
            if (target == null || i >= _spriteAuthoredColors.Count)
                continue;

            Color color = _spriteAuthoredColors[i];
            color.a *= alphaMultiplier;
            target.color = color;
        }

        for (int i = 0; i < listImage.Count; i++)
        {
            Image target = listImage[i];
            if (target == null || i >= _imageAuthoredColors.Count)
                continue;

            Color color = _imageAuthoredColors[i];
            color.a *= alphaMultiplier;
            target.color = color;
        }
    }

    private void EnsureColorCache()
    {
        if (!_hasCachedColors ||
            _spriteAuthoredColors.Count != listSprite.Count ||
            _imageAuthoredColors.Count != listImage.Count)
        {
            CacheAuthoredColors();
        }
    }

    private void CacheAuthoredColors()
    {
        _spriteAuthoredColors.Clear();
        _imageAuthoredColors.Clear();

        for (int i = 0; i < listSprite.Count; i++)
        {
            SpriteRenderer target = listSprite[i];
            _spriteAuthoredColors.Add(target != null ? target.color : Color.white);
        }

        for (int i = 0; i < listImage.Count; i++)
        {
            Image target = listImage[i];
            _imageAuthoredColors.Add(target != null ? target.color : Color.white);
        }

        _hasCachedColors = true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        fadeDuration = Mathf.Max(0.01f, fadeDuration);
    }
#endif

}
