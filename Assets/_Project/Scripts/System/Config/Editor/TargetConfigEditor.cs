using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TargetConfig))]
public class TargetConfigEditor : Editor
{
    private SerializedProperty _spriteSheet;
    private SerializedProperty _targets;
    private string _message;
    private MessageType _messageType = MessageType.Info;

    private void OnEnable()
    {
        _spriteSheet = serializedObject.FindProperty("spriteSheet");
        _targets = serializedObject.FindProperty("targets");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField(
            "Target Sprite Sheet",
            EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(
            _spriteSheet,
            new GUIContent(
                "Sprite Sheet (Multiple)",
                "Keo texture da Slice voi Sprite Mode = Multiple vao day."));
        bool changedSpriteSheet = EditorGUI.EndChangeCheck();

        serializedObject.ApplyModifiedProperties();

        if (changedSpriteSheet && _spriteSheet.objectReferenceValue != null)
            AutoFill();

        using (new EditorGUI.DisabledScope(
                   _spriteSheet.objectReferenceValue == null))
        {
            if (GUILayout.Button("Auto Fill Target Sprites", GUILayout.Height(28f)))
                AutoFill();
        }

        if (!string.IsNullOrEmpty(_message))
            EditorGUILayout.HelpBox(_message, _messageType);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Target Icons", EditorStyles.boldLabel);

        serializedObject.Update();
        EditorGUILayout.PropertyField(_targets, true);
        serializedObject.ApplyModifiedProperties();
    }

    private void AutoFill()
    {
        TargetConfig config = (TargetConfig)target;
        Texture2D sheet = _spriteSheet.objectReferenceValue as Texture2D;

        if (sheet == null)
        {
            SetMessage("Hay keo Sprite Sheet vao truoc.", MessageType.Warning);
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(sheet);
        List<Sprite> sprites = AssetDatabase
            .LoadAllAssetRepresentationsAtPath(assetPath)
            .OfType<Sprite>()
            .OrderByDescending(sprite => sprite.rect.y)
            .ThenBy(sprite => sprite.rect.x)
            .ToList();

        if (sprites.Count == 0)
        {
            SetMessage(
                "Khong tim thay sprite con. Hay dat Sprite Mode = Multiple, Slice va Apply trong Sprite Editor.",
                MessageType.Error);
            return;
        }

        Undo.RecordObject(config, "Auto Fill Target Sprites");

        Dictionary<CardType, TargetIconData> existing =
            (config.targets ?? new List<TargetIconData>())
            .Where(item => item != null)
            .GroupBy(item => item.cardType)
            .ToDictionary(group => group.Key, group => group.First());

        CardType[] cardTypes = Enum.GetValues(typeof(CardType))
            .Cast<CardType>()
            .Where(type => type != CardType.WildCard &&
                           type != CardType.StackCard &&
                           type != CardType.UpgradeCard &&
                           type != CardType.KingCard &&
                           type != CardType.DowngradeCard &&
                           type != CardType.ChainCard &&
                           type != CardType.FrozenCard &&
                           type != CardType.IronCard &&
                           type != CardType.DarkingCard)
            .OrderBy(type => (int)type)
            .ToArray();

        List<TargetIconData> rebuilt =
            new List<TargetIconData>(cardTypes.Length);

        for (int i = 0; i < cardTypes.Length; i++)
        {
            CardType cardType = cardTypes[i];
            if (!existing.TryGetValue(cardType, out TargetIconData data))
                data = new TargetIconData();

            data.cardType = cardType;
            data.icon = i < sprites.Count ? sprites[i] : null;
            rebuilt.Add(data);
        }

        config.targets = rebuilt;
        EditorUtility.SetDirty(config);
        serializedObject.Update();

        int assignedCount = Mathf.Min(sprites.Count, cardTypes.Length);
        SetMessage(
            $"Da gan {assignedCount}/{cardTypes.Length} target sprite theo vi tri tren sprite sheet.",
            sprites.Count < cardTypes.Length
                ? MessageType.Warning
                : MessageType.Info);
    }

    private void SetMessage(string message, MessageType type)
    {
        _message = message;
        _messageType = type;
        Repaint();
    }

}
