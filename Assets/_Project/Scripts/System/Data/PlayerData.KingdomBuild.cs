using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class KingdomBuildSlotSaveData
{
    public string roomId;
    public string slotId;
}

public partial class PlayerData
{
    [SerializeField]
    private List<KingdomBuildSlotSaveData> completedKingdomBuildSlots =
        new List<KingdomBuildSlotSaveData>();

    public List<KingdomBuildSlotSaveData> CompletedKingdomBuildSlots
    {
        get
        {
            if (completedKingdomBuildSlots == null)
                completedKingdomBuildSlots = new List<KingdomBuildSlotSaveData>();

            return completedKingdomBuildSlots;
        }
        set => completedKingdomBuildSlots = value ?? new List<KingdomBuildSlotSaveData>();
    }

    public bool IsKingdomBuildSlotCompleted(string roomId, string slotId)
    {
        string safeRoomId = NormalizeBuildId(roomId);
        string safeSlotId = NormalizeBuildId(slotId);

        if (string.IsNullOrEmpty(safeRoomId) || string.IsNullOrEmpty(safeSlotId))
            return false;

        foreach (KingdomBuildSlotSaveData savedSlot in CompletedKingdomBuildSlots)
        {
            if (savedSlot == null)
                continue;

            if (string.Equals(savedSlot.roomId, safeRoomId, StringComparison.Ordinal) &&
                string.Equals(savedSlot.slotId, safeSlotId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public void SetKingdomBuildSlotCompleted(string roomId, string slotId)
    {
        string safeRoomId = NormalizeBuildId(roomId);
        string safeSlotId = NormalizeBuildId(slotId);

        if (string.IsNullOrEmpty(safeRoomId) || string.IsNullOrEmpty(safeSlotId))
            return;

        if (IsKingdomBuildSlotCompleted(safeRoomId, safeSlotId))
            return;

        CompletedKingdomBuildSlots.Add(new KingdomBuildSlotSaveData
        {
            roomId = safeRoomId,
            slotId = safeSlotId,
        });
    }

    private static string NormalizeBuildId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }
}
