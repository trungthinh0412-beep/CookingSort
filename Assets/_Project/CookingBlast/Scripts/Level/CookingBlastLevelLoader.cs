using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace CookingSort2D
{
    [DisallowMultipleComponent]
    public sealed class CookingBlastLevelLoader : MonoBehaviour
    {
        private const string ProgressKey = "CookingBlast.CurrentLevel";

        [Header("References")]
        [SerializeField] private CookingBlastLevelConfig config;
        [SerializeField] private CookingSortGameController game;
        [SerializeField] private Transform levelRoot;

        [Header("Loading")]
        [SerializeField] private string addressFormat = "CookingBlast/Level {0}";
        [SerializeField] private bool loadOnStart = true;
        [SerializeField] private bool persistProgress = true;
        [SerializeField, Min(1)] private int defaultPlayerLevel = 1;

        private GameObject _currentInstance;
        private int _loadVersion;
        private bool _isLoading;

        public CookingBlastLevel CurrentLevel { get; private set; }
        public int CurrentPlayerLevel { get; private set; } = 1;
        public int CurrentAssetLevel { get; private set; } = 1;
        public bool IsLoading => _isLoading;

        public event Action<CookingBlastLevel, int, int> LevelLoaded;
        public event Action<int, string> LevelLoadFailed;

        private void Awake()
        {
            if (game == null) game = FindFirstObjectByType<CookingSortGameController>();
            if (levelRoot == null)
            {
                Transform found = transform.Find("LevelRoot");
                if (found != null) levelRoot = found;
            }

            CurrentPlayerLevel = persistProgress
                ? Mathf.Max(1, PlayerPrefs.GetInt(ProgressKey, defaultPlayerLevel))
                : Mathf.Max(1, defaultPlayerLevel);
        }

        private async void Start()
        {
            if (loadOnStart) await LoadCurrentLevelAsync();
        }

        public Task<bool> LoadCurrentLevelAsync()
        {
            return LoadLevelAsync(CurrentPlayerLevel);
        }

        public async Task<bool> LoadLevelAsync(int playerLevel)
        {
            if (config == null)
            {
                Fail(playerLevel, "CookingBlastLevelConfig is not assigned.");
                return false;
            }
            if (game == null)
            {
                Fail(playerLevel, "CookingSortGameController is not assigned.");
                return false;
            }
            if (levelRoot == null)
            {
                Fail(playerLevel, "LevelRoot is not assigned.");
                return false;
            }

            playerLevel = Mathf.Max(1, playerLevel);
            int assetLevel = config.ResolveLevelIndex(playerLevel);
            string address = string.Format(addressFormat, assetLevel);
            int version = ++_loadVersion;
            _isLoading = true;

            ReleaseCurrentLevel();
            game.SetLevelDefinition(null);

            AsyncOperationHandle<GameObject> handle = default;
            GameObject instance = null;
            try
            {
                handle = Addressables.InstantiateAsync(address, levelRoot, false);
                await handle.Task;

                if (version != _loadVersion)
                {
                    if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
                        Addressables.ReleaseInstance(handle.Result);
                    else if (handle.IsValid())
                        Addressables.Release(handle);
                    return false;
                }

                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    if (handle.IsValid()) Addressables.Release(handle);
                    Fail(playerLevel, $"Could not load Addressable '{address}'. Sync Cooking Blast Addressables first.");
                    return false;
                }

                instance = handle.Result;
                CookingBlastLevel loaded = instance.GetComponent<CookingBlastLevel>();
                if (loaded == null)
                {
                    Addressables.ReleaseInstance(instance);
                    Fail(playerLevel, $"Addressable '{address}' has no CookingBlastLevel component.");
                    return false;
                }

                if (!loaded.TryValidate(out string validationError))
                {
                    Addressables.ReleaseInstance(instance);
                    Fail(playerLevel, $"Level asset {assetLevel} is invalid: {validationError}");
                    return false;
                }

                _currentInstance = instance;
                CurrentLevel = loaded;
                CurrentPlayerLevel = playerLevel;
                CurrentAssetLevel = assetLevel;

                instance.name = playerLevel == assetLevel
                    ? $"Level {playerLevel}"
                    : $"Level {playerLevel} (Asset {assetLevel})";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                if (persistProgress)
                {
                    PlayerPrefs.SetInt(ProgressKey, CurrentPlayerLevel);
                    PlayerPrefs.Save();
                }

                game.SetLevelDefinition(loaded);
                game.StartLevel(CurrentPlayerLevel);
                LevelLoaded?.Invoke(loaded, CurrentPlayerLevel, CurrentAssetLevel);
                return true;
            }
            catch (Exception exception)
            {
                if (instance != null)
                    Addressables.ReleaseInstance(instance);
                else if (handle.IsValid())
                    Addressables.Release(handle);

                Fail(playerLevel, $"Load failed for '{address}': {exception.Message}");
                return false;
            }
            finally
            {
                if (version == _loadVersion) _isLoading = false;
            }
        }

        public async void ReloadCurrentLevel()
        {
            if (_isLoading) return;
            await LoadLevelAsync(CurrentPlayerLevel);
        }

        public async void CompleteAndLoadNextLevel()
        {
            if (_isLoading) return;
            await LoadLevelAsync(CurrentPlayerLevel + 1);
        }

        public async void LoadPreviousLevel()
        {
            if (_isLoading) return;
            await LoadLevelAsync(Mathf.Max(1, CurrentPlayerLevel - 1));
        }

        public async void LoadLevel(int playerLevel)
        {
            if (_isLoading) return;
            await LoadLevelAsync(playerLevel);
        }

        public void ResetProgress(int level = 1)
        {
            CurrentPlayerLevel = Mathf.Max(1, level);
            if (!persistProgress) return;
            PlayerPrefs.SetInt(ProgressKey, CurrentPlayerLevel);
            PlayerPrefs.Save();
        }

        private void ReleaseCurrentLevel()
        {
            CurrentLevel = null;
            if (_currentInstance == null) return;
            Addressables.ReleaseInstance(_currentInstance);
            _currentInstance = null;
        }

        private void Fail(int playerLevel, string message)
        {
            Debug.LogError($"[CookingBlastLevelLoader] {message}", this);
            LevelLoadFailed?.Invoke(playerLevel, message);
        }

        private void OnDestroy()
        {
            ++_loadVersion;
            ReleaseCurrentLevel();
        }
    }
}
