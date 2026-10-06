using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PopupConfig", menuName = "ScriptableObject/PopupConfig")]
public partial class PopupConfig : ScriptableObject
{
    public float durationPopup = .5f;
    public List<Popup> popups;
    public PopupDebugConsole popupDebugConsole;
}
public partial class PopupConfig { public System.Collections.Generic.List<UnityEngine.GameObject> popupPrefabs; }
