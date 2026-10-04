using UnityEditor;
using UnityEngine;
using System.IO;

public class AnalyzePopups {
    public static void Execute() {
        var pb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Popup/PopupBooster.prefab");
        var pq = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Popup/PopupBoosterQuit.prefab");
        
        using (StreamWriter w = new StreamWriter("popup_diff.txt")) {
            w.WriteLine("=== PopupBooster === ");
            Dump(pb.transform, "", w);
            w.WriteLine("\n=== PopupBoosterQuit === ");
            Dump(pq.transform, "", w);
        }
    }
    
    static void Dump(Transform t, string indent, StreamWriter w) {
        w.WriteLine($"{indent}{t.name}");
        foreach(Transform c in t) Dump(c, indent + "  ", w);
    }
}
