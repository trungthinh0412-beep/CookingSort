using CookingSort2D;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
internal static class AnimalDemandFillSetup
{
    private const string PrefabPath =
        "Assets/_Project/CookingBlast/Prefabs/Animal/AnimalBase.prefab";

    static AnimalDemandFillSetup()
    {
        EditorApplication.delayCall += EnsureDemandFill;
    }

    private static void EnsureDemandFill()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling)
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null) return;

        try
        {
            CustomerView customer = root.GetComponent<CustomerView>();
            if (customer == null) return;

            Transform canvasTransform = root.transform.Find("Canvas");
            Canvas canvas = canvasTransform != null
                ? canvasTransform.GetComponent<Canvas>()
                : null;
            Image fill = canvasTransform != null
                ? canvasTransform.Find("Bar/Fill")?.GetComponent<Image>()
                : null;

            if (canvas == null || fill == null) return;

            // This UI belongs to the animal in world space. The original Canvas
            // was Screen Space - Camera with a zero scale, so it was invisible.
            RectTransform canvasRect = (RectTransform)canvas.transform;
            canvasRect.localPosition = new Vector3(0f, -0.89f, 0f);
            canvasRect.localRotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one * 0.004f;

            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = null;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 25;

            RectTransform barRect = canvasTransform.Find("Bar") as RectTransform;
            if (barRect != null)
                barRect.anchoredPosition = Vector2.zero;

            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;

            foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;

            // Remove the temporary duplicate Canvas created by the first setup.
            Transform duplicate = root.transform.Find("DemandFillCanvas");
            if (duplicate != null)
                Object.DestroyImmediate(duplicate.gameObject);

            var serializedCustomer = new SerializedObject(customer);
            SerializedProperty fillProperty =
                serializedCustomer.FindProperty("demandFill");
            if (fillProperty != null && fillProperty.objectReferenceValue != fill)
            {
                fillProperty.objectReferenceValue = fill;
                serializedCustomer.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(customer);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
