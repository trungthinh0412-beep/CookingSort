using System;
using System.Globalization;
using System.Threading.Tasks;
using CookingSort2D;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CommandPanelItem : ConsolePanelItem
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private Material validInputPreset;
    [SerializeField] private Material invalidInputPreset;

    protected override ConsoleTabType consoleTabType => ConsoleTabType.Command;

    public async void OnClickRun()
    {
        if (inputField == null)
        {
            ShowResult("Input field is not assigned.", false);
            return;
        }

        string input = inputField.text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(input))
        {
            ShowResult("Enter a command. Example: Level 1", false);
            return;
        }

        try
        {
            ParseCommand(input, out string command, out string parameter);
            bool succeeded = await ExecuteCommand(command, parameter);
            if (succeeded)
                ShowResult("Command executed successfully", true);
        }
        catch (Exception exception)
        {
            ShowResult(exception.Message, false);
            Debug.LogException(exception, this);
        }
    }

    private static void ParseCommand(
        string input,
        out string command,
        out string parameter)
    {
        string compact = input.Replace(" ", string.Empty);
        int parameterStart = 0;
        while (parameterStart < compact.Length &&
               (char.IsLetter(compact[parameterStart]) ||
                compact[parameterStart] == '_'))
        {
            parameterStart++;
        }

        command = compact.Substring(0, parameterStart).ToLowerInvariant();
        parameter = compact.Substring(parameterStart);
    }

    private async Task<bool> ExecuteCommand(string command, string parameter)
    {
        switch (command)
        {
            case "level":
                if (!TryParseInt(parameter, out int requestedLevel)) return false;
                return await LoadLevel(Mathf.Max(1, requestedLevel));

            case "n":
                return await LoadLevel(GetCurrentLevel() + 1);

            case "b":
                return await LoadLevel(Mathf.Max(1, GetCurrentLevel() - 1));

            case "gold":
                if (!TryParseInt(parameter, out int gold)) return false;
                RequirePlayerData();
                Data.PlayerData.CurrentGold += gold;
                Data.SaveData();
                return true;

            case "star":
                if (!TryParseInt(parameter, out int star)) return false;
                RequirePlayerData();
                Data.PlayerData.CurrentStar += star;
                Data.SaveData();
                return true;

            case "timescale":
                if (!float.TryParse(parameter, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out float timeScale))
                {
                    ShowResult("Invalid number. Example: Timescale 1", false);
                    return false;
                }

                Time.timeScale = Mathf.Max(0f, timeScale);
                return true;

            case "ts":
                Time.timeScale = 0f;
                return true;

            case "tr":
                Time.timeScale = 1f;
                return true;

            case "et":
                GameController.IsTesting = true;
                return true;

            case "dt":
                GameController.IsTesting = false;
                return true;

            default:
                ShowResult($"Command not found: {command}", false);
                return false;
        }
    }

    private async Task<bool> LoadLevel(int level)
    {
        RequirePlayerData();
        Data.PlayerData.CurrentLevelIndex = level;
        Data.SaveData();

        CookingBlastLevelLoader loader =
            FindFirstObjectByType<CookingBlastLevelLoader>();

        if (loader != null)
            return await loader.LoadLevelAsync(level);

        SceneManager.LoadScene("CookingBlastGameplay");
        return true;
    }

    private int GetCurrentLevel()
    {
        RequirePlayerData();
        return Data.PlayerData.CurrentLevelIndex;
    }

    private static void RequirePlayerData()
    {
        if (Data.PlayerData == null)
            throw new InvalidOperationException("Player data is not initialized.");
    }

    private bool TryParseInt(string value, out int result)
    {
        if (int.TryParse(value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out result))
            return true;

        ShowResult("Invalid number. Example: Level 1", false);
        return false;
    }

    private void ShowResult(string message, bool isValid)
    {
        if (resultText == null) return;

        Material preset = isValid ? validInputPreset : invalidInputPreset;
        if (preset != null) resultText.fontSharedMaterial = preset;
        resultText.text = message;
    }
}
