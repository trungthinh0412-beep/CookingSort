#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CardConfig))]
public sealed class CardConfigEditor : Editor
{
    private static readonly string[] TabNames =
    {
        "Cards",
        "Pre-Level Cards",
        "Obstacle-Level Cards"
    };

    private SerializedProperty _cards;
    private SerializedProperty _preLevelCards;
    private SerializedProperty _obstacleLevelCards;
    private SerializedProperty _numberCardPrefab;
    private SerializedProperty _wildCardPrefab;
    private SerializedProperty _downgradeCardPrefab;
    private SerializedProperty _chainCardPrefab;
    private SerializedProperty _frozenCardPrefab;
    private SerializedProperty _ironCardPrefab;
    private SerializedProperty _darkKingCardPrefab;
    private SerializedProperty _defaultDarkKingMovesBeforeTrayLock;
    private SerializedProperty _defaultFrozenMovesBeforeOpen;
    private int _selectedTab;

    private void OnEnable()
    {
        _cards = serializedObject.FindProperty("cards");
        _preLevelCards = serializedObject.FindProperty("preLevelCards");
        _obstacleLevelCards = serializedObject.FindProperty(
            "obstacleLevelCards"
        );
        _numberCardPrefab = serializedObject.FindProperty("numberCardPrefab");
        _wildCardPrefab = serializedObject.FindProperty("wildCardPrefab");
        _downgradeCardPrefab = serializedObject.FindProperty("downgradeCardPrefab");
        _chainCardPrefab = serializedObject.FindProperty("chainCardPrefab");
        _frozenCardPrefab = serializedObject.FindProperty("frozenCardPrefab");
        _ironCardPrefab = serializedObject.FindProperty("ironCardPrefab");
        _darkKingCardPrefab = serializedObject.FindProperty("darkKingCardPrefab");
        _defaultDarkKingMovesBeforeTrayLock = serializedObject.FindProperty(
            "defaultDarkKingMovesBeforeTrayLock"
        );
        _defaultFrozenMovesBeforeOpen = serializedObject.FindProperty(
            "defaultFrozenMovesBeforeOpen"
        );
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField(
            "Card Config",
            EditorStyles.boldLabel
        );
        _selectedTab = GUILayout.Toolbar(_selectedTab, TabNames);
        EditorGUILayout.Space(6f);

        EditorGUILayout.LabelField("Card Prefabs", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_numberCardPrefab);
        EditorGUILayout.PropertyField(_wildCardPrefab);
        EditorGUILayout.PropertyField(_downgradeCardPrefab);
        EditorGUILayout.PropertyField(_chainCardPrefab);
        EditorGUILayout.PropertyField(_frozenCardPrefab);
        EditorGUILayout.PropertyField(_ironCardPrefab);
        EditorGUILayout.PropertyField(_darkKingCardPrefab);
        EditorGUILayout.Space(6f);

        if (_defaultDarkKingMovesBeforeTrayLock != null)
        {
            EditorGUILayout.LabelField(
                "Dark King Card Defaults",
                EditorStyles.boldLabel
            );
            EditorGUILayout.PropertyField(
                _defaultDarkKingMovesBeforeTrayLock,
                new GUIContent("Default Moves Before Tray Lock")
            );
            EditorGUILayout.Space(6f);
        }

        if (_defaultFrozenMovesBeforeOpen != null)
        {
            EditorGUILayout.LabelField("Frozen Card Defaults", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                _defaultFrozenMovesBeforeOpen,
                new GUIContent("Default Moves Before Open")
            );
            EditorGUILayout.Space(6f);
        }

        SerializedProperty activeList = _selectedTab switch
        {
            0 => _cards,
            1 => _preLevelCards,
            _ => _obstacleLevelCards
        };

        if (activeList != null)
        {
            EditorGUILayout.PropertyField(
                activeList,
                GUIContent.none,
                includeChildren: true
            );
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
