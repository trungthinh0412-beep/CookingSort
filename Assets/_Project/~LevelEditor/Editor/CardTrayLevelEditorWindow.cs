using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class CardTrayLevelEditorWindow : EditorWindow
{
    private enum EditMode
    {
        AddCard,
        DeleteCard,
        AddTray,
        DeleteTray,
        FreezeCard
    }

    private const float PaletteItemWidth = 78f;
    private const float PaletteItemHeight = 98f;

    [SerializeField] private Level level;
    [SerializeField] private CardConfig cardConfig;
    [SerializeField] private GameObject clickBlocker;
    [SerializeField] private CardType selectedCardType;
    [SerializeField] private EditMode editMode;
    [SerializeField, Min(1)] private int targetCount = 1;
    [SerializeField, Min(1)]
    private int darkKingMovesBeforeTrayLock = 3;
    [SerializeField, Min(1)] private int frozenMovesBeforeOpen = 3;
    [SerializeField, Range(1, 20)] private int frozenCardValue = 8;
    [SerializeField] private int selectedTrayPrefabIndex;

    private LevelEditorSetting _setting;
    private Vector2 _windowScroll;
    private Vector2 _paletteScroll;
    private string _statusMessage = "Ready.";
    private MessageType _statusType = MessageType.Info;
    private bool _clickBlockerStateCaptured;
    private bool _clickBlockerWasActive;

    [MenuItem("Tools/Level Editor/Card Tray Editor")]
    public static void OpenWindow()
    {
        CardTrayLevelEditorWindow window = GetWindow<CardTrayLevelEditorWindow>("Card Tray Editor");
        window.InitializeFromContext();
        window.Show();
        window.Focus();
    }

    [MenuItem("CONTEXT/Level/Open Card Tray Editor")]
    private static void OpenFromLevelContext(MenuCommand command)
    {
        OpenWindow();
        CardTrayLevelEditorWindow window = GetWindow<CardTrayLevelEditorWindow>();
        window.SetLevel(command.context as Level);
    }

    private void OnEnable()
    {
        _setting = Resources.Load<LevelEditorSetting>("LevelEditorSetting");
        if (_setting != null)
        {
            minSize = _setting.windowMinSize;
            if (cardConfig == null)
            {
                cardConfig = _setting.cardConfig;
            }
        }

        SceneView.duringSceneGui += DuringSceneGUI;
        Selection.selectionChanged += OnSelectionChanged;
        InitializeFromContext();
        if (clickBlocker == null)
        {
            ResolveSavedClickBlocker();
        }
        DisableClickBlocker();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
        Selection.selectionChanged -= OnSelectionChanged;
        RestoreClickBlocker();
    }

    private void OnGUI()
    {
        _windowScroll = EditorGUILayout.BeginScrollView(_windowScroll);
        try
        {
            DrawHeader();
            EditorGUILayout.Space(8f);
            DrawLevelSettings();
            EditorGUILayout.Space(8f);
            DrawTargetEditor();
            EditorGUILayout.Space(8f);
            DrawTrayEditor();
            EditorGUILayout.Space(8f);
            DrawModeButtons();
            EditorGUILayout.Space(8f);
            DrawCardPalette();
            EditorGUILayout.Space(8f);
            DrawStatus();
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Card Tray Level Editor", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        Level newLevel = (Level)EditorGUILayout.ObjectField(
            new GUIContent("Level", "Keo prefab Level hoac Level dang mo trong Scene vao day."),
            level,
            typeof(Level),
            true);
        if (EditorGUI.EndChangeCheck())
        {
            SetLevel(newLevel);
        }

        EditorGUI.BeginChangeCheck();
        CardConfig newCardConfig = (CardConfig)EditorGUILayout.ObjectField(
            new GUIContent("Card List", "Keo CardConfig vao day de hien danh sach icon card."),
            cardConfig,
            typeof(CardConfig),
            false);
        if (EditorGUI.EndChangeCheck())
        {
            SetCardConfig(newCardConfig);
        }

        EditorGUI.BeginChangeCheck();
        GameObject newClickBlocker = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent(
                "Click Blocker",
                "Keo object dang chan click tu Hierarchy vao day. Tool se tat no khi mo va bat lai khi dong."),
            clickBlocker,
            typeof(GameObject),
            true);
        if (EditorGUI.EndChangeCheck())
        {
            SetClickBlocker(newClickBlocker);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Use Selected Level", GUILayout.Height(26f)))
        {
            Level selectedLevel = FindLevelFromSelection();
            if (selectedLevel != null)
            {
                SetLevel(selectedLevel);
            }
            else
            {
                SetStatus("Hay chon Level hoac mot object con cua Level.", MessageType.Warning);
            }
        }

        using (new EditorGUI.DisabledScope(level == null || !EditorUtility.IsPersistent(level)))
        {
            if (GUILayout.Button("Open Prefab", GUILayout.Height(26f)))
            {
                AssetDatabase.OpenAsset(level.gameObject);
            }
        }
        EditorGUILayout.EndHorizontal();

    }

    private void DrawModeButtons()
    {
        EditorGUILayout.LabelField("Card Tools", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (DrawModeButton("+  ADD CARD", EditMode.AddCard, new Color(0.18f, 0.62f, 0.34f)))
        {
            editMode = EditMode.AddCard;
            SceneView.RepaintAll();
        }

        if (DrawModeButton("-  DELETE CARD", EditMode.DeleteCard, new Color(0.78f, 0.25f, 0.22f)))
        {
            editMode = EditMode.DeleteCard;
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        if (selectedCardType == CardType.FrozenCard &&
            DrawModeButton("FREEZE EXISTING CARD", EditMode.FreezeCard,
                new Color(0.22f, 0.52f, 0.78f)))
        {
            editMode = EditMode.FreezeCard;
            SetStatus("Click a number card in the Scene to freeze it.",
                MessageType.Info);
            SceneView.RepaintAll();
        }
    }

    private void DrawLevelSettings()
    {
        EditorGUILayout.LabelField("Level Settings", EditorStyles.boldLabel);

        if (level == null)
        {
            EditorGUILayout.HelpBox(
                "Chon Level de chinh Deal va Move Settings.",
                MessageType.Info);
            return;
        }

        SerializedObject serializedLevel = new SerializedObject(level);
        serializedLevel.UpdateIfRequiredOrScript();
        SerializedProperty maxMoveCount =
            serializedLevel.FindProperty("maxMoveCount");
        SerializedProperty minDealCardValue =
            serializedLevel.FindProperty("minDealCardValue");

        if (maxMoveCount == null || minDealCardValue == null)
        {
            EditorGUILayout.HelpBox(
                "Khong tim thay Deal/Move Settings trong Level.",
                MessageType.Error);
            return;
        }

        EditorGUI.BeginChangeCheck();
        int newMinDealCardValue = EditorGUILayout.IntField(
            new GUIContent(
                "Min Value Deal Card",
                "Gia tri card thap nhat co the deal. Dat 0 de tu dong dung gia tri thap nhat tren ban. Card deal van phai thap hon card cao nhat tren ban."),
            minDealCardValue.intValue);
        int newMaxMoveCount = EditorGUILayout.IntField(
            new GUIContent(
                "Max Move Count",
                "So luot di chuyen bai cua level. Het luot se chay Continue/Lose."),
            maxMoveCount.intValue);

        if (!EditorGUI.EndChangeCheck())
            return;

        minDealCardValue.intValue = Mathf.Clamp(
            newMinDealCardValue,
            0,
            20
        );
        maxMoveCount.intValue = Mathf.Max(1, newMaxMoveCount);
        serializedLevel.ApplyModifiedProperties();
        MarkLevelChanged(level);
        SetStatus(
            $"Min Deal: {(minDealCardValue.intValue == 0 ? "Auto" : minDealCardValue.intValue.ToString())}; " +
            $"Max Move: {maxMoveCount.intValue}.",
            MessageType.Info);
    }

    private void DrawTrayEditor()
    {
        EditorGUILayout.LabelField("Tray Tools", EditorStyles.boldLabel);

        if (_setting == null)
        {
            EditorGUILayout.HelpBox("Khong tim thay LevelEditorSetting.", MessageType.Error);
            return;
        }

        SerializedObject serializedSetting = new SerializedObject(_setting);
        serializedSetting.UpdateIfRequiredOrScript();
        SerializedProperty trayPrefabs = serializedSetting.FindProperty("trayPrefabs");
        EditorGUILayout.PropertyField(
            trayPrefabs,
            new GUIContent("Tray Prefabs", "Keo cac loai prefab CardSlotHolder vao list nay."),
            true);

        if (serializedSetting.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(_setting);
            AssetDatabase.SaveAssetIfDirty(_setting);
        }

        DrawTraysPerRowField();

        List<GameObject> prefabs = _setting.trayPrefabs;
        if (prefabs == null || prefabs.Count == 0)
        {
            EditorGUILayout.HelpBox("Them it nhat mot Tray Prefab vao list.", MessageType.Warning);
        }
        else
        {
            string[] names = new string[prefabs.Count];
            for (int i = 0; i < prefabs.Count; i++)
            {
                names[i] = prefabs[i] != null ? prefabs[i].name : $"Missing Tray {i + 1}";
            }

            selectedTrayPrefabIndex = Mathf.Clamp(selectedTrayPrefabIndex, 0, prefabs.Count - 1);
            selectedTrayPrefabIndex = EditorGUILayout.Popup(
                "Selected Tray",
                selectedTrayPrefabIndex,
                names);
        }

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(
                   prefabs == null ||
                   prefabs.Count == 0 ||
                   selectedTrayPrefabIndex < 0 ||
                   selectedTrayPrefabIndex >= prefabs.Count ||
                   prefabs[selectedTrayPrefabIndex] == null))
        {
            if (DrawModeButton("+  ADD TRAY", EditMode.AddTray, new Color(0.15f, 0.58f, 0.72f)))
            {
                editMode = EditMode.AddTray;
                SetStatus($"Add Tray: {prefabs[selectedTrayPrefabIndex].name}.", MessageType.Info);
                SceneView.RepaintAll();
            }
        }

        if (DrawModeButton("-  DELETE TRAY", EditMode.DeleteTray, new Color(0.78f, 0.25f, 0.22f)))
        {
            editMode = EditMode.DeleteTray;
            SetStatus("Delete Tray: click tray trong Scene.", MessageType.Info);
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawTraysPerRowField()
    {
        if (level == null) return;

        Transform trayParent = GetTrayParent(level);
        TrayLayoutGroup layout = trayParent != null
            ? trayParent.GetComponent<TrayLayoutGroup>()
            : null;

        if (layout == null)
        {
            EditorGUILayout.HelpBox(
                "SlotsRoot chua co TrayLayoutGroup.",
                MessageType.Warning);
            return;
        }

        SerializedObject serializedLayout = new SerializedObject(layout);
        serializedLayout.UpdateIfRequiredOrScript();
        SerializedProperty columnCount = serializedLayout.FindProperty("columnCount");
        if (columnCount == null) return;

        EditorGUI.BeginChangeCheck();
        int newColumnCount = EditorGUILayout.IntField(
            new GUIContent("Trays Per Row", "So luong Tray toi da tren mot hang."),
            columnCount.intValue);

        if (!EditorGUI.EndChangeCheck()) return;

        Undo.RegisterFullObjectHierarchyUndo(trayParent.gameObject, "Change Trays Per Row");
        columnCount.intValue = Mathf.Max(1, newColumnCount);
        serializedLayout.ApplyModifiedProperties();
        layout.RebuildLayout();

        EditorUtility.SetDirty(layout);
        PrefabUtility.RecordPrefabInstancePropertyModifications(layout);
        for (int index = 0; index < trayParent.childCount; index++)
        {
            Transform child = trayParent.GetChild(index);
            EditorUtility.SetDirty(child);
            PrefabUtility.RecordPrefabInstancePropertyModifications(child);
        }

        MarkLevelChanged(level);
        SceneView.RepaintAll();
    }

    private bool DrawModeButton(string label, EditMode mode, Color activeColor)
    {
        Rect rect = GUILayoutUtility.GetRect(100f, 38f, GUILayout.ExpandWidth(true));
        bool active = editMode == mode;
        if (active)
        {
            EditorGUI.DrawRect(rect, activeColor);
        }

        Rect buttonRect = active
            ? new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f)
            : rect;
        return GUI.Button(buttonRect, active ? $"✓  {label}" : label, EditorStyles.miniButton);
    }

    private void DrawCardPalette()
    {
        EditorGUILayout.LabelField("Card Icons", EditorStyles.boldLabel);

        if (cardConfig == null)
        {
            EditorGUILayout.HelpBox(
                "Keo CardConfig vao o Card List de hien icon card.",
                MessageType.Warning);
            return;
        }

        if (cardConfig.GetEditorCards().Count == 0)
        {
            EditorGUILayout.HelpBox("CardConfig khong co card nao.", MessageType.Warning);
            return;
        }

        CardData selectedData = cardConfig.GetEditorCardData(selectedCardType);
        if (selectedData == null)
        {
            CardData firstData = GetFirstValidCardData();
            if (firstData != null)
            {
                selectedCardType = firstData.cardType;
            }
        }

        EditorGUILayout.LabelField($"Selected: {GetCardDisplayName(selectedCardType)}", EditorStyles.helpBox);

        if (selectedCardType == CardType.DarkingCard)
        {
            int defaultMoveCount = cardConfig != null
                ? cardConfig.DefaultDarkKingMovesBeforeTrayLock
                : 3;
            if (darkKingMovesBeforeTrayLock <= 0)
                darkKingMovesBeforeTrayLock = defaultMoveCount;

            darkKingMovesBeforeTrayLock = Mathf.Max(
                1,
                EditorGUILayout.IntField(
                    new GUIContent(
                        "Dark King Lock Moves",
                        "So luot rieng cua Dark King sap duoc them vao level."),
                    darkKingMovesBeforeTrayLock
                )
            );
            EditorGUILayout.HelpBox(
                "Gia tri nay duoc luu tren tung la Dark King trong prefab level.",
                MessageType.Info
            );
        }

        if (selectedCardType == CardType.FrozenCard)
        {
            int defaultMoveCount = cardConfig.DefaultFrozenMovesBeforeOpen;
            if (frozenMovesBeforeOpen <= 0)
                frozenMovesBeforeOpen = defaultMoveCount;

            frozenMovesBeforeOpen = Mathf.Max(1, EditorGUILayout.IntField(
                new GUIContent("Frozen Open Moves",
                    "Number of moves until this frozen card can be used."),
                frozenMovesBeforeOpen));
            frozenCardValue = Mathf.Clamp(EditorGUILayout.IntField(
                new GUIContent("Reveals As Card",
                    "Number card added when using ADD CARD. Existing cards keep their own value."),
                frozenCardValue), 1, 20);
            EditorGUILayout.HelpBox(
                "ADD CARD places a frozen number card. FREEZE EXISTING CARD changes a card already in a tray.",
                MessageType.Info);
        }

        float availableWidth = Mathf.Max(PaletteItemWidth, position.width - 34f);
        int columnCount = Mathf.Max(1, Mathf.FloorToInt(availableWidth / (PaletteItemWidth + 6f)));

        _paletteScroll = EditorGUILayout.BeginScrollView(_paletteScroll, GUILayout.MinHeight(190f));
        int column = 0;

        foreach (CardData data in cardConfig.GetEditorCards())
        {
            if (data == null) continue;

            if (column == 0)
            {
                EditorGUILayout.BeginHorizontal();
            }

            DrawCardPaletteItem(data);
            column++;

            if (column >= columnCount)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                column = 0;
            }
        }

        if (column != 0)
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawCardPaletteItem(CardData data)
    {
        Rect itemRect = GUILayoutUtility.GetRect(
            PaletteItemWidth,
            PaletteItemHeight,
            GUILayout.Width(PaletteItemWidth),
            GUILayout.Height(PaletteItemHeight));

        bool selected = data.cardType == selectedCardType;
        bool obstacle = cardConfig != null &&
            cardConfig.IsObstacleLevelCard(data.cardType);
        Color background = selected
            ? new Color(0.16f, 0.55f, 0.95f, 0.9f)
            : obstacle
                ? new Color(0.48f, 0.18f, 0.12f, 1f)
                : new Color(0.22f, 0.22f, 0.22f, 1f);
        EditorGUI.DrawRect(itemRect, background);

        Rect buttonRect = new Rect(itemRect.x + 2f, itemRect.y + 2f, itemRect.width - 4f, itemRect.height - 4f);
        if (GUI.Button(buttonRect, GUIContent.none, GUI.skin.button))
        {
            selectedCardType = data.cardType;
            editMode = EditMode.AddCard;
            SetStatus($"Selected {GetCardDisplayName(data.cardType)}.", MessageType.Info);
            SceneView.RepaintAll();
            Repaint();
        }

        Rect iconRect = new Rect(itemRect.x + 9f, itemRect.y + 7f, itemRect.width - 18f, itemRect.width - 18f);
        DrawSprite(iconRect, data.icon);

        Rect labelRect = new Rect(itemRect.x + 3f, itemRect.yMax - 25f, itemRect.width - 6f, 20f);
        GUI.Label(labelRect, GetCardDisplayName(data.cardType), EditorStyles.centeredGreyMiniLabel);
    }

    private void DrawTargetEditor()
    {
        EditorGUILayout.LabelField("Level Targets", EditorStyles.boldLabel);
        targetCount = Mathf.Max(1, EditorGUILayout.IntField("Target Count", targetCount));

        using (new EditorGUI.DisabledScope(level == null || cardConfig == null))
        {
            if (GUILayout.Button(
                    $"+  ADD TARGET   {GetCardDisplayName(selectedCardType)}  x{targetCount}",
                    GUILayout.Height(32f)))
            {
                AddSelectedTarget();
            }
        }

        if (level == null || level.CardTargets == null || level.CardTargets.Count == 0)
        {
            EditorGUILayout.LabelField("No targets.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        int removeIndex = -1;
        for (int i = 0; i < level.CardTargets.Count; i++)
        {
            CardTarget target = level.CardTargets[i];
            if (target == null) continue;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            Rect iconRect = GUILayoutUtility.GetRect(34f, 34f, GUILayout.Width(34f), GUILayout.Height(34f));
            CardData targetData = cardConfig != null ? cardConfig.GetCardData(target.cardType) : null;
            DrawSprite(iconRect, targetData?.icon);

            EditorGUILayout.LabelField(
                $"{GetCardDisplayName(target.cardType)}     x{target.count}",
                EditorStyles.boldLabel,
                GUILayout.Height(34f));

            if (GUILayout.Button("X", GUILayout.Width(30f), GUILayout.Height(28f)))
            {
                removeIndex = i;
            }
            EditorGUILayout.EndHorizontal();
        }

        if (removeIndex >= 0)
        {
            RemoveTargetAt(removeIndex);
        }
    }

    private void DrawStatus()
    {
        Color color = _statusType switch
        {
            MessageType.Error => new Color(0.55f, 0.16f, 0.16f, 0.95f),
            MessageType.Warning => new Color(0.62f, 0.43f, 0.10f, 0.95f),
            _ => new Color(0.12f, 0.42f, 0.58f, 0.95f)
        };

        Rect statusRect = GUILayoutUtility.GetRect(80f, 36f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(statusRect, color);

        GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(10, 10, 0, 0),
            wordWrap = true
        };
        style.normal.textColor = Color.white;
        GUI.Label(statusRect, _statusMessage, style);
    }

    private void AddSelectedTarget()
    {
        if (level == null)
        {
            SetStatus("Chua chon Level.", MessageType.Warning);
            return;
        }

        CardData selectedData = cardConfig != null
            ? cardConfig.GetEditorCardData(selectedCardType)
            : null;
        if (selectedData != null &&
            cardConfig.IsPreLevelCard(selectedCardType))
        {
            SetStatus(
                "Pre-level card chi duoc dat trong Tray, khong the lam target.",
                MessageType.Warning);
            return;
        }

        SerializedObject serializedLevel = new SerializedObject(level);
        SerializedProperty targets = serializedLevel.FindProperty("cardTargets");
        if (targets == null)
        {
            SetStatus("Khong tim thay Card Targets trong Level.", MessageType.Error);
            return;
        }

        Undo.RecordObject(level, "Add Level Target");
        int existingIndex = -1;
        for (int i = 0; i < targets.arraySize; i++)
        {
            SerializedProperty item = targets.GetArrayElementAtIndex(i);
            if (item.FindPropertyRelative("cardType").enumValueIndex == (int)selectedCardType)
            {
                existingIndex = i;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            SerializedProperty count = targets
                .GetArrayElementAtIndex(existingIndex)
                .FindPropertyRelative("count");
            count.intValue = Mathf.Max(0, count.intValue) + targetCount;
        }
        else
        {
            int newIndex = targets.arraySize;
            targets.InsertArrayElementAtIndex(newIndex);
            SerializedProperty item = targets.GetArrayElementAtIndex(newIndex);
            item.FindPropertyRelative("cardType").enumValueIndex = (int)selectedCardType;
            item.FindPropertyRelative("count").intValue = targetCount;
        }

        serializedLevel.ApplyModifiedProperties();
        MarkLevelChanged(level);
        SetStatus(
            $"Added target {GetCardDisplayName(selectedCardType)} x{targetCount}.",
            MessageType.Info);
    }

    private void RemoveTargetAt(int index)
    {
        if (level == null) return;

        SerializedObject serializedLevel = new SerializedObject(level);
        SerializedProperty targets = serializedLevel.FindProperty("cardTargets");
        if (targets == null || index < 0 || index >= targets.arraySize) return;

        CardType removedType = (CardType)targets
            .GetArrayElementAtIndex(index)
            .FindPropertyRelative("cardType")
            .enumValueIndex;

        Undo.RecordObject(level, "Remove Level Target");
        targets.DeleteArrayElementAtIndex(index);
        serializedLevel.ApplyModifiedProperties();
        MarkLevelChanged(level);
        SetStatus($"Removed target {GetCardDisplayName(removedType)}.", MessageType.Info);
    }

    private static void MarkLevelChanged(Level changedLevel)
    {
        EditorUtility.SetDirty(changedLevel);
        PrefabUtility.RecordPrefabInstancePropertyModifications(changedLevel);
        if (changedLevel.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(changedLevel.gameObject.scene);
        }
    }

    private void SetStatus(string message, MessageType type)
    {
        _statusMessage = message;
        _statusType = type;
        Repaint();
    }

    private void DuringSceneGUI(SceneView sceneView)
    {
        if (Application.isPlaying) return;

        if (_clickBlockerStateCaptured && clickBlocker != null && clickBlocker.activeSelf)
        {
            clickBlocker.SetActive(false);
        }

        Level stageLevel = FindLevelInCurrentPrefabStage();
        if (stageLevel != null && level != stageLevel)
        {
            SetLevel(stageLevel);
            Repaint();
        }

        Event currentEvent = Event.current;
        if (currentEvent == null) return;

        CardSlotHolder holder = FindHolderAtMousePosition(currentEvent.mousePosition);

        if (holder != null && currentEvent.type == EventType.Repaint)
        {
            DrawHolderHighlight(holder);
        }

        if (!currentEvent.alt)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }

        if (currentEvent.type != EventType.MouseDown ||
            currentEvent.button != 0 ||
            currentEvent.alt)
        {
            return;
        }

        if (editMode == EditMode.AddTray)
        {
            AddTrayAtMousePosition(currentEvent.mousePosition);
        }
        else if (holder == null)
        {
            return;
        }
        else if (editMode == EditMode.AddCard)
        {
            AddCardToHolder(holder);
        }
        else if (editMode == EditMode.DeleteCard)
        {
            DeleteCardFromHolder(holder);
        }
        else if (editMode == EditMode.DeleteTray)
        {
            DeleteTray(holder);
        }
        else if (editMode == EditMode.FreezeCard)
        {
            FreezeCardAtMousePosition(holder, currentEvent.mousePosition);
        }

        currentEvent.Use();
        sceneView.Repaint();
        Repaint();
    }

    private CardSlotHolder FindHolderAtMousePosition(Vector2 mousePosition)
    {
        GameObject pickedObject = HandleUtility.PickGameObject(mousePosition, false);
        CardSlotHolder pickedHolder = pickedObject != null
            ? pickedObject.GetComponentInParent<CardSlotHolder>()
            : null;

        if (IsHolderInCurrentLevel(pickedHolder))
        {
            return pickedHolder;
        }

        if (level == null) return null;

        CardSlotHolder closestHolder = null;
        float closestDistance = float.MaxValue;
        CardSlotHolder[] holders = level.GetComponentsInChildren<CardSlotHolder>(true);

        foreach (CardSlotHolder holder in holders)
        {
            if (!IsHolderInCurrentLevel(holder) ||
                !TryGetHolderGuiRect(holder, out Rect holderRect) ||
                !holderRect.Contains(mousePosition))
            {
                continue;
            }

            float distance = (holderRect.center - mousePosition).sqrMagnitude;
            if (distance >= closestDistance) continue;

            closestDistance = distance;
            closestHolder = holder;
        }

        return closestHolder;
    }

    private bool IsHolderInCurrentLevel(CardSlotHolder holder)
    {
        return holder != null &&
               level != null &&
               holder.GetComponentInParent<Level>() == level;
    }

    private static bool TryGetHolderGuiRect(CardSlotHolder holder, out Rect guiRect)
    {
        Bounds bounds;
        Collider2D holderCollider = holder.GetComponent<Collider2D>();

        if (holderCollider != null)
        {
            bounds = holderCollider.bounds;
        }
        else
        {
            Renderer[] renderers = holder.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                guiRect = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    bounds.Encapsulate(renderers[index].bounds);
                }
            }
        }

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector2 cornerA = HandleUtility.WorldToGUIPoint(new Vector3(min.x, min.y, bounds.center.z));
        Vector2 cornerB = HandleUtility.WorldToGUIPoint(new Vector3(min.x, max.y, bounds.center.z));
        Vector2 cornerC = HandleUtility.WorldToGUIPoint(new Vector3(max.x, min.y, bounds.center.z));
        Vector2 cornerD = HandleUtility.WorldToGUIPoint(new Vector3(max.x, max.y, bounds.center.z));

        float minX = Mathf.Min(cornerA.x, cornerB.x, cornerC.x, cornerD.x);
        float maxX = Mathf.Max(cornerA.x, cornerB.x, cornerC.x, cornerD.x);
        float minY = Mathf.Min(cornerA.y, cornerB.y, cornerC.y, cornerD.y);
        float maxY = Mathf.Max(cornerA.y, cornerB.y, cornerC.y, cornerD.y);

        guiRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        const float clickPadding = 5f;
        guiRect.xMin -= clickPadding;
        guiRect.xMax += clickPadding;
        guiRect.yMin -= clickPadding;
        guiRect.yMax += clickPadding;
        return guiRect.width > 0f && guiRect.height > 0f;
    }

    private void AddTrayAtMousePosition(Vector2 mousePosition)
    {
        if (level == null)
        {
            SetStatus("Chua co Level de them Tray.", MessageType.Warning);
            return;
        }

        GameObject trayPrefab = GetSelectedTrayPrefab();
        if (trayPrefab == null)
        {
            SetStatus("Hay chon mot Tray Prefab hop le.", MessageType.Warning);
            return;
        }

        if (!EditorUtility.IsPersistent(trayPrefab))
        {
            SetStatus("Tray Prefab phai duoc keo tu Project.", MessageType.Warning);
            return;
        }

        CardSlotHolder prefabHolder = trayPrefab.GetComponentInChildren<CardSlotHolder>(true);
        if (prefabHolder == null)
        {
            SetStatus($"{trayPrefab.name} khong co component CardSlotHolder.", MessageType.Error);
            return;
        }

        Transform trayParent = GetTrayParent(level);
        if (!TryGetWorldPosition(mousePosition, trayParent.position.z, out Vector3 worldPosition))
        {
            SetStatus("Khong xac dinh duoc vi tri dat Tray.", MessageType.Warning);
            return;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add Tray To Level");
        Undo.RecordObject(level, "Add Tray To Level");

        GameObject trayObject = PrefabUtility.InstantiatePrefab(trayPrefab, trayParent) as GameObject;
        if (trayObject == null)
        {
            SetStatus("Khong instantiate duoc Tray Prefab.", MessageType.Error);
            return;
        }

        Undo.RegisterCreatedObjectUndo(trayObject, "Add Tray To Level");
        trayObject.transform.position = worldPosition;
        trayObject.transform.localRotation = Quaternion.identity;
        trayObject.transform.localScale = Vector3.one;

        CardSlotHolder newHolder = trayObject.GetComponentInChildren<CardSlotHolder>(true);
        AddHolderReference(level, newHolder);
        RebuildTrayLayout(trayParent);
        MarkLevelChanged(level);
        EditorUtility.SetDirty(newHolder);
        Undo.CollapseUndoOperations(undoGroup);

        Selection.activeGameObject = trayObject;
        SetStatus($"Added Tray: {trayPrefab.name}.", MessageType.Info);
    }

    private void DeleteTray(CardSlotHolder holder)
    {
        if (holder == null || level == null) return;

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Delete Tray From Level");
        Undo.RecordObject(level, "Delete Tray From Level");

        RemoveHolderReference(level, holder);
        string trayName = holder.name;
        Transform trayParent = holder.transform.parent;
        Undo.DestroyObjectImmediate(holder.gameObject);
        RebuildTrayLayout(trayParent);
        MarkLevelChanged(level);
        Undo.CollapseUndoOperations(undoGroup);

        SetStatus($"Deleted Tray: {trayName}.", MessageType.Info);
    }

    private static void RebuildTrayLayout(Transform trayParent)
    {
        if (trayParent == null) return;

        TrayLayoutGroup layout = trayParent.GetComponent<TrayLayoutGroup>();
        if (layout == null || !layout.AutoLayout) return;

        Undo.RegisterFullObjectHierarchyUndo(trayParent.gameObject, "Rebuild Tray Layout");
        layout.RebuildLayout();
        EditorUtility.SetDirty(layout);
    }

    private GameObject GetSelectedTrayPrefab()
    {
        if (_setting?.trayPrefabs == null || _setting.trayPrefabs.Count == 0)
        {
            return null;
        }

        selectedTrayPrefabIndex = Mathf.Clamp(
            selectedTrayPrefabIndex,
            0,
            _setting.trayPrefabs.Count - 1);
        return _setting.trayPrefabs[selectedTrayPrefabIndex];
    }

    private static Transform GetTrayParent(Level targetLevel)
    {
        Transform slotsRoot = targetLevel.transform.Find("SlotsRoot");
        if (slotsRoot != null) return slotsRoot;

        if (targetLevel.CardSlotHolders != null)
        {
            foreach (CardSlotHolder holder in targetLevel.CardSlotHolders)
            {
                if (holder != null && holder.transform.parent != null)
                {
                    return holder.transform.parent;
                }
            }
        }

        return targetLevel.transform;
    }

    private static bool TryGetWorldPosition(Vector2 guiPosition, float zPosition, out Vector3 worldPosition)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, zPosition));
        if (!plane.Raycast(ray, out float distance))
        {
            worldPosition = default;
            return false;
        }

        worldPosition = ray.GetPoint(distance);
        worldPosition.z = zPosition;
        return true;
    }

    private static void AddHolderReference(Level targetLevel, CardSlotHolder holder)
    {
        if (targetLevel == null || holder == null) return;

        SerializedObject serializedLevel = new SerializedObject(targetLevel);
        SerializedProperty holders = serializedLevel.FindProperty("cardSlotHolders");
        if (holders == null) return;

        int newIndex = holders.arraySize;
        holders.InsertArrayElementAtIndex(newIndex);
        holders.GetArrayElementAtIndex(newIndex).objectReferenceValue = holder;
        serializedLevel.ApplyModifiedProperties();
    }

    private static void RemoveHolderReference(Level targetLevel, CardSlotHolder holder)
    {
        SerializedObject serializedLevel = new SerializedObject(targetLevel);
        SerializedProperty holders = serializedLevel.FindProperty("cardSlotHolders");
        if (holders == null) return;

        for (int i = holders.arraySize - 1; i >= 0; i--)
        {
            if (holders.GetArrayElementAtIndex(i).objectReferenceValue != holder) continue;

            int oldSize = holders.arraySize;
            holders.DeleteArrayElementAtIndex(i);
            if (holders.arraySize == oldSize)
            {
                holders.DeleteArrayElementAtIndex(i);
            }
        }

        serializedLevel.ApplyModifiedProperties();
    }

    private void AddCardToHolder(CardSlotHolder holder)
    {
        Level holderLevel = holder.GetComponentInParent<Level>();
        CardData data = cardConfig != null
            ? cardConfig.GetEditorCardData(selectedCardType)
            : null;
        if (data == null)
        {
            SetStatus("Hay chon mot icon card truoc.", MessageType.Warning);
            return;
        }

        Card selectedPrefab = holderLevel != null
            ? holderLevel.GetCardPrefab(selectedCardType)
            : null;
        if (holderLevel == null || selectedPrefab == null)
        {
            SetStatus($"Chua gan prefab cho {selectedCardType}.", MessageType.Error);
            return;
        }

        List<CardSlot> slots = GetOrderedSlots(holder);
        CardSlot targetSlot = null;
        int targetIndex = -1;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && slots[i].IsEmpty)
            {
                targetSlot = slots[i];
                targetIndex = i;
                break;
            }
        }

        if (targetSlot == null)
        {
            SetStatus("Tray da day.", MessageType.Warning);
            return;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add Card To Tray");
        Undo.RecordObject(holder, "Add Card To Tray");
        Undo.RecordObject(targetSlot, "Add Card To Tray");

        GameObject cardObject = PrefabUtility.InstantiatePrefab(
            selectedPrefab.gameObject,
            targetSlot.transform) as GameObject;

        if (cardObject == null)
        {
            cardObject = Instantiate(selectedPrefab.gameObject, targetSlot.transform);
        }

        Undo.RegisterCreatedObjectUndo(cardObject, "Add Card To Tray");

        Transform cardTransform = cardObject.transform;
        cardTransform.localPosition = Vector3.zero;
        cardTransform.localRotation = Quaternion.identity;
        cardTransform.localScale = Vector3.one;

        Card card = cardObject.GetComponent<Card>();
        if (card == null)
        {
            Undo.DestroyObjectImmediate(cardObject);
            SetStatus("Card Prefab khong co component Card.", MessageType.Error);
            return;
        }

        if (data.cardType == CardType.FrozenCard)
        {
            card.CardType = (CardType)(frozenCardValue - 1);
            card.SetIcon(data.icon);
            card.SetFrozenMovesBeforeOpen(frozenMovesBeforeOpen);
        }
        else if (data.cardType == CardType.WildCard)
            card.SetAsWild(data.icon);
        else
        {
            card.CardType = data.cardType;
            card.SetIcon(data.icon);
        }

        if (data.cardType == CardType.DarkingCard)
        {
            card.SetDarkKingMovesBeforeTrayLock(
                darkKingMovesBeforeTrayLock
            );
        }
        targetSlot.SetCard(card);

        SyncSerializedCardTypes(holder, slots);
        MarkChanged(holder, targetSlot, card);
        Undo.CollapseUndoOperations(undoGroup);

        SetStatus(
            $"Added {GetCardDisplayName(data.cardType)} to Tray / Slot {targetIndex + 1}.",
            MessageType.Info);
    }

    private void FreezeCardAtMousePosition(CardSlotHolder holder,
        Vector2 mousePosition)
    {
        GameObject pickedObject = HandleUtility.PickGameObject(mousePosition, false);
        Card card = pickedObject != null
            ? pickedObject.GetComponentInParent<Card>()
            : null;
        if (card == null)
            card = holder.GetTopCard();
        if (card == null || card.GetComponentInParent<CardSlotHolder>() != holder)
        {
            SetStatus("Click a tray containing a number card to freeze it.",
                MessageType.Warning);
            return;
        }

        if (!Card.IsNumberCardType(card.CardType))
        {
            SetStatus("Only number cards can be frozen.", MessageType.Warning);
            return;
        }

        CardData frozenData = cardConfig != null
            ? cardConfig.GetEditorCardData(CardType.FrozenCard)
            : null;
        if (frozenData == null || frozenData.icon == null)
        {
            SetStatus("FrozenCard needs an icon in CardConfig.",
                MessageType.Error);
            return;
        }

        CardSlot slot = card.GetComponentInParent<CardSlot>();
        Undo.RecordObject(card, "Freeze Number Card");
        if (card.IconRenderer != null)
            Undo.RecordObject(card.IconRenderer, "Freeze Number Card");
        card.SetFrozenMovesBeforeOpen(frozenMovesBeforeOpen);
        card.SetIcon(frozenData.icon);
        MarkChanged(holder, slot, card);
        SetStatus($"Frozen {GetCardDisplayName(card.CardType)} for " +
                  $"{frozenMovesBeforeOpen} moves.", MessageType.Info);
    }

    private void DeleteCardFromHolder(CardSlotHolder holder)
    {
        List<CardSlot> slots = GetOrderedSlots(holder);
        CardSlot targetSlot = null;
        int targetIndex = -1;

        for (int i = slots.Count - 1; i >= 0; i--)
        {
            if (slots[i] != null && !slots[i].IsEmpty)
            {
                targetSlot = slots[i];
                targetIndex = i;
                break;
            }
        }

        if (targetSlot == null)
        {
            SetStatus("Tray khong co card de xoa.", MessageType.Warning);
            return;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Delete Card From Tray");
        Undo.RecordObject(holder, "Delete Card From Tray");
        Undo.RecordObject(targetSlot, "Delete Card From Tray");

        Card card = targetSlot.RemoveCard();
        SyncSerializedCardTypes(holder, slots);
        MarkChanged(holder, targetSlot, null);

        if (card != null)
        {
            Undo.DestroyObjectImmediate(card.gameObject);
        }

        Undo.CollapseUndoOperations(undoGroup);
        SetStatus($"Deleted card at Tray / Slot {targetIndex + 1}.", MessageType.Info);
    }

    private static List<CardSlot> GetOrderedSlots(CardSlotHolder holder)
    {
        List<CardSlot> slots = holder.CardSlots;
        if (slots != null && slots.Count > 0)
        {
            return slots;
        }

        return new List<CardSlot>(holder.GetComponentsInChildren<CardSlot>(true));
    }

    private static void SyncSerializedCardTypes(CardSlotHolder holder, List<CardSlot> slots)
    {
        SerializedObject serializedHolder = new SerializedObject(holder);
        SerializedProperty cardTypes = serializedHolder.FindProperty("cardTypes");
        if (cardTypes == null) return;

        cardTypes.ClearArray();
        int resultIndex = 0;

        foreach (CardSlot slot in slots)
        {
            if (slot == null || slot.Card == null) continue;

            cardTypes.InsertArrayElementAtIndex(resultIndex);
            cardTypes.GetArrayElementAtIndex(resultIndex).enumValueIndex = (int)slot.Card.CardType;
            resultIndex++;
        }

        serializedHolder.ApplyModifiedProperties();
    }

    private static void MarkChanged(CardSlotHolder holder, CardSlot slot, Card card)
    {
        EditorUtility.SetDirty(holder);
        EditorUtility.SetDirty(slot);
        if (card != null)
        {
            EditorUtility.SetDirty(card);
            if (card.IconRenderer != null)
            {
                EditorUtility.SetDirty(card.IconRenderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    card.IconRenderer);
            }
        }

        PrefabUtility.RecordPrefabInstancePropertyModifications(holder);
        PrefabUtility.RecordPrefabInstancePropertyModifications(slot);
        if (card != null)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(card);
        }

        if (holder.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(holder.gameObject.scene);
        }
    }

    private static void DrawHolderHighlight(CardSlotHolder holder)
    {
        Renderer[] renderers = holder.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(holder.transform.position, Vector3.one * 0.5f);
        bool hasBounds = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        Handles.color = new Color(0.15f, 0.85f, 1f, 1f);
        Handles.DrawWireCube(bounds.center, bounds.size + new Vector3(0.08f, 0.08f, 0f));

        List<CardSlot> slots = GetOrderedSlots(holder);
        int cardCount = 0;
        foreach (CardSlot slot in slots)
        {
            if (slot != null && !slot.IsEmpty) cardCount++;
        }

        Vector3 labelPosition = bounds.center + Vector3.up * (bounds.extents.y + 0.12f);
        Handles.Label(labelPosition, $"Tray  {cardCount}/{slots.Count}", EditorStyles.whiteMiniLabel);
    }

    private void InitializeFromContext()
    {
        Level stageLevel = FindLevelInCurrentPrefabStage();
        if (stageLevel != null)
        {
            SetLevel(stageLevel);
            return;
        }

        Level selectedLevel = FindLevelFromSelection();
        if (selectedLevel != null)
        {
            SetLevel(selectedLevel);
        }
    }

    private void OnSelectionChanged()
    {
        Level selectedLevel = FindLevelFromSelection();
        if (selectedLevel != null)
        {
            SetLevel(selectedLevel);
            Repaint();
        }
    }

    private void SetLevel(Level newLevel)
    {
        if (level == newLevel)
        {
            return;
        }

        RestoreClickBlocker();
        level = newLevel;
        if (level != null && level.CardConfig != null && cardConfig == null)
        {
            SetCardConfig(level.CardConfig);
        }

        ResolveSavedClickBlocker();
    }

    private void SetCardConfig(CardConfig newCardConfig)
    {
        cardConfig = newCardConfig;

        if (_setting != null && _setting.cardConfig != newCardConfig)
        {
            Undo.RecordObject(_setting, "Change Level Editor Card List");
            _setting.cardConfig = newCardConfig;
            EditorUtility.SetDirty(_setting);
            AssetDatabase.SaveAssetIfDirty(_setting);
        }

        CardData selectedData = cardConfig != null
            ? cardConfig.GetEditorCardData(selectedCardType)
            : null;
        if (selectedData == null)
        {
            CardData firstData = GetFirstValidCardData();
            if (firstData != null)
            {
                selectedCardType = firstData.cardType;
            }
        }

        Repaint();
    }

    private void SetClickBlocker(GameObject newClickBlocker)
    {
        RestoreClickBlocker();
        clickBlocker = newClickBlocker;
        SaveClickBlockerPath();
        DisableClickBlocker();
    }

    private void SaveClickBlockerPath()
    {
        if (_setting == null) return;

        string relativePath = string.Empty;
        if (clickBlocker != null && level != null && clickBlocker.transform.IsChildOf(level.transform))
        {
            relativePath = AnimationUtility.CalculateTransformPath(clickBlocker.transform, level.transform);
            if (string.IsNullOrEmpty(relativePath) && clickBlocker.transform == level.transform)
            {
                relativePath = ".";
            }
        }
        else if (clickBlocker != null)
        {
            SetStatus(
                "Click Blocker van duoc tat, nhung chi luu tu dong neu no nam ben trong Level.",
                MessageType.Warning);
        }

        if (_setting.clickBlockerPath == relativePath) return;

        _setting.clickBlockerPath = relativePath;
        EditorUtility.SetDirty(_setting);
        AssetDatabase.SaveAssetIfDirty(_setting);
    }

    private void ResolveSavedClickBlocker()
    {
        if (_setting == null ||
            level == null ||
            EditorUtility.IsPersistent(level) ||
            string.IsNullOrEmpty(_setting.clickBlockerPath))
        {
            return;
        }

        Transform blockerTransform = _setting.clickBlockerPath == "."
            ? level.transform
            : level.transform.Find(_setting.clickBlockerPath);

        if (blockerTransform == null)
        {
            clickBlocker = null;
            SetStatus("Khong tim thay Click Blocker da luu trong Level nay.", MessageType.Warning);
            return;
        }

        clickBlocker = blockerTransform.gameObject;
        DisableClickBlocker();
    }

    private void DisableClickBlocker()
    {
        if (clickBlocker == null || _clickBlockerStateCaptured) return;

        if (EditorUtility.IsPersistent(clickBlocker))
        {
            SetStatus("Click Blocker phai duoc keo tu Hierarchy, khong phai prefab asset.", MessageType.Warning);
            return;
        }

        _clickBlockerWasActive = clickBlocker.activeSelf;
        _clickBlockerStateCaptured = true;
        if (clickBlocker.activeSelf)
        {
            clickBlocker.SetActive(false);
        }

        SetStatus($"Disabled click blocker: {clickBlocker.name}.", MessageType.Info);
        SceneView.RepaintAll();
    }

    private void RestoreClickBlocker()
    {
        if (!_clickBlockerStateCaptured) return;

        if (clickBlocker != null)
        {
            clickBlocker.SetActive(_clickBlockerWasActive);
        }

        _clickBlockerStateCaptured = false;
        SceneView.RepaintAll();
    }

    private CardData GetFirstValidCardData()
    {
        if (cardConfig == null) return null;
        foreach (CardData data in cardConfig.GetEditorCards())
        {
            if (data != null) return data;
        }

        return null;
    }

    private static Level FindLevelFromSelection()
    {
        if (Selection.activeGameObject != null)
        {
            return Selection.activeGameObject.GetComponentInParent<Level>();
        }

        GameObject selectedPrefab = Selection.activeObject as GameObject;
        return selectedPrefab != null ? selectedPrefab.GetComponent<Level>() : null;
    }

    private static Level FindLevelInCurrentPrefabStage()
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        return stage != null && stage.prefabContentsRoot != null
            ? stage.prefabContentsRoot.GetComponentInChildren<Level>(true)
            : null;
    }

    private static string GetCardDisplayName(CardType type)
    {
        return type.ToString().Replace("Card", "Card ");
    }

    private static void DrawSprite(Rect rect, Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
        {
            GUI.Label(rect, "No Icon", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        Rect spriteRect = sprite.rect;
        Texture2D texture = sprite.texture;
        float sourceAspect = spriteRect.width / Mathf.Max(1f, spriteRect.height);
        float targetAspect = rect.width / Mathf.Max(1f, rect.height);
        Rect fittedRect = rect;

        if (sourceAspect > targetAspect)
        {
            float fittedHeight = rect.width / sourceAspect;
            fittedRect.y += (rect.height - fittedHeight) * 0.5f;
            fittedRect.height = fittedHeight;
        }
        else
        {
            float fittedWidth = rect.height * sourceAspect;
            fittedRect.x += (rect.width - fittedWidth) * 0.5f;
            fittedRect.width = fittedWidth;
        }

        Rect uv = new Rect(
            spriteRect.x / texture.width,
            spriteRect.y / texture.height,
            spriteRect.width / texture.width,
            spriteRect.height / texture.height);
        GUI.DrawTextureWithTexCoords(fittedRect, texture, uv, true);
    }
}
