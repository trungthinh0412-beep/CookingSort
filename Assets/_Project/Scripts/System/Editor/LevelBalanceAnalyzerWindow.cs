#if UNITY_EDITOR || LEVEL_BALANCE_STANDALONE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
#if !LEVEL_BALANCE_STANDALONE
using UnityEditor;
#endif
using UnityEngine;

public sealed partial class LevelBalanceAnalyzerWindow
#if !LEVEL_BALANCE_STANDALONE
    : EditorWindow
#endif
{
    private enum BotSkill
    {
        Casual,
        Normal,
        Expert
    }

    // Simulator card codes use the visible number for regular cards and the
    // serialized CardType value + 1 for special cards. Dark King also carries
    // its remaining countdown in the code so cloned states stay self-contained.
    private const int WildCardCode = 21;
    private const int DowngradeCardCode = 25;
    private const int ChainCardCode = 26;
    private const int FrozenCardCode = 27;
    private const int IronCardCode = 28;
    private const int DarkKingCardCode = 29;
    private const int DarkKingCountdownBase = 1000;

#if !LEVEL_BALANCE_STANDALONE
    private GameObject levelPrefab;
    private int simulationCount = 5000;
    private int maximumMoves = 80;
    private int maximumDeals = 30;
    private int randomSeed = 12345;
    private BotSkill botSkill = BotSkill.Normal;

    private Vector2 windowScroll;
    private LevelSnapshot snapshot;
    private AnalysisResult result;
    private bool isRunning;
    private int completedSimulations;
    private string statusMessage;
    private string sampleTrace;
    private bool analysisComplete;
    private string analyzedSettings;
    private bool showInputSnapshot;

    [MenuItem("Tools/Solitaire/Level Balance Analyzer")]
    private static void Open()
    {
        GetWindow<LevelBalanceAnalyzerWindow>("Level Balance Analyzer");
    }

    private void OnDisable()
    {
        StopAnalysis();
    }

    private void OnGUI()
    {
        using (var scroll = new EditorGUILayout.ScrollViewScope(windowScroll, false, true))
        {
            windowScroll = scroll.scrollPosition;
            DrawWindowContents();
        }
    }

    private void DrawWindowContents()
    {
        EditorGUILayout.LabelField("Level Balance Analyzer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Keo prefab Level vao, tool se mo phong nhieu van va tinh % win " +
            "cho tung gioi han move. Ket qua la uoc luong theo bot, nen dung de " +
            "can bang level va sau do play-test lai bang nguoi that.",
            MessageType.Info
        );

        using (new EditorGUI.DisabledScope(isRunning))
        {
            levelPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Level Prefab",
                levelPrefab,
                typeof(GameObject),
                false
            );
            simulationCount = EditorGUILayout.IntSlider(
                "Simulation Count",
                simulationCount,
                100,
                50000
            );
            maximumMoves = EditorGUILayout.IntSlider(
                "Maximum Moves To Test",
                maximumMoves,
                5,
                200
            );
            maximumDeals = EditorGUILayout.IntSlider(
                "Simulation Deal Guard",
                maximumDeals,
                1,
                100
            );
            randomSeed = EditorGUILayout.IntField("Random Seed", randomSeed);
            botSkill = (BotSkill)EditorGUILayout.EnumPopup("Bot Skill", botSkill);
        }

        EditorGUILayout.Space(6f);

        if (isRunning)
        {
            float progress = simulationCount > 0
                ? completedSimulations / (float)simulationCount
                : 0f;
            Rect progressRect = GUILayoutUtility.GetRect(18f, 22f);
            EditorGUI.ProgressBar(
                progressRect,
                progress,
                $"{completedSimulations:N0} / {simulationCount:N0}"
            );

            if (GUILayout.Button("Cancel"))
            {
                statusMessage =
                    $"Cancelled after {completedSimulations:N0} simulations.";
                StopAnalysis();
            }
        }
        else if (GUILayout.Button("Analyze Level", GUILayout.Height(30f)))
        {
            BeginAnalysis();
        }

        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.HelpBox(
                statusMessage,
                result != null ? MessageType.None : MessageType.Warning
            );
        }

        if (result != null)
            DrawResults();
    }

    private void DrawResults()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
        if (!IsSnapshotCurrent())
            EditorGUILayout.HelpBox("Level/config or test settings changed. Analyze again before applying a recommendation.", MessageType.Warning);

        int recommended = result.FindMinimumMovesForBestWinRate();
        int currentMoves = snapshot != null ? snapshot.ConfiguredMoves : 0;
        float currentRate = result.GetWinRate(currentMoves);
        float maxRate = result.GetWinRate(result.MaximumMoves);

        EditorGUILayout.LabelField("Level", snapshot != null ? snapshot.Name : "-");
        showInputSnapshot = EditorGUILayout.Foldout(showInputSnapshot, "Initial board / deal config", true);
        if (snapshot != null && showInputSnapshot)
            EditorGUILayout.HelpBox(snapshot.Summary, MessageType.Info);
        EditorGUILayout.HelpBox(
            "Baseline: no boosters, coins, ads or continue. Deals are free. " +
            "These are bot results, not proven player win rates. Deal guard / bot stalled " +
            "mean the search stopped, not that the level is impossible.", MessageType.Info);
        EditorGUILayout.LabelField("Current Moves", currentMoves.ToString());
        EditorGUILayout.LabelField(
            "Win Rate At Current Moves",
            currentMoves <= result.MaximumMoves
                ? FormatRateWithConfidence(currentRate, result.CompletedSimulations)
                : "Outside tested range"
        );
        EditorGUILayout.LabelField(
            "Recommended Moves",
            recommended >= 0
                ? $"{recommended} ({maxRate * 100f:0.00}% - best in tested range)"
                : "No wins in this test; no recommendation"
        );
        EditorGUILayout.LabelField(
            "Highest Win Rate In Test Range",
            FormatRateWithConfidence(maxRate, result.CompletedSimulations)
        );
        EditorGUILayout.HelpBox(
            "Recommendation = fewest moves reaching the highest observed win rate in the tested range.",
            MessageType.None);
        EditorGUILayout.LabelField(
            "Average Deals (all simulations)",
            result.AverageDeals.ToString("0.00")
        );
        EditorGUILayout.LabelField(
            "Average Moves (winning simulations)",
            result.AverageWinningMoves > 0f
                ? result.AverageWinningMoves.ToString("0.00")
                : "-"
        );

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Bot win-rate bands (not player difficulty)", EditorStyles.boldLabel);
        DrawDifficultyRange("Lower", 0.55f, 0.69f);
        DrawDifficultyRange("Middle", 0.70f, 0.84f);
        DrawDifficultyRange("Higher", 0.85f, 0.95f);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Failure reasons", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Deadlock / full board", result.DeadlockCount.ToString("N0"));
        EditorGUILayout.LabelField("Reached move test limit", result.MoveLimitCount.ToString("N0"));
        EditorGUILayout.LabelField("Reached deal guard", result.DealLimitCount.ToString("N0"));
        EditorGUILayout.LabelField("Bot stalled / repeated states", result.BotStalledCount.ToString("N0"));

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(recommended < 1 || isRunning ||
                       !analysisComplete || !IsSnapshotCurrent()))
            {
                if (GUILayout.Button("Apply Recommended Moves"))
                    ApplyRecommendedMoves(recommended);
            }

            if (GUILayout.Button("Copy Table"))
                EditorGUIUtility.systemCopyBuffer = BuildResultTableText();
            if (GUILayout.Button("Copy First Run"))
                EditorGUIUtility.systemCopyBuffer = sampleTrace ?? "No trace yet.";
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Win rate by move budget", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            GUILayout.Label("Moves", EditorStyles.boldLabel, GUILayout.Width(55f));
            GUILayout.Label("Wins", EditorStyles.boldLabel, GUILayout.Width(80f));
            GUILayout.Label("Win Rate", EditorStyles.boldLabel, GUILayout.Width(100f));
            GUILayout.Label("Gain", EditorStyles.boldLabel, GUILayout.Width(80f));
        }

        float previousRate = 0f;
        for (int move = 1; move <= result.MaximumMoves; move++)
        {
            float rate = result.GetWinRate(move);
            float gain = rate - previousRate;
            bool highlight = move == currentMoves || move == recommended;

            Color oldColor = GUI.backgroundColor;
            if (highlight)
            {
                GUI.backgroundColor = move == recommended
                    ? new Color(0.55f, 1f, 0.65f)
                    : new Color(0.65f, 0.85f, 1f);
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(move.ToString(), GUILayout.Width(55f));
                GUILayout.Label(
                    result.GetCumulativeWins(move).ToString("N0"),
                    GUILayout.Width(80f)
                );
                GUILayout.Label($"{rate * 100f:0.00}%", GUILayout.Width(100f));
                GUILayout.Label($"+{gain * 100f:0.00}%", GUILayout.Width(80f));
            }

            GUI.backgroundColor = oldColor;
            previousRate = rate;
        }

    }

    private void DrawDifficultyRange(string label, float lowRate, float highRate)
    {
        int low = result.FindFirstMoveAtRate(lowRate);
        int high = result.FindFirstMoveAtRate(highRate);
        string value;

        if (low < 0)
            value = "Not reached";
        else if (high < 0)
            value = $"From {low} moves; upper rate not reached";
        else
            value = $"{low} - {high} moves";

        EditorGUILayout.LabelField(
            $"{label} ({lowRate * 100f:0}-{highRate * 100f:0}%)",
            value
        );
    }

    private void BeginAnalysis()
    {
        StopAnalysis();
        result = null;
        analysisComplete = false;
        statusMessage = null;

        if (!TryReadLevelSnapshot(levelPrefab, out snapshot, out string error))
        {
            statusMessage = error;
            return;
        }

        simulationCount = Mathf.Max(1, simulationCount);
        maximumMoves = Mathf.Max(1, maximumMoves);
        maximumDeals = Mathf.Max(1, maximumDeals);
        analyzedSettings = SettingsSignature();
        completedSimulations = 0;
        result = new AnalysisResult(maximumMoves);
        sampleTrace = null;
        isRunning = true;
        statusMessage =
            $"Analyzing {snapshot.Name}: {snapshot.Trays.Count} trays, " +
            $"{snapshot.Targets.Count} targets...";
        EditorApplication.update += RunAnalysisBatch;
    }

    private void RunAnalysisBatch()
    {
        if (!isRunning || snapshot == null || result == null)
            return;

        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            while (completedSimulations < simulationCount &&
                   stopwatch.ElapsedMilliseconds < 12)
            {
                int seed = unchecked(
                    randomSeed + completedSimulations * 486187739
                );
                StringBuilder trace = completedSimulations == 0 ? new StringBuilder(snapshot.Summary) : null;
                SimulationOutcome outcome = LevelSimulator.Run(
                    snapshot,
                    maximumMoves,
                    maximumDeals,
                    botSkill,
                    seed,
                    trace
                );
                if (trace != null)
                {
                    trace.AppendLine($"Outcome: {(outcome.Won ? "WIN" : outcome.Failure.ToString())}; actual moves={outcome.Moves}; required budget={outcome.RequiredMoveBudget}; deals={outcome.Deals}");
                    sampleTrace = trace.ToString();
                }
                result.Add(outcome);
                completedSimulations++;
            }
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogException(exception);
            statusMessage = "Simulation failed: " + exception.Message;
            StopAnalysis();
            result = null;
            Repaint();
            return;
        }

        if (completedSimulations >= simulationCount)
        {
            result.Complete();
            analysisComplete = true;
            statusMessage =
                $"Finished {completedSimulations:N0} simulations. " +
                "Blue row = current moves, green row = recommended moves.";
            StopAnalysis();
        }

        Repaint();
    }

    private void StopAnalysis()
    {
        if (isRunning)
            EditorApplication.update -= RunAnalysisBatch;

        isRunning = false;
    }

    private void ApplyRecommendedMoves(int recommended)
    {
        if (levelPrefab == null || recommended < 1 || !analysisComplete || !IsSnapshotCurrent())
            return;

        string path = AssetDatabase.GetAssetPath(levelPrefab);
        if (string.IsNullOrEmpty(path))
        {
            statusMessage = "Level must be a prefab asset.";
            return;
        }

        GameObject contents = null;
        try
        {
            contents = PrefabUtility.LoadPrefabContents(path);
            Level level = contents.GetComponentInChildren<Level>(true);
            if (level == null)
            {
                statusMessage = "Prefab does not contain a Level component.";
                return;
            }

            SerializedObject serializedLevel = new SerializedObject(level);
            SerializedProperty moveProperty =
                serializedLevel.FindProperty("maxMoveCount");
            if (moveProperty == null)
            {
                statusMessage = "Cannot find Level.maxMoveCount.";
                return;
            }

            moveProperty.intValue = recommended;
            serializedLevel.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            AssetDatabase.SaveAssets();
            snapshot.ConfiguredMoves = recommended;
            snapshot.DependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString();
            statusMessage = $"Applied {recommended} moves to {levelPrefab.name}.";
        }
        finally
        {
            if (contents != null)
                PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private bool IsSnapshotCurrent()
    {
        return snapshot != null && levelPrefab != null &&
               analyzedSettings == SettingsSignature() &&
               AssetDatabase.GetAssetPath(levelPrefab) == snapshot.AssetPath &&
               AssetDatabase.GetAssetDependencyHash(snapshot.AssetPath).ToString() == snapshot.DependencyHash;
    }

    private string SettingsSignature() =>
        $"{simulationCount}|{maximumMoves}|{maximumDeals}|{randomSeed}|{botSkill}";

    private string BuildResultTableText()
    {
        if (result == null)
            return string.Empty;

        StringBuilder builder = new StringBuilder();
        builder.AppendLine(snapshot.Summary);
        builder.AppendLine($"Settings (count|max moves|deal guard|seed|skill): {analyzedSettings}; completed: {result.CompletedSimulations}");
        builder.AppendLine("Bot estimate; no boosters/continue. Guard/stalled results are inconclusive.");
        builder.AppendLine("Moves\tWins\tWin Rate\tGain");
        float previousRate = 0f;

        for (int move = 1; move <= result.MaximumMoves; move++)
        {
            float rate = result.GetWinRate(move);
            builder.Append(move).Append('\t')
                .Append(result.GetCumulativeWins(move)).Append('\t')
                .Append((rate * 100f).ToString("0.00")).Append("%\t")
                .Append(((rate - previousRate) * 100f).ToString("0.00"))
                .AppendLine("%");
            previousRate = rate;
        }

        return builder.ToString();
    }

    private static string FormatRateWithConfidence(float rate, int sampleCount)
    {
        if (sampleCount <= 0)
            return "-";

        double z2 = 1.96 * 1.96;
        double denominator = 1.0 + z2 / sampleCount;
        double center = (rate + z2 / (2 * sampleCount)) / denominator;
        double half = 1.96 * Math.Sqrt(rate * (1.0 - rate) / sampleCount +
            z2 / (4.0 * sampleCount * sampleCount)) / denominator;
        return $"{rate * 100f:0.00}% (95% CI: {(center - half) * 100:0.00}-{(center + half) * 100:0.00}%)";
    }

    private static bool TryReadLevelSnapshot(
        GameObject prefab,
        out LevelSnapshot levelSnapshot,
        out string error)
    {
        levelSnapshot = null;
        error = null;

        if (prefab == null)
        {
            error = "Please assign a Level prefab.";
            return false;
        }

        string path = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(path) ||
            PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.NotAPrefab)
        {
            error = "Level must be a prefab asset from the Project window.";
            return false;
        }

        GameObject contents = null;
        try
        {
            contents = PrefabUtility.LoadPrefabContents(path);
            Level level = contents.GetComponentInChildren<Level>(true);
            if (level == null)
            {
                error = "The assigned prefab does not contain a Level component.";
                return false;
            }

            levelSnapshot = new LevelSnapshot
            {
                Name = prefab.name,
                AssetPath = path,
                DependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString()
            };

            SerializedObject serializedLevel = new SerializedObject(level);
            SerializedProperty moveProperty =
                serializedLevel.FindProperty("maxMoveCount");
            levelSnapshot.ConfiguredMoves = moveProperty != null
                ? Mathf.Max(1, moveProperty.intValue)
                : 30;
            SerializedProperty minDealValueProperty =
                serializedLevel.FindProperty("minDealCardValue");
            int configuredMinDealValue = minDealValueProperty != null
                ? Mathf.Clamp(minDealValueProperty.intValue, 0, 20)
                : 0;

            SerializedProperty targetsProperty =
                serializedLevel.FindProperty("cardTargets");
            if (targetsProperty != null && targetsProperty.isArray)
            {
                for (int i = 0; i < targetsProperty.arraySize; i++)
                {
                    SerializedProperty target =
                        targetsProperty.GetArrayElementAtIndex(i);
                    SerializedProperty type = target.FindPropertyRelative("cardType");
                    SerializedProperty count = target.FindPropertyRelative("count");

                    if (type == null || count == null)
                        throw new InvalidOperationException($"Target {i + 1} has missing fields.");
                    if (type.intValue < 0 || type.intValue >= 20 || count.intValue < 1)
                        throw new InvalidOperationException($"Target {i + 1} has an invalid value/count.");

                    levelSnapshot.Targets.Add(new TargetDefinition
                    {
                        Value = Mathf.Clamp(type.intValue + 1, 1, 20),
                        Count = Mathf.Max(1, count.intValue)
                    });
                }
            }

            if (levelSnapshot.Targets.Count == 0)
            {
                error = "Level has no card targets.";
                return false;
            }

            int defaultDarkKingMoves = level.CardConfig != null
                ? level.CardConfig.DefaultDarkKingMovesBeforeTrayLock
                : 3;
            levelSnapshot.DefaultDarkKingMoves = defaultDarkKingMoves;

            CardSlotHolder[] holders =
                level.GetComponentsInChildren<CardSlotHolder>(true);
            for (int i = 0; i < holders.Length; i++)
            {
                TrayDefinition tray = ReadTray(holders[i], defaultDarkKingMoves);
                if (tray != null && tray.Capacity > 0)
                    levelSnapshot.Trays.Add(tray);
            }

            if (levelSnapshot.Trays.Count == 0)
            {
                error = "Level has no CardSlotHolder with slots.";
                return false;
            }

            CardDesk desk = level.GetComponentInChildren<CardDesk>(true);
            CardDeskConfig deskConfig = null;
            if (desk != null)
            {
                SerializedObject serializedDesk = new SerializedObject(desk);
                SerializedProperty configProperty =
                    serializedDesk.FindProperty("config");
                if (configProperty != null)
                    deskConfig = configProperty.objectReferenceValue as CardDeskConfig;
            }

            levelSnapshot.Deal = ReadDealSettings(deskConfig);
            levelSnapshot.Deal.MinimumValue = configuredMinDealValue;
            StringBuilder summary = new StringBuilder();
            summary.AppendLine($"{prefab.name} | Moves: {levelSnapshot.ConfiguredMoves}");
            summary.AppendLine(
                $"Min Value Deal Card: " +
                $"{(configuredMinDealValue == 0 ? "Auto" : configuredMinDealValue.ToString())}"
            );
            foreach (TargetDefinition target in levelSnapshot.Targets)
                summary.AppendLine($"Target: Card{target.Value} x{target.Count}");
            for (int i = 0; i < levelSnapshot.Trays.Count; i++)
            {
                TrayDefinition tray = levelSnapshot.Trays[i];
                summary.AppendLine($"Tray {i + 1}: {tray.Rule}, {(tray.Locked ? "LOCKED" : "open")}, " +
                    $"{tray.InitialCards.Count}/{tray.Capacity} cards [bottom -> top: " +
                    $"{string.Join(",", tray.InitialCards.Select(FormatCardCode))}]");
            }
            summary.AppendLine(deskConfig != null
                ? $"Deal config: {AssetDatabase.GetAssetPath(deskConfig)}; dynamic tray limit 1-4, skip chance {levelSnapshot.Deal.LeaveOneTrayEmptyChance:P0}, three-value chance {levelSnapshot.Deal.AllowThreeValuesPerTrayChance:P0}"
                : "No deal config: dealing disabled, as in gameplay.");
            levelSnapshot.Summary = summary.ToString();
            return true;
        }
        catch (Exception exception)
        {
            error = "Cannot read level prefab: " + exception.Message;
            return false;
        }
        finally
        {
            if (contents != null)
                PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static TrayDefinition ReadTray(
        CardSlotHolder holder,
        int defaultDarkKingMoves)
    {
        if (holder == null)
            return null;

        SerializedObject serializedHolder = new SerializedObject(holder);
        SerializedProperty slotsProperty =
            serializedHolder.FindProperty("cardSlots");
        int capacity = slotsProperty != null && slotsProperty.isArray && slotsProperty.arraySize > 0
            ? slotsProperty.arraySize
            : holder.GetComponentsInChildren<CardSlot>().Length;

        TrayDefinition tray = new TrayDefinition
        {
            Name = holder.name,
            Capacity = Mathf.Max(0, capacity),
            Locked = ReadBool(serializedHolder, "isLocked", false),
            Rule = TrayRule.Normal,
            MinimumValue = 1
        };

        // cardTypes is only the editor's spawn recipe. Gameplay reads CardSlot.Card.
        IList<CardSlot> slots = holder.CardSlots;
        if (slots == null || slots.Count == 0)
            slots = holder.GetComponentsInChildren<CardSlot>();
        bool foundEmpty = false;
        for (int i = 0; i < slots.Count; i++)
        {
            CardSlot slot = slots[i];
            if (slot == null)
                throw new InvalidOperationException($"{holder.name}: missing slot reference at {i}. Fix the tray before analyzing.");
            if (slot.Card == null)
            {
                foundEmpty = true;
                continue;
            }
            if (foundEmpty)
                throw new InvalidOperationException($"{holder.name}: cards have empty slots between them. This simulator requires contiguous starting cards; it will not silently reorder them.");
            int cardCode = (int)slot.Card.CardType + 1;
            if (!IsSupportedStartingCardCode(cardCode))
                throw new InvalidOperationException($"{holder.name}: unsupported starting card {slot.Card.CardType}.");
            if (cardCode == DarkKingCardCode)
            {
                int countdown = slot.Card.DarkKingMovesBeforeTrayLock > 0
                    ? slot.Card.DarkKingMovesBeforeTrayLock
                    : defaultDarkKingMoves;
                cardCode = EncodeDarkKing(countdown);
            }
            tray.InitialCards.Add(cardCode);
        }

        CardSlotHolderFrozen frozen = holder.GetComponent<CardSlotHolderFrozen>();
        CardSlotHolderHighValue highValue = holder.GetComponent<CardSlotHolderHighValue>();
        CardSlotHolderWild wild = holder.GetComponent<CardSlotHolderWild>();
        CardSlotHolderGold gold = holder.GetComponent<CardSlotHolderGold>();
        CardSlotHolderBonus bonus = holder.GetComponent<CardSlotHolderBonus>();

        if (frozen != null)
        {
            SerializedObject serializedFrozen = new SerializedObject(frozen);
            tray.Rule = TrayRule.Frozen;
            tray.FrozenStacksRemaining = Mathf.Max(
                0,
                ReadInt(serializedFrozen, "requiredSuccessfulStacks", 2)
            );
            tray.Locked = tray.FrozenStacksRemaining > 0;
        }
        else if (highValue != null)
        {
            tray.Rule = TrayRule.HighValue;
            tray.MinimumValue = Mathf.Clamp(highValue.MinimumCardValue, 1, 20);
        }
        else if (wild != null)
        {
            tray.Rule = TrayRule.Wild;
        }
        else if (gold != null)
        {
            SerializedObject serializedGold = new SerializedObject(gold);
            tray.Rule = TrayRule.Gold;
            bool purchased = ReadBool(serializedGold, "isPurchased", false);
            bool boosterUnlocked = ReadBool(
                serializedGold,
                "isUnlockedByPreLevelBooster",
                false
            );
            tray.Locked = !purchased && !boosterUnlocked;
        }
        else if (bonus != null || holder.name == "CardSlotHolderBonus")
        {
            tray.Rule = TrayRule.Bonus;
            int remainingDraws = 3;
            if (bonus != null)
            {
                SerializedObject serializedBonus = new SerializedObject(bonus);
                remainingDraws = Mathf.Max(
                    0,
                    ReadInt(serializedBonus, "remainingDraws", 3)
                );
            }
            tray.Locked = remainingDraws > 0;
        }

        return tray;
    }

    private static bool IsSupportedStartingCardCode(int cardCode)
    {
        return IsNumberCardCode(cardCode) ||
               cardCode == WildCardCode ||
               cardCode == DowngradeCardCode ||
               cardCode == ChainCardCode ||
               cardCode == FrozenCardCode ||
               cardCode == IronCardCode ||
               cardCode == DarkKingCardCode;
    }

    private static DealSettings ReadDealSettings(CardDeskConfig config)
    {
        DealSettings settings = new DealSettings();

        if (config == null)
        {
            return settings;
        }

        settings.Enabled = true;
        settings.LeaveOneTrayEmptyChance = Mathf.Clamp01(
            config.leaveOneTrayEmptyChance
        );
        settings.AllowThreeValuesPerTrayChance = Mathf.Clamp01(
            config.allowThreeValuesPerTrayChance
        );
        if (config.dealWeightProfiles != null)
        {
            for (int i = 0; i < config.dealWeightProfiles.Count; i++)
            {
                DealWeightProfile profile = config.dealWeightProfiles[i];
                if (profile == null)
                    continue;

                List<float> weights = new List<float>();
                if (profile.weights != null)
                {
                    for (int j = 0; j < profile.weights.Count; j++)
                        weights.Add(Mathf.Max(0f, profile.weights[j]));
                }
                if (!settings.Profiles.ContainsKey(profile.lowerValueCount))
                    settings.Profiles.Add(profile.lowerValueCount, weights);
            }
        }

        return settings;
    }

    private static int ReadInt(
        SerializedObject serializedObject,
        string propertyName,
        int fallback)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);
        return property != null ? property.intValue : fallback;
    }

    private static bool ReadBool(
        SerializedObject serializedObject,
        string propertyName,
        bool fallback)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);
        return property != null ? property.boolValue : fallback;
    }

#endif
    private static bool IsNumberCardCode(int cardCode) =>
        cardCode >= 1 && cardCode <= 20;

    private static bool IsDarkKingCode(int cardCode) =>
        cardCode >= DarkKingCountdownBase;

    private static int EncodeDarkKing(int movesRemaining) =>
        DarkKingCountdownBase + Mathf.Max(1, movesRemaining);

    private static int GetCardKind(int cardCode) =>
        IsDarkKingCode(cardCode) ? DarkKingCardCode : cardCode;

    private static string FormatCardCode(int cardCode)
    {
        if (IsNumberCardCode(cardCode))
            return "Card" + cardCode;
        if (IsDarkKingCode(cardCode))
            return "DarkingCard(" + (cardCode - DarkKingCountdownBase) + ")";

        switch (cardCode)
        {
            case WildCardCode: return "WildCard";
            case DowngradeCardCode: return "DowngradeCard";
            case ChainCardCode: return "ChainCard";
            case FrozenCardCode: return "FrozenCard";
            case IronCardCode: return "IronCard";
            default: return "Unknown(" + cardCode + ")";
        }
    }

    private enum TrayRule
    {
        Normal,
        Frozen,
        HighValue,
        Wild,
        Gold,
        Bonus
    }

    private sealed class LevelSnapshot
    {
        public string Name;
        public string AssetPath;
        public string DependencyHash;
        public string Summary;
        public int ConfiguredMoves;
        public int DefaultDarkKingMoves = 3;
        public readonly List<TrayDefinition> Trays = new List<TrayDefinition>();
        public readonly List<TargetDefinition> Targets = new List<TargetDefinition>();
        public DealSettings Deal;
    }

    private sealed class TrayDefinition
    {
        public string Name;
        public int Capacity;
        public bool Locked;
        public TrayRule Rule;
        public int MinimumValue;
        public int FrozenStacksRemaining;
        public readonly List<int> InitialCards = new List<int>();
    }

    private sealed class TargetDefinition
    {
        public int Value;
        public int Count;
    }

    private sealed class DealSettings
    {
        public bool Enabled;
        public int MinimumValue;
        public float LeaveOneTrayEmptyChance;
        public float AllowThreeValuesPerTrayChance;
        public readonly Dictionary<int, List<float>> Profiles =
            new Dictionary<int, List<float>>();

        public void AddDefaultProfiles()
        {
            Profiles[5] = new List<float> { 5f, 15f, 20f, 25f, 35f };
            Profiles[4] = new List<float> { 15f, 20f, 25f, 30f };
            Profiles[3] = new List<float> { 20f, 30f, 50f };
            Profiles[2] = new List<float> { 30f, 70f };
        }
    }

    private enum FailureReason
    {
        None,
        Deadlock,
        MoveLimit,
        DealLimit,
        BotStalled
    }

    private struct SimulationOutcome
    {
        public bool Won;
        public int Moves;
        public int RequiredMoveBudget;
        public int Deals;
        public FailureReason Failure;
    }

    private sealed class AnalysisResult
    {
        private readonly int[] exactWinsByMove;
        private int[] cumulativeWinsByMove;
        private long totalDeals;
        private long totalWinningMoves;
        private int winningCount;

        public int MaximumMoves { get; }
        public int CompletedSimulations { get; private set; }
        public int DeadlockCount { get; private set; }
        public int MoveLimitCount { get; private set; }
        public int DealLimitCount { get; private set; }
        public int BotStalledCount { get; private set; }
        public float AverageDeals => CompletedSimulations > 0
            ? totalDeals / (float)CompletedSimulations
            : 0f;
        public float AverageWinningMoves => winningCount > 0
            ? totalWinningMoves / (float)winningCount
            : 0f;

        public AnalysisResult(int maximumMoves)
        {
            MaximumMoves = Mathf.Max(1, maximumMoves);
            exactWinsByMove = new int[MaximumMoves + 1];
        }

        public void Add(SimulationOutcome outcome)
        {
            CompletedSimulations++;
            totalDeals += outcome.Deals;

            if (outcome.Won)
            {
                int moves = Mathf.Clamp(outcome.RequiredMoveBudget, 0, MaximumMoves);
                exactWinsByMove[moves]++;
                winningCount++;
                totalWinningMoves += outcome.Moves;
                return;
            }

            switch (outcome.Failure)
            {
                case FailureReason.Deadlock:
                    DeadlockCount++;
                    break;
                case FailureReason.DealLimit:
                    DealLimitCount++;
                    break;
                case FailureReason.BotStalled:
                    BotStalledCount++;
                    break;
                default:
                    MoveLimitCount++;
                    break;
            }
        }

        public void Complete()
        {
            cumulativeWinsByMove = new int[MaximumMoves + 1];
            int cumulative = 0;
            for (int move = 0; move <= MaximumMoves; move++)
            {
                cumulative += exactWinsByMove[move];
                cumulativeWinsByMove[move] = cumulative;
            }
        }

        public int GetCumulativeWins(int moves)
        {
            if (CompletedSimulations <= 0)
                return 0;

            moves = Mathf.Clamp(moves, 0, MaximumMoves);
            if (cumulativeWinsByMove != null)
                return cumulativeWinsByMove[moves];

            int cumulative = 0;
            for (int i = 0; i <= moves; i++)
                cumulative += exactWinsByMove[i];
            return cumulative;
        }

        public float GetWinRate(int moves)
        {
            return CompletedSimulations > 0
                ? GetCumulativeWins(moves) / (float)CompletedSimulations
                : 0f;
        }

        public int FindMinimumMovesForBestWinRate()
        {
            int bestWins = GetCumulativeWins(MaximumMoves);
            if (bestWins == 0)
                return -1;

            // Compare integer win counts, not rounded percentages. If multiple
            // budgets tie for the maximum, prefer the smallest playable budget.
            for (int move = 1; move <= MaximumMoves; move++)
                if (GetCumulativeWins(move) == bestWins)
                    return move;
            return -1;
        }

        public int FindFirstMoveAtRate(float desiredRate)
        {
            for (int move = 0; move <= MaximumMoves; move++)
            {
                if (GetWinRate(move) >= desiredRate)
                    return Mathf.Max(1, move);
            }
            return -1;
        }
    }

    private static class LevelSimulator
    {
        private sealed class SimTray
        {
            public int Capacity;
            public bool Locked;
            public TrayRule Rule;
            public int MinimumValue;
            public int FrozenStacksRemaining;
            public readonly List<int> Cards = new List<int>();

            public SimTray Clone()
            {
                SimTray clone = new SimTray
                {
                    Capacity = Capacity,
                    Locked = Locked,
                    Rule = Rule,
                    MinimumValue = MinimumValue,
                    FrozenStacksRemaining = FrozenStacksRemaining
                };
                clone.Cards.AddRange(Cards);
                return clone;
            }
        }

        private sealed class SimState
        {
            public readonly List<SimTray> Trays = new List<SimTray>();
            public bool[] CompletedTargets;
            public int MergeCount;

            public SimState Clone()
            {
                SimState clone = new SimState
                {
                    CompletedTargets = (bool[])CompletedTargets.Clone(),
                    MergeCount = MergeCount
                };
                for (int i = 0; i < Trays.Count; i++)
                    clone.Trays.Add(Trays[i].Clone());
                return clone;
            }
        }

        private sealed class CandidateMove
        {
            public SimState State;
            public double Score;
            public ulong Hash;
            public int Source;
            public int Target;
            public int Amount;
        }

        private sealed class DealGroup
        {
            public int Value;
            public int Count;
            public int FirstIndex;
        }

        public static SimulationOutcome Run(
            LevelSnapshot snapshot,
            int maximumMoves,
            int maximumDeals,
            BotSkill skill,
            int seed,
            StringBuilder trace = null)
        {
            System.Random random = new System.Random(seed);
            // Bot tie-breaking must not consume the deal random stream.
            System.Random dealRandom = new System.Random(unchecked(seed ^ 0x51ed270b));
            SimState state = CreateInitialState(snapshot);
            trace?.AppendLine($"\nSeed: {seed}; skill: {skill}; max moves: {maximumMoves}; deal guard: {maximumDeals}");
            AppendState(trace, state);
            UpdateTargets(state, snapshot.Targets);

            if (HasWon(state))
            {
                return new SimulationOutcome
                {
                    Won = true,
                    Moves = 0,
                    RequiredMoveBudget = 1,
                    Deals = 0
                };
            }

            HashSet<ulong> visited = new HashSet<ulong>
            {
                CalculateHash(state)
            };
            int moves = 0;
            int deals = 0;

            while (moves < maximumMoves)
            {
                List<CandidateMove> candidates = BuildCandidates(
                    state,
                    snapshot,
                    visited,
                    random
                );
                // Prefer a certain win before random skill mistakes or another deal.
                CandidateMove immediateWin = candidates.Find(candidate => HasWon(candidate.State));
                if (immediateWin == null && skill != BotSkill.Casual)
                    RankWithLookahead(candidates, snapshot, visited, skill);
                double currentScore = EvaluateState(state, snapshot.Targets);
                bool hasProductiveMove =
                    candidates.Count > 0 && candidates[0].Score > currentScore + 0.25;
                bool casualEarlyDeal = skill == BotSkill.Casual &&
                                       deals < maximumDeals &&
                                       GetEmptyRatio(state) > 0.25 &&
                                       random.NextDouble() < 0.1;

                bool canDeal = CanDealAnyCard(state, snapshot.Deal);
                if (immediateWin == null && (!hasProductiveMove || casualEarlyDeal) &&
                    deals < maximumDeals && canDeal)
                {
                    deals++;
                    bool placed = ApplyDeal(state, snapshot, dealRandom);
                    trace?.AppendLine($"Deal {deals} after {moves} moves: {(placed ? "placed" : "no eligible rolled card")}");
                    AppendState(trace, state);
                    if (placed)
                    {
                        ulong afterDeal = CalculateHash(state);
                        visited.Add(afterDeal);

                        if (HasWon(state))
                        {
                            return new SimulationOutcome
                            {
                                Won = true,
                                Moves = moves,
                                // At budget 'moves', gameplay already lost after the
                                // previous move. One unspent move is needed to deal.
                                RequiredMoveBudget = moves + 1,
                                Deals = deals
                            };
                        }

                    }
                    // An unlucky roll on a restricted tray is not a deadlock.
                    continue;
                }

                if (candidates.Count == 0)
                {
                    return new SimulationOutcome
                    {
                        Won = false,
                        Moves = moves,
                        Deals = deals,
                        Failure = canDeal && deals >= maximumDeals
                            ? FailureReason.DealLimit
                            : HasLegalMove(state, snapshot)
                                ? FailureReason.BotStalled
                                : FailureReason.Deadlock
                    };
                }

                CandidateMove selected = immediateWin ?? SelectCandidate(candidates, skill, random);
                state = selected.State;
                visited.Add(selected.Hash);
                moves++;
                trace?.AppendLine($"Move {moves}: Tray {selected.Source + 1} -> Tray {selected.Target + 1}, {selected.Amount} cards");
                AppendState(trace, state);

                if (HasWon(state))
                {
                    return new SimulationOutcome
                    {
                        Won = true,
                        Moves = moves,
                        RequiredMoveBudget = moves,
                        Deals = deals
                    };
                }
            }

            return new SimulationOutcome
            {
                Won = false,
                Moves = moves,
                Deals = deals,
                Failure = FailureReason.MoveLimit
            };
        }

        private static void AppendState(StringBuilder trace, SimState state)
        {
            if (trace == null)
                return;
            for (int i = 0; i < state.Trays.Count; i++)
                trace.Append($"T{i + 1}{(state.Trays[i].Locked ? "(locked)" : "")}" +
                    $"[{string.Join(",", state.Trays[i].Cards.Select(FormatCardCode))}] ");
            trace.AppendLine();
        }

        private static void RankWithLookahead(List<CandidateMove> candidates,
            LevelSnapshot snapshot, HashSet<ulong> visited, BotSkill skill)
        {
            // Bounded two-ply search: recognize moves that expose a useful run,
            // instead of immediately burying it under a deal. Never inspect future RNG.
            int width = Math.Min(candidates.Count, skill == BotSkill.Expert ? 12 : 6);
            for (int i = 0; i < width; i++)
            {
                CandidateMove candidate = candidates[i];
                HashSet<ulong> branchVisited = new HashSet<ulong>(visited) { candidate.Hash };
                List<CandidateMove> replies = BuildCandidates(candidate.State, snapshot,
                    branchVisited, new System.Random(0));
                if (replies.Count == 0)
                    continue;
                if (replies.Exists(reply => HasWon(reply.State)))
                    candidate.Score += 1000000;
                else
                    candidate.Score += Math.Max(0, replies[0].Score -
                        EvaluateState(candidate.State, snapshot.Targets)) * 0.65;
            }
            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        }

        private static bool CanDealAnyCard(SimState state, DealSettings settings)
        {
            if (settings == null || !settings.Enabled)
                return false;
            GetDealBoardValues(state, out int min, out int max);
            min = ResolveMinimumDealValue(min, max, settings.MinimumValue);
            int count = max - min;
            settings.Profiles.TryGetValue(count, out List<float> weights);
            bool completeProfile = weights != null && weights.Count >= count;
            float total = 0;
            if (completeProfile)
                for (int i = 0; i < count; i++)
                    total += Math.Max(0, weights[i]);
            int upper = min == max ? min : max - 1;
            foreach (SimTray tray in state.Trays)
            {
                if (tray.Locked || tray.Cards.Count >= tray.Capacity ||
                    HasTopObstacleCard(tray))
                {
                    continue;
                }

                for (int value = min; value <= upper; value++)
                    if ((total <= 0 || weights[max - value - 1] > 0) &&
                        CanReceiveIgnoringTop(tray, value) &&
                        !WouldDealTenthMatchingCard(tray, value) &&
                        CanReceiveDealValue(tray, value, 3))
                        return true;
            }
            return false;
        }

        private static void GetDealBoardValues(SimState state, out int min, out int max)
        {
            min = 20;
            max = 1;
            bool hasCards = false;
            foreach (SimTray tray in state.Trays)
            foreach (int value in tray.Cards)
            {
                if (!IsNumberCardCode(value))
                    continue;
                min = Mathf.Min(min, value);
                max = Mathf.Max(max, value);
                hasCards = true;
            }
            if (!hasCards)
                min = 1;
        }

        private static int ResolveMinimumDealValue(
            int boardMinimum,
            int boardMaximum,
            int configuredMinimum)
        {
            boardMaximum = Mathf.Clamp(boardMaximum, 1, 20);
            int maximumLegalMinimum = boardMaximum > 1
                ? boardMaximum - 1
                : 1;

            return configuredMinimum > 0
                ? Mathf.Clamp(configuredMinimum, 1, maximumLegalMinimum)
                : Mathf.Clamp(boardMinimum, 1, boardMaximum);
        }

        private static int ResolveRegularDealMinimum(
            SimState state,
            int minimumDealValue,
            int maximumDealValue)
        {
            int nextBoardValue = int.MaxValue;

            foreach (SimTray tray in state.Trays)
            foreach (int value in tray.Cards)
            {
                if (IsNumberCardCode(value) &&
                    value > minimumDealValue &&
                    value < nextBoardValue)
                {
                    nextBoardValue = value;
                }
            }

            return nextBoardValue != int.MaxValue
                ? Mathf.Clamp(
                    nextBoardValue,
                    minimumDealValue,
                    maximumDealValue
                )
                : minimumDealValue;
        }

        private static bool HasBoardValue(SimState state, int targetValue)
        {
            foreach (SimTray tray in state.Trays)
            foreach (int value in tray.Cards)
                if (value == targetValue)
                    return true;

            return false;
        }
        private static bool HasLegalMove(SimState state, LevelSnapshot snapshot) =>
            BuildCandidates(
                state,
                snapshot,
                new HashSet<ulong>(),
                new System.Random(0),
                false
            ).Count > 0;

        private static SimState CreateInitialState(LevelSnapshot snapshot)
        {
            SimState state = new SimState
            {
                CompletedTargets = new bool[snapshot.Targets.Count]
            };

            for (int i = 0; i < snapshot.Trays.Count; i++)
            {
                TrayDefinition definition = snapshot.Trays[i];
                SimTray tray = new SimTray
                {
                    Capacity = definition.Capacity,
                    Locked = definition.Locked,
                    Rule = definition.Rule,
                    MinimumValue = definition.MinimumValue,
                    FrozenStacksRemaining = definition.FrozenStacksRemaining
                };
                tray.Cards.AddRange(definition.InitialCards);
                state.Trays.Add(tray);
            }

            return state;
        }

        private static List<CandidateMove> BuildCandidates(
            SimState state,
            LevelSnapshot snapshot,
            HashSet<ulong> visited,
            System.Random random,
            bool pruneEquivalentEmptyMoves = true)
        {
            List<CandidateMove> candidates = new List<CandidateMove>();

            for (int sourceIndex = 0; sourceIndex < state.Trays.Count; sourceIndex++)
            {
                SimTray source = state.Trays[sourceIndex];
                if (source.Locked || source.Cards.Count == 0)
                    continue;

                int sourceTopIndex = source.Cards.Count - 1;
                int sourceCode = source.Cards[sourceTopIndex];
                int sourceKind = GetCardKind(sourceCode);
                int selectionStart = GetSelectionStart(source);
                int selectionCount = source.Cards.Count - selectionStart;
                bool selectionContainsChain = ContainsCardKind(
                    source,
                    selectionStart,
                    selectionCount,
                    ChainCardCode
                );

                for (int targetIndex = 0; targetIndex < state.Trays.Count; targetIndex++)
                {
                    if (sourceIndex == targetIndex)
                        continue;

                    SimTray target = state.Trays[targetIndex];
                    int empty = target.Capacity - target.Cards.Count;
                    if (empty <= 0 || target.Locked || IsTopCard(target, IronCardCode))
                        continue;

                    int targetKind = target.Cards.Count > 0
                        ? GetCardKind(target.Cards[target.Cards.Count - 1])
                        : 0;
                    int amount = 0;
                    bool sourceDowngrade = sourceKind == DowngradeCardCode;
                    bool targetDowngrade = targetKind == DowngradeCardCode;
                    bool bindSourceWild = false;
                    bool bindTargetWild = false;
                    int resolvedSourceCode = sourceCode;

                    if (sourceKind == IronCardCode)
                    {
                        if (CanReceiveIgnoringTop(target, sourceCode))
                            amount = 1;
                    }
                    else if (sourceKind == DarkKingCardCode)
                    {
                        bool validTop = targetKind == 0 ||
                                        IsNumberCardCode(targetKind) ||
                                        targetKind == DarkKingCardCode;
                        if (validTop && CanReceiveIgnoringTop(target, sourceCode))
                            amount = 1;
                    }
                    else if (sourceDowngrade || targetDowngrade)
                    {
                        if (sourceDowngrade && targetDowngrade)
                            continue;

                        if (sourceDowngrade)
                        {
                            if (ContainsNumberCard(target) &&
                                CanReceiveIgnoringTop(target, sourceCode))
                                amount = 1;
                        }
                        else if (IsNumberCardCode(sourceKind) &&
                                 CanReceiveIgnoringTop(target, sourceCode))
                        {
                            amount = selectionContainsChain
                                ? selectionCount
                                : Mathf.Min(selectionCount, empty);
                        }
                    }
                    else if (selectionContainsChain || targetKind == ChainCardCode)
                    {
                        amount = selectionContainsChain
                            ? selectionCount
                            : Mathf.Min(selectionCount, empty);
                        if (amount > empty ||
                            !CanReceiveSelectionIgnoringTop(
                                source,
                                source.Cards.Count - amount,
                                amount,
                                target))
                        {
                            amount = 0;
                        }
                    }
                    else
                    {
                        bool sourceWild = sourceKind == WildCardCode;
                        bool targetWild = targetKind == WildCardCode;
                        if (sourceWild && targetWild)
                            continue;

                        if (sourceWild && targetKind != 0)
                        {
                            resolvedSourceCode = targetKind == DarkKingCardCode
                                ? EncodeDarkKing(snapshot.DefaultDarkKingMoves)
                                : targetKind;
                            bindSourceWild = true;
                        }

                        int resolvedKind = GetCardKind(resolvedSourceCode);
                        if (!CanReceiveIgnoringTop(target, resolvedSourceCode))
                            continue;
                        if (targetKind != 0 && !targetWild && targetKind != resolvedKind)
                            continue;

                        bindTargetWild = targetWild && !sourceWild;
                        amount = Mathf.Min(selectionCount, empty);
                    }

                    if (amount > empty)
                        amount = 0;
                    amount = Mathf.Min(amount, CardSlotHolder.MAX_SLOTS);
                    if (amount <= 0)
                        continue;

                    // Only prune a true rename of interchangeable empty space.
                    // Different capacities/rules can make this a useful (or winning) move.
                    if (pruneEquivalentEmptyMoves &&
                        target.Cards.Count == 0 && amount == source.Cards.Count &&
                        amount < target.Capacity && source.Capacity == target.Capacity &&
                        source.Rule == target.Rule && source.MinimumValue == target.MinimumValue)
                        continue;

                    bool joinedSameValue = target.Cards.Count > 0 &&
                        GetCardKind(target.Cards[target.Cards.Count - 1]) ==
                        GetCardKind(resolvedSourceCode);
                    bool emptiedSource = amount == source.Cards.Count;
                    SimState next = state.Clone();
                    SimTray nextSource = next.Trays[sourceIndex];
                    SimTray nextTarget = next.Trays[targetIndex];
                    int targetCountBeforeMove = nextTarget.Cards.Count;

                    if (bindSourceWild)
                        nextSource.Cards[nextSource.Cards.Count - 1] = resolvedSourceCode;
                    if (bindTargetWild)
                        nextTarget.Cards[nextTarget.Cards.Count - 1] = sourceCode;

                    List<int> movedCards = nextSource.Cards.GetRange(
                        nextSource.Cards.Count - amount,
                        amount
                    );

                    nextSource.Cards.RemoveRange(
                        nextSource.Cards.Count - amount,
                        amount
                    );
                    nextTarget.Cards.AddRange(movedCards);

                    TickDarkKingCards(next);
                    if (sourceDowngrade)
                        ApplySourceDowngrade(nextTarget);
                    else if (targetDowngrade)
                        ApplyTargetDowngrade(nextTarget, targetCountBeforeMove);

                    int mergeBefore = next.MergeCount;
                    TryMergeTray(next, targetIndex);
                    UpdateTargets(next, snapshot.Targets);
                    ulong hash = CalculateHash(next);

                    if (visited.Contains(hash))
                        continue;

                    double score = EvaluateState(next, snapshot.Targets);

                    if (joinedSameValue)
                        score += 15.0 * amount;
                    if (emptiedSource)
                        score += 32.0;
                    if (next.MergeCount > mergeBefore)
                        score += 1200.0;
                    score += random.NextDouble() * 0.001;

                    candidates.Add(new CandidateMove
                    {
                        State = next,
                        Score = score,
                        Hash = hash,
                        Source = sourceIndex,
                        Target = targetIndex,
                        Amount = amount
                    });
                }
            }

            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
            return candidates;
        }

        private static int GetSelectionStart(SimTray tray)
        {
            int topIndex = tray.Cards.Count - 1;
            if (topIndex < 0)
                return 0;

            int topKind = GetCardKind(tray.Cards[topIndex]);
            if (IsObstacleCardKind(topKind))
                return topIndex;

            int chainIndex = -1;
            for (int i = topIndex; i >= 0; i--)
            {
                if (GetCardKind(tray.Cards[i]) == ChainCardCode)
                {
                    chainIndex = i;
                    break;
                }
            }

            if (chainIndex >= 0 && chainIndex != topIndex)
            {
                int start = 0;
                for (int i = topIndex; i >= 0; i--)
                {
                    if (GetCardKind(tray.Cards[i]) == IronCardCode)
                    {
                        start = i + 1;
                        break;
                    }
                }
                return start;
            }

            if (topKind == WildCardCode)
                return topIndex;

            int runStart = topIndex;
            while (runStart > 0 &&
                   GetCardKind(tray.Cards[runStart - 1]) == topKind)
            {
                runStart--;
            }
            return runStart;
        }

        private static bool IsObstacleCardKind(int cardKind)
        {
            return cardKind == DowngradeCardCode ||
                   cardKind == ChainCardCode ||
                   cardKind == FrozenCardCode ||
                   cardKind == IronCardCode ||
                   cardKind == DarkKingCardCode;
        }

        private static bool ContainsCardKind(
            SimTray tray,
            int start,
            int count,
            int cardKind)
        {
            int end = Mathf.Min(tray.Cards.Count, start + count);
            for (int i = Mathf.Max(0, start); i < end; i++)
                if (GetCardKind(tray.Cards[i]) == cardKind)
                    return true;
            return false;
        }

        private static bool ContainsNumberCard(SimTray tray)
        {
            for (int i = 0; i < tray.Cards.Count; i++)
                if (IsNumberCardCode(GetCardKind(tray.Cards[i])))
                    return true;
            return false;
        }

        private static bool IsTopCard(SimTray tray, int cardKind)
        {
            return tray.Cards.Count > 0 &&
                   GetCardKind(tray.Cards[tray.Cards.Count - 1]) == cardKind;
        }

        private static bool HasTopObstacleCard(SimTray tray)
        {
            return tray.Cards.Count > 0 &&
                   IsObstacleCardKind(GetCardKind(tray.Cards[tray.Cards.Count - 1]));
        }

        private static bool CanReceiveSelectionIgnoringTop(
            SimTray source,
            int start,
            int count,
            SimTray target)
        {
            for (int i = start; i < start + count; i++)
                if (!CanReceiveIgnoringTop(target, source.Cards[i]))
                    return false;
            return true;
        }

        private static void ApplySourceDowngrade(SimTray target)
        {
            for (int i = 0; i < target.Cards.Count; i++)
                if (IsNumberCardCode(target.Cards[i]))
                    target.Cards[i] = Mathf.Max(1, target.Cards[i] - 1);

            for (int i = target.Cards.Count - 1; i >= 0; i--)
            {
                if (GetCardKind(target.Cards[i]) != DowngradeCardCode)
                    continue;
                target.Cards.RemoveAt(i);
                break;
            }
        }

        private static void ApplyTargetDowngrade(
            SimTray target,
            int targetCountBeforeMove)
        {
            for (int i = targetCountBeforeMove; i < target.Cards.Count; i++)
                if (IsNumberCardCode(target.Cards[i]))
                    target.Cards[i] = Mathf.Max(1, target.Cards[i] - 1);

            int obstacleIndex = targetCountBeforeMove - 1;
            if (obstacleIndex >= 0 && obstacleIndex < target.Cards.Count &&
                GetCardKind(target.Cards[obstacleIndex]) == DowngradeCardCode)
            {
                target.Cards.RemoveAt(obstacleIndex);
            }
        }

        private static void TickDarkKingCards(SimState state)
        {
            for (int trayIndex = 0; trayIndex < state.Trays.Count; trayIndex++)
            {
                SimTray tray = state.Trays[trayIndex];
                if (tray.Locked)
                    continue;

                bool shouldLock = false;
                for (int cardIndex = 0; cardIndex < tray.Cards.Count; cardIndex++)
                {
                    int code = tray.Cards[cardIndex];
                    if (!IsDarkKingCode(code))
                        continue;
                    int remaining = Mathf.Max(0, code - DarkKingCountdownBase - 1);
                    tray.Cards[cardIndex] = DarkKingCountdownBase + remaining;
                    if (remaining == 0)
                        shouldLock = true;
                }

                if (shouldLock)
                    tray.Locked = true;
            }
        }

        private static CandidateMove SelectCandidate(
            List<CandidateMove> candidates,
            BotSkill skill,
            System.Random random)
        {
            int maxChoice;
            double roll = random.NextDouble();

            if (skill == BotSkill.Expert)
            {
                return candidates[0];
            }

            if (skill == BotSkill.Normal)
            {
                maxChoice = Mathf.Min(3, candidates.Count);
                int index = roll < 0.72 ? 0 : roll < 0.93 ? 1 : 2;
                return candidates[Mathf.Min(index, maxChoice - 1)];
            }

            maxChoice = Mathf.Min(5, candidates.Count);
            int casualIndex = roll < 0.45
                ? 0
                : roll < 0.70
                    ? 1
                    : roll < 0.85
                        ? 2
                        : roll < 0.95 ? 3 : 4;
            return candidates[Mathf.Min(casualIndex, maxChoice - 1)];
        }

        private static bool ApplyDeal(
            SimState state,
            LevelSnapshot snapshot,
            System.Random random)
        {
            if (snapshot.Deal == null || !snapshot.Deal.Enabled)
                return false;

            List<int> holderIndices = new List<int>();
            int totalEmpty = 0;
            int totalCapacity = 0;
            int totalCapacityRemaining = 0;

            for (int i = 0; i < state.Trays.Count; i++)
            {
                SimTray tray = state.Trays[i];
                if (tray.Locked || HasTopObstacleCard(tray))
                    continue;

                totalCapacity += tray.Capacity;
                int empty = Mathf.Max(0, tray.Capacity - tray.Cards.Count);
                totalCapacityRemaining += empty;
                if (empty <= 0)
                    continue;

                holderIndices.Add(i);
                totalEmpty += empty;
            }

            if (totalEmpty <= 0)
                return false;

            int cardCount = GetCapacityBasedDealCount(
                totalCapacityRemaining,
                totalCapacity,
                random
            );
            int maxDealableCards = totalEmpty == 1
                ? 1
                : totalEmpty - 1;
            cardCount = Mathf.Clamp(cardCount, 1, maxDealableCards);

            int intentionallySkippedHolder = -1;
            if (holderIndices.Count > 1 &&
                random.NextDouble() < snapshot.Deal.LeaveOneTrayEmptyChance)
            {
                intentionallySkippedHolder = holderIndices[
                    random.Next(holderIndices.Count)
                ];
            }

            GetDealBoardValues(state, out int minValue, out int maxValue);
            minValue = ResolveMinimumDealValue(
                minValue,
                maxValue,
                snapshot.Deal.MinimumValue
            );
            bool mustAddConfiguredMinimum =
                snapshot.Deal.MinimumValue > 0 &&
                snapshot.Deal.MinimumValue == minValue &&
                !HasBoardValue(state, minValue);
            int regularMinValue = mustAddConfiguredMinimum
                ? ResolveRegularDealMinimum(state, minValue, maxValue)
                : minValue;
            int? rangeExtensionValue = GetRangeExtensionDealValue(
                state,
                minValue
            );
            List<int> rolledCards = new List<int>(cardCount);
            if (mustAddConfiguredMinimum)
                rolledCards.Add(minValue);

            if (rangeExtensionValue.HasValue &&
                rolledCards.Count < cardCount &&
                (!mustAddConfiguredMinimum ||
                 rangeExtensionValue.Value != minValue))
            {
                rolledCards.Add(rangeExtensionValue.Value);
            }

            for (int i = rolledCards.Count; i < cardCount; i++)
                rolledCards.Add(RollCardValueInRange(
                    regularMinValue,
                    maxValue,
                    snapshot.Deal,
                    random
                ));

            List<DealGroup> groups = GroupDealCards(
                rolledCards,
                mustAddConfiguredMinimum ? minValue : (int?)null,
                rangeExtensionValue
            );
            int[] dealtCounts = new int[state.Trays.Count];
            int[] dealLimits = BuildRandomTrayDealLimits(
                state,
                holderIndices,
                cardCount,
                intentionallySkippedHolder,
                random
            );
            int[] valueLimits = new int[state.Trays.Count];

            for (int i = 0; i < holderIndices.Count; i++)
            {
                int holderIndex = holderIndices[i];
                valueLimits[holderIndex] =
                    random.NextDouble() <
                    snapshot.Deal.AllowThreeValuesPerTrayChance
                        ? 3
                        : 2;
            }

            HashSet<int> affected = new HashSet<int>();
            int placedCount = 0;

            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                DealGroup group = groups[groupIndex];
                int remaining = group.Count;

                while (remaining > 0)
                {
                    bool preserveRequestedValue =
                        rangeExtensionValue.HasValue &&
                        group.Value == rangeExtensionValue.Value;
                    bool foundHolder = TrySelectDealHolder(
                        state,
                        holderIndices,
                        group.Value,
                        dealtCounts,
                        dealLimits,
                        valueLimits,
                        minValue,
                        maxValue,
                        mustAddConfiguredMinimum,
                        preserveRequestedValue,
                        random,
                        out int bestHolder,
                        out int valueToSpawn
                    );

                    if (!foundHolder &&
                        (preserveRequestedValue ||
                         !TrySelectExistingValueFallback(
                             state,
                             holderIndices,
                             dealtCounts,
                             dealLimits,
                             valueLimits,
                             minValue,
                             maxValue,
                             mustAddConfiguredMinimum,
                             random,
                             out bestHolder,
                             out valueToSpawn)))
                    {
                        break;
                    }

                    SimTray destination = state.Trays[bestHolder];
                    if (IsTopCard(destination, WildCardCode))
                    {
                        destination.Cards[destination.Cards.Count - 1] =
                            valueToSpawn;
                    }
                    destination.Cards.Add(valueToSpawn);

                    dealtCounts[bestHolder]++;
                    remaining--;
                    placedCount++;
                    affected.Add(bestHolder);
                }
            }

            foreach (int trayIndex in affected)
                TryMergeTray(state, trayIndex);
            UpdateTargets(state, snapshot.Targets);
            return placedCount > 0;
        }

        private static int GetCapacityBasedDealCount(
            int remainingCapacity,
            int totalCapacity,
            System.Random random)
        {
            if (remainingCapacity <= 0 || totalCapacity <= 0)
                return 0;

            float remainingRatio = remainingCapacity / (float)totalCapacity;
            if (remainingRatio <= 0.2f)
                return random.Next(6, 9);
            if (remainingRatio <= 0.4f)
                return random.Next(8, 11);
            if (remainingRatio <= 0.6f)
                return random.Next(10, 13);
            if (remainingRatio <= 0.8f)
                return random.Next(12, 15);
            if (remainingRatio <= 0.9f)
                return random.Next(14, 17);
            return random.Next(16, 19);
        }

        private static int GetDynamicTrayDealLimit(int emptySlotCount)
        {
            if (emptySlotCount <= 0)
                return 0;
            if (emptySlotCount <= 2)
                return 1;
            if (emptySlotCount <= 4)
                return 2;
            if (emptySlotCount <= 8)
                return 3;
            return 4;
        }

        private static int[] BuildRandomTrayDealLimits(
            SimState state,
            List<int> holderIndices,
            int cardCount,
            int intentionallySkippedHolder,
            System.Random random)
        {
            int[] limits = new int[state.Trays.Count];
            List<int> shuffledHolders = new List<int>();

            foreach (int holderIndex in holderIndices)
            {
                if (holderIndex != intentionallySkippedHolder &&
                    GetDynamicTrayDealLimit(
                        state.Trays[holderIndex].Capacity -
                        state.Trays[holderIndex].Cards.Count
                    ) > 0)
                {
                    shuffledHolders.Add(holderIndex);
                }
            }

            for (int i = shuffledHolders.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                int temporary = shuffledHolders[i];
                shuffledHolders[i] = shuffledHolders[swapIndex];
                shuffledHolders[swapIndex] = temporary;
            }

            int remainingCapacity = 0;
            foreach (int holderIndex in shuffledHolders)
            {
                SimTray tray = state.Trays[holderIndex];
                remainingCapacity += GetDynamicTrayDealLimit(
                    tray.Capacity - tray.Cards.Count
                );
            }

            int remainingCards = Mathf.Min(
                Mathf.Max(0, cardCount),
                remainingCapacity
            );
            foreach (int holderIndex in shuffledHolders)
            {
                SimTray tray = state.Trays[holderIndex];
                int holderCapacity = GetDynamicTrayDealLimit(
                    tray.Capacity - tray.Cards.Count
                );
                remainingCapacity -= holderCapacity;
                int minimum = Mathf.Max(
                    0,
                    remainingCards - remainingCapacity
                );
                int maximum = Mathf.Min(holderCapacity, remainingCards);
                int limit = minimum < maximum
                    ? NextIntInclusive(random, minimum, maximum)
                    : minimum;
                limits[holderIndex] = limit;
                remainingCards -= limit;
            }

            return limits;
        }

        private static int? GetRangeExtensionDealValue(
            SimState state,
            int minimumDealValue)
        {
            HashSet<int> boardValues = new HashSet<int>();
            foreach (SimTray tray in state.Trays)
            foreach (int card in tray.Cards)
                if (IsNumberCardCode(card))
                    boardValues.Add(card);

            if (boardValues.Count != 2)
                return null;

            int lowestValue = boardValues.Min();
            int extensionValue = lowestValue - 1;
            return extensionValue >= minimumDealValue
                ? extensionValue
                : (int?)null;
        }

        private static bool TrySelectDealHolder(
            SimState state,
            List<int> holderIndices,
            int requestedValue,
            int[] dealtCounts,
            int[] dealLimits,
            int[] valueLimits,
            int minimumDealValue,
            int highestBoardValue,
            bool protectMinimumFrequency,
            bool preserveRequestedValue,
            System.Random random,
            out int selectedHolder,
            out int selectedValue)
        {
            selectedHolder = -1;
            selectedValue = requestedValue;
            int bestDealLimit = int.MinValue;
            int bestDealtCount = int.MinValue;
            int equalBestCount = 0;

            foreach (int holderIndex in holderIndices)
            {
                SimTray tray = state.Trays[holderIndex];
                int alignedValue = preserveRequestedValue
                    ? requestedValue
                    : GetTrayAlignedDealValue(
                        tray,
                        requestedValue,
                        minimumDealValue,
                        highestBoardValue,
                        protectMinimumFrequency
                    );

                if (WouldDealTenthMatchingCard(tray, alignedValue))
                {
                    alignedValue = requestedValue;
                    if (WouldDealTenthMatchingCard(tray, alignedValue))
                        continue;
                }

                if (!CanReceiveIgnoringTop(tray, alignedValue))
                    continue;
                if (!preserveRequestedValue &&
                    !CanReceiveDealValue(
                        tray,
                        alignedValue,
                        Mathf.Max(1, valueLimits[holderIndex])
                    ))
                {
                    continue;
                }

                int dealtCount = dealtCounts[holderIndex];
                int dealLimit = dealLimits[holderIndex];
                int emptySlotCount = tray.Capacity - tray.Cards.Count;
                if (emptySlotCount <= 0 || dealtCount >= dealLimit)
                    continue;

                bool isBetter = dealLimit > bestDealLimit ||
                                (dealLimit == bestDealLimit &&
                                 dealtCount > bestDealtCount);
                if (isBetter)
                {
                    selectedHolder = holderIndex;
                    selectedValue = alignedValue;
                    bestDealLimit = dealLimit;
                    bestDealtCount = dealtCount;
                    equalBestCount = 1;
                }
                else if (dealLimit == bestDealLimit &&
                         dealtCount == bestDealtCount)
                {
                    equalBestCount++;
                    if (random.Next(equalBestCount) == 0)
                    {
                        selectedHolder = holderIndex;
                        selectedValue = alignedValue;
                    }
                }
            }

            return selectedHolder >= 0;
        }

        private static bool TrySelectExistingValueFallback(
            SimState state,
            List<int> holderIndices,
            int[] dealtCounts,
            int[] dealLimits,
            int[] valueLimits,
            int minimumDealValue,
            int highestBoardValue,
            bool protectMinimumFrequency,
            System.Random random,
            out int selectedHolder,
            out int selectedValue)
        {
            List<int> existingValues = new List<int>();
            foreach (int holderIndex in holderIndices)
            foreach (int card in state.Trays[holderIndex].Cards)
            {
                if (!IsNumberCardCode(card) ||
                    !IsWithinDealValueRange(
                        card,
                        minimumDealValue,
                        highestBoardValue
                    ) ||
                    (protectMinimumFrequency &&
                     card == minimumDealValue) ||
                    existingValues.Contains(card))
                {
                    continue;
                }

                existingValues.Add(card);
            }

            int startIndex = existingValues.Count > 0
                ? random.Next(existingValues.Count)
                : 0;
            for (int offset = 0; offset < existingValues.Count; offset++)
            {
                int candidateValue = existingValues[
                    (startIndex + offset) % existingValues.Count
                ];
                if (TrySelectDealHolder(
                    state,
                    holderIndices,
                    candidateValue,
                    dealtCounts,
                    dealLimits,
                    valueLimits,
                    minimumDealValue,
                    highestBoardValue,
                    protectMinimumFrequency,
                    false,
                    random,
                    out selectedHolder,
                    out selectedValue))
                {
                    return true;
                }
            }

            selectedHolder = -1;
            selectedValue = 0;
            return false;
        }

        private static int GetTrayAlignedDealValue(
            SimTray tray,
            int requestedValue,
            int minimumDealValue,
            int highestBoardValue,
            bool protectMinimumFrequency)
        {
            if (tray == null || tray.Capacity <= 0 ||
                tray.Cards.Count <= tray.Capacity * 0.5f)
            {
                return requestedValue;
            }

            Dictionary<int, int> valueCounts = new Dictionary<int, int>();
            foreach (int card in tray.Cards)
            {
                if (!IsNumberCardCode(card))
                    continue;
                valueCounts.TryGetValue(card, out int count);
                valueCounts[card] = count + 1;
            }

            if (valueCounts.Count == 0)
                return requestedValue;

            int dominantCount = valueCounts.Values.Max();
            bool HasDominantLegalValue(int value)
            {
                bool isProtectedMinimum =
                    protectMinimumFrequency &&
                    value == minimumDealValue &&
                    value != requestedValue;
                return valueCounts.TryGetValue(value, out int count) &&
                       count == dominantCount &&
                       !isProtectedMinimum &&
                       IsWithinDealValueRange(
                           value,
                           minimumDealValue,
                           highestBoardValue
                       );
            }

            int topValue = tray.Cards.Count > 0
                ? tray.Cards[tray.Cards.Count - 1]
                : 0;
            if (HasDominantLegalValue(topValue))
                return topValue;
            if (HasDominantLegalValue(requestedValue))
                return requestedValue;

            foreach (KeyValuePair<int, int> pair in valueCounts)
                if (HasDominantLegalValue(pair.Key))
                    return pair.Key;

            return requestedValue;
        }

        private static bool IsWithinDealValueRange(
            int value,
            int minimumDealValue,
            int highestBoardValue)
        {
            return value >= minimumDealValue &&
                   (highestBoardValue <= 1 || value < highestBoardValue);
        }

        private static bool WouldDealTenthMatchingCard(
            SimTray tray,
            int value)
        {
            if (tray == null || tray.Cards.Count >= tray.Capacity)
                return false;

            int matchingCount = 0;
            foreach (int card in tray.Cards)
                if (card == value)
                    matchingCount++;
            return matchingCount >= CardSlotHolder.MAX_SLOTS - 1;
        }

        private static bool CanReceiveDealValue(
            SimTray tray,
            int value,
            int valueLimit)
        {
            if (!IsNumberCardCode(value))
                return false;

            HashSet<int> distinctValues = new HashSet<int>();
            foreach (int card in tray.Cards)
                if (IsNumberCardCode(card))
                    distinctValues.Add(card);

            return distinctValues.Contains(value) ||
                   distinctValues.Count < Mathf.Max(1, valueLimit);
        }

        private static List<DealGroup> GroupDealCards(
            List<int> cards,
            int? requiredMinimum,
            int? rangeExtension)
        {
            Dictionary<int, DealGroup> byValue = new Dictionary<int, DealGroup>();
            for (int i = 0; i < cards.Count; i++)
            {
                int value = cards[i];
                if (!byValue.TryGetValue(value, out DealGroup group))
                {
                    group = new DealGroup
                    {
                        Value = value,
                        FirstIndex = i
                    };
                    byValue.Add(value, group);
                }
                group.Count++;
            }

            List<DealGroup> groups = new List<DealGroup>(byValue.Values);
            groups.Sort((a, b) =>
            {
                int requiredCompare =
                    (requiredMinimum.HasValue && b.Value == requiredMinimum.Value ? 1 : 0)
                    .CompareTo(requiredMinimum.HasValue && a.Value == requiredMinimum.Value ? 1 : 0);
                if (requiredCompare != 0)
                    return requiredCompare;

                int extensionCompare =
                    (rangeExtension.HasValue && b.Value == rangeExtension.Value ? 1 : 0)
                    .CompareTo(rangeExtension.HasValue && a.Value == rangeExtension.Value ? 1 : 0);
                if (extensionCompare != 0)
                    return extensionCompare;

                int countCompare = b.Count.CompareTo(a.Count);
                return countCompare != 0
                    ? countCompare
                    : a.FirstIndex.CompareTo(b.FirstIndex);
            });
            return groups;
        }

        private static int RollCardValue(
            int maxValue,
            DealSettings settings,
            System.Random random)
        {
            return RollCardValueInRange(1, maxValue, settings, random);
        }

        private static int RollCardValueInRange(
            int minValue, int maxValue, DealSettings settings, System.Random random)
        {
            maxValue = Mathf.Clamp(maxValue, 1, 20);
            minValue = Mathf.Clamp(minValue, 1, maxValue);
            if (minValue == maxValue)
                return minValue;

            int count = maxValue - minValue;
            if (!settings.Profiles.TryGetValue(count, out List<float> weights) ||
                weights == null || weights.Count < count)
                return random.Next(minValue, maxValue);

            float total = 0f;
            for (int i = 0; i < count; i++)
                total += Mathf.Max(0f, weights[i]);
            if (total <= 0f)
                return random.Next(minValue, maxValue);

            double roll = random.NextDouble() * total;
            float cumulative = 0f;
            int lastPositive = maxValue - 1;
            for (int i = 0; i < count; i++)
            {
                float weight = Mathf.Max(0f, weights[i]);
                if (weight <= 0f)
                    continue;
                lastPositive = maxValue - (i + 1);
                cumulative += weight;
                if (roll < cumulative)
                    return lastPositive;
            }
            return lastPositive;
        }
        private static bool TryMergeTray(SimState state, int trayIndex)
        {
            SimTray tray = state.Trays[trayIndex];
            if (tray.Locked || tray.Capacity < 2 ||
                tray.Cards.Count != tray.Capacity)
            {
                return false;
            }

            int value = tray.Cards[0];
            if (!IsNumberCardCode(value) || value >= 20)
                return false;

            for (int i = 1; i < tray.Cards.Count; i++)
            {
                if (tray.Cards[i] != value)
                    return false;
            }

            int mergedCardCount = tray.Cards.Count;
            tray.Cards.Clear();
            tray.Cards.Add(value + 1);
            tray.Cards.Add(value + 1);
            state.MergeCount++;

            if (mergedCardCount == CardSlotHolder.MAX_SLOTS)
            {
                for (int i = 0; i < state.Trays.Count; i++)
                {
                    SimTray frozen = state.Trays[i];
                    if (frozen.Rule != TrayRule.Frozen ||
                        frozen.FrozenStacksRemaining <= 0)
                    {
                        continue;
                    }

                    frozen.FrozenStacksRemaining--;
                    if (frozen.FrozenStacksRemaining <= 0)
                        frozen.Locked = false;
                }
            }

            return true;
        }

        private static bool CanReceiveNormal(SimTray tray, int value)
        {
            if (!CanReceiveIgnoringTop(tray, value))
                return false;
            if (tray.Cards.Count == 0 || IsTopCard(tray, WildCardCode))
                return true;
            return GetCardKind(tray.Cards[tray.Cards.Count - 1]) ==
                   GetCardKind(value);
        }

        private static bool CanReceiveIgnoringTop(SimTray tray, int value)
        {
            if (tray.Locked || tray.Cards.Count >= tray.Capacity)
                return false;
            if (IsTopCard(tray, IronCardCode))
                return false;
            int cardKind = GetCardKind(value);
            if (tray.Rule == TrayRule.HighValue && cardKind < tray.MinimumValue)
                return false;
            if (tray.Rule == TrayRule.Wild && tray.Cards.Count > 0)
                return GetCardKind(tray.Cards[tray.Cards.Count - 1]) == cardKind;
            return true;
        }

        private static int GetTopRunCount(SimTray tray)
        {
            if (tray.Cards.Count == 0)
                return 0;

            int value = GetCardKind(tray.Cards[tray.Cards.Count - 1]);
            int count = 1;
            for (int i = tray.Cards.Count - 2; i >= 0; i--)
            {
                if (GetCardKind(tray.Cards[i]) != value)
                    break;
                count++;
            }
            return count;
        }

        private static int GetMaximumBoardValue(SimState state)
        {
            int max = 1;
            for (int i = 0; i < state.Trays.Count; i++)
            {
                SimTray tray = state.Trays[i];
                if (tray.Locked)
                    continue;
                for (int j = 0; j < tray.Cards.Count; j++)
                    if (IsNumberCardCode(tray.Cards[j]))
                        max = Mathf.Max(max, tray.Cards[j]);
            }
            return max;
        }

        private static void UpdateTargets(
            SimState state,
            List<TargetDefinition> targets)
        {
            int[] counts = new int[21];
            for (int i = 0; i < state.Trays.Count; i++)
            {
                SimTray tray = state.Trays[i];
                if (tray.Locked)
                    continue;
                for (int j = 0; j < tray.Cards.Count; j++)
                {
                    int value = tray.Cards[j];
                    if (value >= 1 && value <= 20)
                        counts[value]++;
                }
            }

            for (int i = 0; i < targets.Count; i++)
            {
                if (!state.CompletedTargets[i] &&
                    counts[targets[i].Value] >= targets[i].Count)
                {
                    state.CompletedTargets[i] = true;
                }
            }
        }

        private static bool HasWon(SimState state)
        {
            for (int i = 0; i < state.CompletedTargets.Length; i++)
            {
                if (!state.CompletedTargets[i])
                    return false;
            }
            return true;
        }

        private static double EvaluateState(
            SimState state,
            List<TargetDefinition> targets)
        {
            int[] counts = new int[21];
            double score = 0.0;

            for (int i = 0; i < state.Trays.Count; i++)
            {
                SimTray tray = state.Trays[i];
                if (tray.Locked)
                {
                    if (tray.Rule == TrayRule.Frozen)
                        score -= tray.FrozenStacksRemaining * 25.0;
                    continue;
                }

                if (tray.Cards.Count == 0)
                {
                    score += 75.0;
                    continue;
                }

                int transitions = 0;
                for (int j = 0; j < tray.Cards.Count; j++)
                {
                    int value = tray.Cards[j];
                    if (value >= 1 && value <= 20)
                        counts[value]++;
                    if (j > 0 && tray.Cards[j - 1] != value)
                        transitions++;
                }

                int topRun = GetTopRunCount(tray);
                score += topRun * topRun * 4.0;
                score -= transitions * 18.0;
                score += (tray.Capacity - tray.Cards.Count) * 1.5;

                if (tray.Cards.Count == tray.Capacity && transitions > 0)
                    score -= 180.0;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                TargetDefinition target = targets[i];
                if (state.CompletedTargets[i])
                {
                    score += 100000.0;
                    continue;
                }

                double exactProgress = Mathf.Min(
                    1f,
                    counts[target.Value] / (float)target.Count
                );
                score += exactProgress * 5000.0;

                double convertibleProgress = 0.0;
                for (int value = 1; value < target.Value; value++)
                {
                    int difference = target.Value - value;
                    convertibleProgress += counts[value] * Math.Pow(0.2, difference);
                }
                score += Math.Min(
                    1.0,
                    convertibleProgress / Math.Max(1, target.Count)
                ) * 800.0;
            }

            score += state.MergeCount * 20.0;
            return score;
        }

        private static float GetEmptyRatio(SimState state)
        {
            int empty = 0;
            int capacity = 0;
            for (int i = 0; i < state.Trays.Count; i++)
            {
                SimTray tray = state.Trays[i];
                if (tray.Locked)
                    continue;
                capacity += tray.Capacity;
                empty += Mathf.Max(0, tray.Capacity - tray.Cards.Count);
            }
            return capacity > 0 ? empty / (float)capacity : 0f;
        }

        private static ulong CalculateHash(SimState state)
        {
            const ulong offset = 1469598103934665603UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;

            for (int i = 0; i < state.Trays.Count; i++)
            {
                SimTray tray = state.Trays[i];
                hash = (hash ^ (ulong)(tray.Locked ? 1 : 0)) * prime;
                hash = (hash ^ (ulong)(tray.FrozenStacksRemaining + 31)) * prime;
                hash = (hash ^ (ulong)(tray.Cards.Count + 47)) * prime;
                for (int j = 0; j < tray.Cards.Count; j++)
                    hash = (hash ^ (ulong)(tray.Cards[j] + 67)) * prime;
                hash = (hash ^ 251UL) * prime;
            }

            for (int i = 0; i < state.CompletedTargets.Length; i++)
                hash = (hash ^ (ulong)(state.CompletedTargets[i] ? 379 : 383)) * prime;
            return hash;
        }

        private static int NextIntInclusive(
            System.Random random,
            int minimum,
            int maximum)
        {
            minimum = Mathf.Max(1, minimum);
            maximum = Mathf.Max(minimum, maximum);
            return random.Next(minimum, maximum + 1);
        }
    }
}
#endif
