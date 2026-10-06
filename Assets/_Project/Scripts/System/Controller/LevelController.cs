using System;
using System.Threading.Tasks;
using CustomInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class LevelController : SingletonDontDestroy<LevelController>
{
    [SerializeField] private LevelConfig levelConfig;
    [SerializeField] private string levelAddressFormat = "Level {0}";
    [ReadOnly] public GameObject currentLevel;

    private Task<GameObject> _prepareTask;
    private int _preparingPlayerLevel = -1;
    private int _loadedPlayerLevel = -1;
    private int _loadVersion;
    private bool _currentLevelIsAddressable;

    public void PrepareLevel()
    {
        _ = PrepareLevelAsync(true);
    }

    public Task<GameObject> PrepareLevelAsync(bool forceReload = true)
    {
        int playerLevel = Data.PlayerData.CurrentLevelIndex;

        if (_prepareTask != null && _preparingPlayerLevel == playerLevel)
        {
            return _prepareTask;
        }

        if (!forceReload && currentLevel != null && _loadedPlayerLevel == playerLevel)
        {
            return Task.FromResult(currentLevel);
        }

        int version = ++_loadVersion;
        _preparingPlayerLevel = playerLevel;
        _prepareTask = GenerateLevelAsync(playerLevel, version);
        return _prepareTask;
    }

    private async Task<GameObject> GenerateLevelAsync(int playerLevel, int version)
    {
        await Task.Delay(10);
        ReleaseCurrentLevel();
        currentLevel = new GameObject("DummyLevel");
        _currentLevelIsAddressable = false;
        _loadedPlayerLevel = playerLevel;
        currentLevel.SetActive(false);
        return currentLevel;
    }

    private int GetAssetLevelIndex(int playerLevel)
    {
        if (levelConfig == null)
            return playerLevel;
        if (playerLevel <= levelConfig.maxLevel)
            return playerLevel;
        int loopLevelIndex = (playerLevel - levelConfig.maxLevel - 1) % levelConfig.loopLevels.Count;
        return levelConfig.loopLevels[loopLevelIndex];
    }

    public void DestroyCurrentLevel()
    {
        ReleaseCurrentLevel();
        _loadedPlayerLevel = -1;
    }

    private void ReleaseCurrentLevel()
    {
        if (currentLevel != null)
        {
            if (_currentLevelIsAddressable)
            {
                // Removed Addressables.ReleaseInstance to prevent issues
            }
            Destroy(currentLevel);
            currentLevel = null;
        }
        _currentLevelIsAddressable = false;
    }
}
