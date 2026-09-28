using System;
using System.Globalization;
using UnityEngine;

public partial class PlayerData
{
    [SerializeField] private string removeAdsExpiryDate = DateTime.UtcNow.AddDays(-1).ToString(Utility.DateTimeFormat, CultureInfo.InvariantCulture);

    public string RemoveAdsExpiryDate
    {
        get => removeAdsExpiryDate;
        set => removeAdsExpiryDate = value;
    }
    

    public bool IsRemoveAds
    {
        get
        {
            if (string.IsNullOrEmpty(removeAdsExpiryDate)) return false;
            if (!DateTime.TryParseExact(removeAdsExpiryDate, Utility.DateTimeFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var expiry)) return false;
            return DateTime.UtcNow < expiry;
        }
    }

    public bool IsFirstClaimToday(string lastClaimDateStr)
    {
        if (string.IsNullOrEmpty(lastClaimDateStr)) return true;
        if (!DateTime.TryParseExact(lastClaimDateStr, Utility.DateTimeFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var lastClaim)) return true;
        return lastClaim.Date < DateTime.UtcNow.Date;
    }
}
