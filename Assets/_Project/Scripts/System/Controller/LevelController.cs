using System;
using System.Threading.Tasks;
using CustomInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class LevelController : SingletonDontDestroy<LevelController>
{
    [SerializeField] private LevelConfig levelConfig;
    [SerializeField] private PictureCollectionConfig pictureCollection;
    public PictureCollectionConfig PictureCollection => pictureCollection;
    public PictureLevelEntry LoadedPictureEntry { get; private set; }
    [SerializeField] private string levelAddressFormat = "Level {0}";
    [Header("World level placement (assigned in GameplayScene)")]
    [SerializeField] private Transform levelRoot;
    [SerializeField] private Camera worldCamera;
    [ReadOnly] public Level currentLevel;

    private Task<Level> _prepareTask;
    private string _preparingKey;
    private string _loadedKey;
    private int _loadVersion;
    private bool _currentLevelIsAddressable;

    public void PrepareLevel()
    {
        _ = PrepareLevelAsync(true);
    }

    public Task<Level> PrepareLevelAsync(bool forceReload = true)
    {
        int playerLevel = Data.PlayerData.CurrentLevelIndex;
        int assetLevel = GetAssetLevelIndex(playerLevel);
        var entry = pictureCollection != null ? pictureCollection.FindLevel(assetLevel) : null;
        object address = entry != null && entry.IsPlayable ? entry.levelPrefab.RuntimeKey : string.Format(levelAddressFormat, assetLevel);
        return PrepareSelectedLevel(address, "main:" + playerLevel, playerLevel, entry, forceReload);
    }

    public Task<Level> PreparePictureLevelAsync(PictureLevelEntry entry, bool forceReload = false)
    {
        if (entry == null || !entry.IsPlayable) throw new ArgumentException("Picture level prefab is missing.");
        return PrepareSelectedLevel(entry.levelPrefab.RuntimeKey, "replay:" + entry.levelId, entry.levelNumber, entry, forceReload);
    }

    private Task<Level> PrepareSelectedLevel(object address, string key, int playerLevel, PictureLevelEntry entry, bool forceReload)
    {

        // Nhieu nut co the yeu cau load trong cung mot frame. Dung chung operation
        // dang chay de tranh instantiate cung mot level nhieu lan.
        if (_prepareTask != null && _preparingKey == key)
        {
            return _prepareTask;
        }

        if (!forceReload && currentLevel != null && _loadedKey == key)
        {
            return Task.FromResult(currentLevel);
        }

        int version = ++_loadVersion;
        _preparingKey = key;
        _prepareTask = GenerateLevelAsync(address, key, playerLevel, entry, version);
        return _prepareTask;
    }

    private async Task<Level> GenerateLevelAsync(object address, string key, int playerLevel, PictureLevelEntry entry, int version)
    {
        AsyncOperationHandle<GameObject> handle = default;
        GameObject instance = null;

        try
        {
            if (levelRoot == null || worldCamera == null)
                throw new InvalidOperationException("Assign Level Root and World Camera in GameplayScene.");
            handle = Addressables.InstantiateAsync(address, levelRoot, false);
            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                Debug.LogError($"[LevelController] Khong load duoc Addressable '{address}'. " +
                               "Hay kiem tra group Levels va Build > New Build > Default Build Script.");
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
                return null;
            }

            instance = handle.Result;

            // Neu nguoi choi da chuyen sang level khac trong luc dang load, bo ket qua cu.
            if (version != _loadVersion)
            {
                Addressables.ReleaseInstance(instance);
                return currentLevel;
            }

            Level loadedLevel = instance.GetComponent<Level>();
            if (loadedLevel == null)
            {
                Debug.LogError($"[LevelController] Prefab Addressable '{address}' khong co component Level.");
                Addressables.ReleaseInstance(instance);
                return null;
            }

            ReleaseCurrentLevel();

            currentLevel = loadedLevel;
            currentLevel.BindWorldCamera(worldCamera);
            _currentLevelIsAddressable = true;
            _loadedKey = key;
            LoadedPictureEntry = entry;
            currentLevel.gameObject.SetActive(false);
            currentLevel.name = playerLevel > levelConfig.maxLevel
                ? $"Level {playerLevel} - {currentLevel.name}"
                : $"Level {playerLevel}";

            return currentLevel;
        }
        catch (Exception exception)
        {
            if (instance != null)
            {
                Addressables.ReleaseInstance(instance);
            }
            else if (handle.IsValid())
            {
                Addressables.Release(handle);
            }

            Debug.LogError($"[LevelController] Load Addressable '{address}' that bai: {exception.Message}");
            return null;
        }
        finally
        {
            if (version == _loadVersion)
            {
                _prepareTask = null;
                _preparingKey = null;
            }
        }
    }

    private int GetAssetLevelIndex(int indexLevel)
    {
        if (indexLevel >= levelConfig.maxLevel)
        {
            switch (levelConfig.levelLoopType)
            {
                case LevelLoopType.Recycle:
                    indexLevel = (indexLevel - levelConfig.startLoopLevel) %
                        (levelConfig.maxLevel - levelConfig.startLoopLevel + 1) + levelConfig.startLoopLevel;
                    break;
                case LevelLoopType.Random:
                    indexLevel = UnityEngine.Random.Range(1, levelConfig.maxLevel + 1);
                    break;
            }
        }
        else
        {
            indexLevel = (indexLevel - 1) % levelConfig.maxLevel + 1;
        }

        return indexLevel;
    }

    private void ReleaseCurrentLevel()
    {
        if (currentLevel == null)
        {
            _currentLevelIsAddressable = false;
            return;
        }

        GameObject levelObject = currentLevel.gameObject;
        currentLevel = null;

        if (_currentLevelIsAddressable)
        {
            Addressables.ReleaseInstance(levelObject);
        }
        else
        {
            Destroy(levelObject);
        }

        _currentLevelIsAddressable = false;
        _loadedKey = null;
        LoadedPictureEntry = null;
    }

    private void OnDestroy()
    {
        ++_loadVersion;
        ReleaseCurrentLevel();
    }
}
