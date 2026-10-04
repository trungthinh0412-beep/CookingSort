using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

public static class Data 
{
    public static PlayerData PlayerData;
    public static string SavePath = Application.persistentDataPath + "/player_data.json";
    public static void SaveData()
    {
        var jsonSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
        string jsonData = JsonConvert.SerializeObject(PlayerData, Formatting.Indented, jsonSettings);

        // Encrypt the JSON data
        string encryptedData = EncryptionHelper.Encrypt(jsonData);
        File.WriteAllText(SavePath, encryptedData);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("<color=green>Save player data (encrypted) succeed</color>");
#endif
    }

    public static void LoadData()
    {
        if (File.Exists(SavePath))
        {
            string encryptedData = File.ReadAllText(SavePath);

            // Decrypt the data before loading
            string decryptedData = EncryptionHelper.Decrypt(encryptedData);
            PlayerData = JsonConvert.DeserializeObject<PlayerData>(decryptedData);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("<color=green>Load player data (decrypted) succeed</color>");
#endif
        }
        else
        {
            PlayerData = new PlayerData();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("<color=green>Create new player data ... </color>");
#endif
        }

        CollectionManager.ValidatePlayerProgress();
        Observer.CollectionChanged?.Invoke();
    }

    public static void ClearData()
    {
        int previousStar = PlayerData == null ? 0 : PlayerData.CurrentStar;

        StoryIntroState.ResetWatchedState();
        BonusTrayTutorialState.ResetSeenState();
        RewardTrayTutorialState.ResetSeenState();
        WildCardTutorialState.ResetSeenState();
        DowngradeCardTutorialState.ResetSeenState();
        IronCardTutorialState.ResetSeenState();
        DarkKingCardTutorialState.ResetSeenState();
        FrozenCardTutorialState.ResetSeenState();

        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("<color=green>Clear player data succeed </color>");
#endif
        }
        else
        {
            Debug.LogWarning("No save file found to delete!");
        }

        // Clearing only the file leaves the current PlayerData object alive
        // while Play Mode is running. Reset the in-memory state as well so
        // PopupKingdomBuild immediately reads every slot as NotBuilt.
        PlayerData = new PlayerData();

        if (previousStar > 0)
            Observer.StarChanged?.Invoke(-previousStar);

        Observer.StarChangedDone?.Invoke();
        Observer.CollectionChanged?.Invoke();
    }

    public static async Task UpdateData(string jsonContent)
    {
        string encryptedData = EncryptionHelper.Encrypt(jsonContent);
        await File.WriteAllTextAsync(SavePath, encryptedData);
        PlayerData = JsonConvert.DeserializeObject<PlayerData>(jsonContent);
        CollectionManager.ValidatePlayerProgress();
        Observer.CollectionChanged?.Invoke();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("<color=green>Update player data succeed </color>");
#endif
    }
}
