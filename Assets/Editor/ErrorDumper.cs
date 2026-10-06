using UnityEditor;
using UnityEngine;
using System.IO;
using System.Reflection;

public static class ErrorDumper
{
    [MenuItem("Tools/Dump Errors")]
    public static void Dump()
    {
        var assembly = Assembly.GetAssembly(typeof(SceneView));
        var type = assembly.GetType("UnityEditor.LogEntries");
        var getCountsMethod = type.GetMethod("GetCountsByType");
        
        int errorCount = 0;
        int warningCount = 0;
        int logCount = 0;
        
        var countsArgs = new object[] { errorCount, warningCount, logCount };
        getCountsMethod.Invoke(null, countsArgs);
        
        errorCount = (int)countsArgs[0];
        
        var startMethod = type.GetMethod("StartGettingEntries");
        startMethod.Invoke(null, null);
        
        var entryType = assembly.GetType("UnityEditor.LogEntry");
        var entry = System.Activator.CreateInstance(entryType);
        
        var getEntryInternalMethod = type.GetMethod("GetEntryInternal", BindingFlags.Static | BindingFlags.Public);
        var conditionField = entryType.GetField("condition", BindingFlags.Instance | BindingFlags.Public);
        
        using (StreamWriter writer = new StreamWriter("MyErrors.txt"))
        {
            writer.WriteLine("Total errors: " + errorCount);
            int count = (int)type.GetMethod("GetCount").Invoke(null, null);
            for (int i = 0; i < count; i++)
            {
                getEntryInternalMethod.Invoke(null, new object[] { i, entry });
                int mode = (int)entryType.GetField("mode", BindingFlags.Instance | BindingFlags.Public).GetValue(entry);
                
                // mode 2 = Error, 6 = Fatal, etc. Using bitwise checks or just matching specific bits
                if ((mode & 2) != 0 || (mode & 16) != 0 || (mode & 64) != 0 || (mode & 131072) != 0 || (mode & 4) != 0) 
                {
                    string cond = (string)conditionField.GetValue(entry);
                    writer.WriteLine(cond);
                }
            }
        }
        
        var endMethod = type.GetMethod("EndGettingEntries");
        endMethod.Invoke(null, null);
        
        Debug.Log("Dumped errors to MyErrors.txt");
    }
}
