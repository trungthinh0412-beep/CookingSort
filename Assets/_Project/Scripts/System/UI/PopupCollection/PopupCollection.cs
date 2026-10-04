using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopupCollection : Popup
{
    [Header("Collection Data")]
    [SerializeField] private CollectionConfig collectionConfig;
    [SerializeField] private RectTransform collectionContent;
    [SerializeField] private CollectionButton collectionButtonTemplate;

    [Header("Collection State")]
    [SerializeField] private GameObject unlockPopup;
    [SerializeField] private GameObject lockPopup;
    [SerializeField] private TMP_Text unlockMessageText;

    [Header("Shine Animation")]
    [SerializeField] private Image shine1;
    [SerializeField] private Image shine2;
    [Tooltip("Rotation speed in degrees per second. Opposite signs rotate in opposite directions.")]
    [SerializeField] private Vector2 shineRotationSpeed = new Vector2(12f, -9f);
    [SerializeField, Min(0.1f)] private float shinePulseDuration = 4f;
    [SerializeField, Range(0f, 0.2f)] private float shineScaleAmount = 0.04f;
    [SerializeField, Range(0f, 1f)] private float shineMinAlpha = 0.75f;

    [Header("Piggy Reward")]
    [SerializeField] private RectTransform rewardMedal;
    [SerializeField] private CustomButton piggyButton;
    [SerializeField] private ParticleSystem piggyGoldDustParticles;

    private ShineMotion _shineMotion1;
    private ShineMotion _shineMotion2;

    private readonly List<CollectionButton> _buttons = new List<CollectionButton>();
    private bool _cachedButtons;

    public CollectionConfig CollectionConfig => collectionConfig;

    protected override void OnEnable()
    {
        base.OnEnable();
        if (_shineMotion1 == null && shine1 != null)
            _shineMotion1 = new ShineMotion(shine1, 0f);
        if (_shineMotion2 == null && shine2 != null)
            _shineMotion2 = new ShineMotion(shine2, Mathf.PI);
        EnsureCollectionManager();
        EnsureTopbarOnTop();
        BindPiggyRewardButton();
        PlayPiggyGoldDust();
        CancelInvoke(nameof(PlayPiggyGoldDust));
        Invoke(nameof(PlayPiggyGoldDust), 0f);
        Observer.LevelChanged += OnLevelChanged;
        Observer.CollectionChanged += RefreshCollections;
        RefreshCollections();
    }

    protected override void OnDisable()
    {
        Observer.LevelChanged -= OnLevelChanged;
        Observer.CollectionChanged -= RefreshCollections;
        CancelInvoke(nameof(PlayPiggyGoldDust));
        UnbindPiggyRewardButton();
        if (piggyGoldDustParticles != null)
        {
            piggyGoldDustParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }
        base.OnDisable();
    }

    protected override void BeforeShow()
    {
        base.BeforeShow();
        EnsureTopbarOnTop();
        BindPiggyRewardButton();
        PlayPiggyGoldDust();
        RefreshCollections();
    }

    private void EnsureTopbarOnTop()
    {
        if (piggyButton == null)
            piggyButton = FindNamedComponent<CustomButton>("Btn_piggy");

        if (piggyButton != null)
        {
            Transform t = piggyButton.transform;
            while (t != null && t.parent != null && t.parent.name != "Unlock_Popup")
            {
                t = t.parent;
            }
            if (t != null && t.parent != null && t.parent.name == "Unlock_Popup")
            {
                t.SetAsLastSibling();
            }
        }
    }

    private void Update()
    {
        // Unscaled time keeps the UI moving while gameplay is paused.
        float deltaTime = Time.unscaledDeltaTime;
        _shineMotion1?.Tick(deltaTime, shineRotationSpeed.x, shinePulseDuration, shineScaleAmount, shineMinAlpha);
        _shineMotion2?.Tick(deltaTime, shineRotationSpeed.y, shinePulseDuration, shineScaleAmount, shineMinAlpha);
    }

    private sealed class ShineMotion
    {
        private readonly Image _image;
        private readonly RectTransform _rect;
        private readonly Quaternion _baseRotation;
        private readonly Vector3 _baseScale;
        private readonly Color _baseColor;
        private float _angle;
        private float _phase;

        public ShineMotion(Image image, float phase)
        {
            _image = image;
            _rect = image.rectTransform;
            _baseRotation = _rect.localRotation;
            _baseScale = _rect.localScale;
            _baseColor = image.color;
            _phase = phase;
            image.raycastTarget = false;
        }

        public void Tick(float deltaTime, float speed, float duration, float scaleAmount, float minAlpha)
        {
            if (_image == null || !_image.isActiveAndEnabled)
                return;

            _angle = Mathf.Repeat(_angle + speed * deltaTime, 360f);
            _phase = Mathf.Repeat(_phase + deltaTime * Mathf.PI * 2f / Mathf.Max(0.1f, duration), Mathf.PI * 2f);
            float pulse = (1f - Mathf.Cos(_phase)) * 0.5f;
            _rect.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, _angle);
            _rect.localScale = _baseScale * (1f + scaleAmount * pulse);
            Color color = _baseColor;
            color.a *= Mathf.Lerp(minAlpha, 1f, pulse);
            _image.color = color;
        }
    }

    private void OnLevelChanged(int level)
    {
        RefreshCollections();
    }

    private void RefreshCollections()
    {
        EnsureCollectionManager();
        PlayerData player = Data.PlayerData;
        int unlockLevel = collectionConfig == null ? 39 : collectionConfig.UnlockLevel;
        bool unlocked = CollectionManager.IsFeatureUnlocked;

        if (unlockPopup != null)
            unlockPopup.SetActive(unlocked);
        if (lockPopup != null)
            lockPopup.SetActive(!unlocked);
        if (unlockMessageText != null)
            unlockMessageText.text = $"Reach <color=#FFDB00>Level {unlockLevel}</color> to unlock Collections!";

        if (collectionConfig == null || collectionConfig.Collections == null || collectionContent == null)
            return;

        if (!_cachedButtons)
        {
            _buttons.AddRange(collectionContent.GetComponentsInChildren<CollectionButton>(true));
            _cachedButtons = true;
        }

        int count = collectionConfig.Collections.Count;
        while (_buttons.Count < count && collectionButtonTemplate != null)
            _buttons.Add(Instantiate(collectionButtonTemplate, collectionContent));

        for (int i = 0; i < _buttons.Count; i++)
        {
            CollectionData collection = i < count ? collectionConfig.Collections[i] : null;
            CollectionButton button = _buttons[i];
            button.Setup(collection, OpenCollection);
            button.Refresh(player, unlockLevel);
            button.gameObject.SetActive(collection != null);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(collectionContent);
    }

    public void OpenCollection(CollectionData collection)
    {
        EnsureCollectionManager();
        if (collection == null || collectionConfig == null ||
            !CollectionManager.IsCollectionUnlocked(collection.Id) ||
            collectionConfig.GetCollection(collection.Id) != collection)
            return;

        PopupController controller = PopupController.Instance;
        if (controller == null || !(controller.Get<PopupCollectionInfor>() is PopupCollectionInfor popup))
        {
            Debug.LogError("[PopupCollection] PopupCollectionInfor is missing from PopupConfig.", this);
            return;
        }

        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);
        popup.Setup(collection);
        controller.Show<PopupCollectionInfor>(PopupAnimation.ScaleFade);
    }

    public void OnClickPiggyReward()
    {
        Debug.Log("[PopupCollection] Clicked Piggy Button -> Showing PopupRewardsMedal");

        if (SoundController.Instance != null)
            SoundController.Instance.PlayFX(SoundName.ClickButton);

        // Make sure the effect is running even when this popup was restored
        // from an already active UI state.
        PlayPiggyGoldDust();

        PopupController controller = PopupController.Instance;
        if (controller == null)
        {
            Debug.LogError("[PopupCollection] PopupController.Instance is null!");
            return;
        }

        PopupRewardsMedal rewardsPopup =
            controller.Get<PopupRewardsMedal>() as PopupRewardsMedal;

        // PopupController builds its registry once during startup. When a
        // popup prefab is added or reimported while the editor is already in
        // Play Mode, refresh the registry once so the button still works
        // without requiring a second click or a manual scene restart.
        if (rewardsPopup == null)
        {
            controller.Initialize();
            rewardsPopup = controller.Get<PopupRewardsMedal>() as PopupRewardsMedal;
        }

        if (rewardsPopup == null)
        {
            Debug.LogWarning(
                "[PopupCollection] PopupRewardsMedal is missing from PopupConfig.",
                this
            );
            return;
        }

        if (rewardMedal == null)
            rewardMedal = FindNamedComponent<RectTransform>("Medal");

        rewardsPopup.Setup(rewardMedal);
        controller.Show<PopupRewardsMedal>(PopupAnimation.ScaleFade);
    }

    private void BindPiggyRewardButton()
    {
        if (piggyButton == null)
            piggyButton = FindNamedComponent<CustomButton>("Btn_piggy");

        if (piggyButton == null)
            return;

        EnsureTopbarOnTop();

        piggyButton.Interactable = true;
        piggyButton.Click.RemoveListener(OnClickPiggyReward);
        piggyButton.Click.AddListener(OnClickPiggyReward);

        EnsurePiggyGoldDustParticles();
    }

    private void UnbindPiggyRewardButton()
    {
        if (piggyButton != null)
            piggyButton.Click.RemoveListener(OnClickPiggyReward);
    }

    private void EnsurePiggyGoldDustParticles()
    {
        if (piggyGoldDustParticles == null)
            piggyGoldDustParticles = FindNamedComponent<ParticleSystem>(
                "Piggy_Gold_Dust_ParticleSystem"
            );

        if (piggyGoldDustParticles != null)
        {
            piggyGoldDustParticles.transform.SetAsFirstSibling();
        }
    }

    private void PlayPiggyGoldDust()
    {
        EnsurePiggyGoldDustParticles();

        if (piggyGoldDustParticles != null &&
            piggyGoldDustParticles.gameObject.activeInHierarchy &&
            !piggyGoldDustParticles.isPlaying)
            piggyGoldDustParticles.Play(true);
    }

    private void EnsureCollectionManager()
    {
        if (CollectionManager.Config != collectionConfig && collectionConfig != null)
            CollectionManager.Initialize(collectionConfig);
        Canvas.ForceUpdateCanvases();
        if (collectionContent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(collectionContent);
        }
    }

    public bool TryGetCollectionTarget(string collectionId, out RectTransform target)
    {
        target = null;
        if (!isActiveAndEnabled)
            return false;
        foreach (CollectionButton button in _buttons)
        {
            if (button.isActiveAndEnabled && button.CollectionData != null && button.CollectionData.Id == collectionId)
            {
                target = button.GetComponent<RectTransform>();
                return true;
            }
        }
        return false;
    }

    private T FindNamedComponent<T>(string objectName) where T : Component
    {
        T[] components = GetComponentsInChildren<T>(true);
        foreach (T component in components)
        {
            if (component != null && component.name == objectName)
                return component;
        }

        return null;
    }
}
