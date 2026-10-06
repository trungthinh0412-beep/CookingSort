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
    }
    
    public static void LoadData()
    {
        if (File.Exists(SavePath))
        {
            string encryptedData = File.ReadAllText(SavePath);
            try
            {
                // Try decrypting the data
                string jsonData = EncryptionHelper.Decrypt(encryptedData);
                PlayerData = JsonConvert.DeserializeObject<PlayerData>(jsonData);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Decryption failed. Attempting to read as plain text: " + ex.Message);
                try
                {
                    // Fallback to reading plain JSON data
                    PlayerData = JsonConvert.DeserializeObject<PlayerData>(encryptedData);
                }
                catch (System.Exception parseEx)
                {
                    Debug.LogError("Failed to parse data file. Creating a new one: " + parseEx.Message);
                    PlayerData = new PlayerData();
                }
            }
        }
        else
        {
            PlayerData = new PlayerData();
        }
    }

    public static void ClearData()
    {
        int previousStar = PlayerData?.Star ?? 0;
        
        0.ResetSeenStates();
        0.ResetSeenState();

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
public partial class PlayerData { public int Star; }
