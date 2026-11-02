using UnityEngine;

public abstract class SingletonDontDestroy<T> : MonoBehaviour where T : MonoBehaviour
{
    static T _instance;

    public static T Instance => _instance ??= FindAnyObjectByType<T>();

    protected virtual void Awake()
    {
#if UNITY_EDITOR
        // PrefabUtility.LoadPrefabContents creates a preview scene while the
        // Editor may still be in Play Mode. Preview objects must never become
        // runtime singletons or be moved to DontDestroyOnLoad.
        if (UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(
                gameObject.scene))
        {
            return;
        }
#endif

        T current = this as T;
        if (_instance != null && _instance != current)
        {
            Destroy(gameObject);
            return;
        }

        _instance = current;

        // Child controllers are already preserved by their persistent root.
        // Calling DontDestroyOnLoad on a child produces a Unity warning.
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }
}
