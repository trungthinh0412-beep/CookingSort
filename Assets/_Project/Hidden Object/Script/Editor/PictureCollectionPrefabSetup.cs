using System;
using System.IO;
using System.Linq;
using MagicSoft.Differences;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class PictureCollectionPrefabSetup
{
    public const string Root = "Assets/_Project/Hidden Object";
    public const string ConfigPath = Root + "/Data/PictureCollectionConfig.asset";
    public const string AlbumPath = Root + "/Data/Albums/SampleAlbum.asset";
    public const string UI = Root + "/Prefabs/UI";
    public const string HomePath = "Assets/_Project/Prefabs/Popup/PopupHome.prefab";
    private static TMP_FontAsset font;
    private static Sprite panel;
    private static readonly System.Collections.Generic.HashSet<GameObject> loadedPages = new System.Collections.Generic.HashSet<GameObject>();
    private static readonly Color Blue = new Color(.27f,.46f,.75f);
    private static readonly Color DarkBlue = new Color(.09f,.34f,.54f);
    private static readonly Color Paper = new Color(.85f,.97f,.98f);

    [MenuItem("Tools/Find Differences/Setup Picture Collection UI")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        Directory.CreateDirectory(Root + "/Data/Albums");
        Directory.CreateDirectory(UI + "/Elements");
        Directory.CreateDirectory(UI + "/Popup");
        AssetDatabase.Refresh();
        var homeAsset = AssetDatabase.LoadAssetAtPath<GameObject>(HomePath);
        font = new SerializedObject(homeAsset.GetComponent<PopupHome>()).FindProperty("levelText").objectReferenceValue is TMP_Text text
            ? text.font : TMP_Settings.defaultFontAsset;
        panel = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var config = AssetDatabase.LoadAssetAtPath<PictureCollectionConfig>(ConfigPath);
        if (config == null)
        {
            var album = ScriptableObject.CreateInstance<PictureAlbumData>();
            album.albumId = "sample-album";
            album.title = "Sample Collection";
            album.levels.Add(new PictureLevelEntry { levelNumber = 1,
                levelPrefab = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(DifferencePrefabSetup.LevelPath)) });
            SyncAlbum(album);
            album.coverSprite = album.levels[0].pictureA;
            AssetDatabase.CreateAsset(album, AlbumPath);
            config = ScriptableObject.CreateInstance<PictureCollectionConfig>();
            config.albums.Add(album);
            AssetDatabase.CreateAsset(config, ConfigPath);
        }
        SyncAllAlbums();
        var albumCard = CreateAlbumCard();
        var levelCard = CreateLevelCard();
        var homePicture = CreateHomePicture(config);
        var library = BuildPage<PictureCollectionPopup>("PictureCollectionPopup");
        Set(library, "collection", config); Set(library, "cardPrefab", albumCard);
        var libraryScroll = Scroll(library.transform, 3, 1.48f);
        Set(library, "scroll", libraryScroll);
        var libraryPrefab = SavePage(library);
        var albumPage = BuildPage<PictureAlbumPopup>("PictureAlbumPopup");
        Set(albumPage, "cardPrefab", levelCard); Set(albumPage, "scroll", Scroll(albumPage.transform,2,.76f));
        var albumPrefab = SavePage(albumPage);
        var preview = BuildPage<PicturePreviewPopup>("PicturePreviewPopup");
        var a = Image("PictureA",preview.transform,Color.white,new Vector2(.04f,.51f),new Vector2(.96f,.86f));
        var b = Image("PictureB",preview.transform,Color.white,new Vector2(.04f,.15f),new Vector2(.96f,.5f));
        a.preserveAspect = b.preserveAspect = true;
        Set(preview,"pictureA",a); Set(preview,"pictureB",b);
        Set(preview,"status",Text("Status",preview.transform,"",30,new Vector2(.06f,.11f),new Vector2(.94f,.15f)));
        var replay = Button("ReplayButton",preview.transform,"Replay",new Vector2(.28f,.035f),new Vector2(.72f,.1f),new Color(.3f,.72f,.16f));
        Set(preview,"replayButton",replay);
        var previewPrefab = SavePage(preview);
        UpdateHome(homePicture);
        var popupConfig = AssetDatabase.LoadAssetAtPath<PopupConfig>("Assets/_Project/Config/PopupConfig.asset");
        foreach (var popup in new Popup[] {libraryPrefab,albumPrefab,previewPrefab})
            if (!popupConfig.popups.Any(p => p != null && p.GetType() == popup.GetType())) popupConfig.popups.Add(popup);
        EditorUtility.SetDirty(popupConfig);
        BindLevelController(config);
        AssetDatabase.SaveAssets();
        Debug.Log("[PictureCollection] Authored prefabs, catalog and GameplayScene references are ready.");
    }

    [MenuItem("Tools/Find Differences/Sync Album Pictures From Levels")]
    public static void SyncAllAlbums()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:PictureAlbumData"))
            SyncAlbum(AssetDatabase.LoadAssetAtPath<PictureAlbumData>(AssetDatabase.GUIDToAssetPath(guid)));
        AssetDatabase.SaveAssets();
    }
    public static void SyncAlbum(PictureAlbumData album)
    {
        if (album.levels == null) return;
        foreach (var entry in album.levels)
        {
            if (entry == null || entry.levelPrefab == null || !entry.levelPrefab.RuntimeKeyIsValid()) continue;
            string path = AssetDatabase.GUIDToAssetPath(entry.levelPrefab.AssetGUID);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var controller = prefab != null ? prefab.GetComponent<DifferenceLevelController>() : null;
            if (controller == null) { Debug.LogError($"[PictureCollection] {album.name}: {path} is not a difference level.", album); continue; }
            if (string.IsNullOrWhiteSpace(entry.levelId)) entry.levelId = entry.levelPrefab.AssetGUID;
            entry.pictureA = DifferenceLevelEditing.Panel(controller,true)?.Picture?.sprite;
            entry.pictureB = DifferenceLevelEditing.Panel(controller,false)?.Picture?.sprite;
        }
        EditorUtility.SetDirty(album);
    }

    private static PictureAlbumCard CreateAlbumCard()
    {
        var root = Rect("PictureAlbumCard",null,Vector2.zero,Vector2.one);
        root.gameObject.SetActive(false);
        var bg = root.gameObject.AddComponent<Image>(); bg.sprite=panel; bg.type=UnityEngine.UI.Image.Type.Sliced; bg.color=Paper;
        var button = root.gameObject.AddComponent<Button>(); button.targetGraphic=bg;
        Image("PictureBackground",root,DarkBlue,new Vector2(.04f,.18f),new Vector2(.96f,.96f));
        var cover = Image("Cover",root,Color.white,new Vector2(.04f,.18f),new Vector2(.96f,.96f)); cover.preserveAspect=true;
        var locked = Text("Locked",root,"?",100,new Vector2(.1f,.3f),new Vector2(.9f,.8f));
        var name = Text("Title",root,"Album",27,new Vector2(.03f,.01f),new Vector2(.97f,.18f)); name.color=DarkBlue;
        var progress = Text("Progress",root,"0/0",25,new Vector2(.03f,.84f),new Vector2(.42f,.97f));
        Image("ProgressBackground",root,new Color(0,0,0,.5f),new Vector2(.03f,.84f),new Vector2(.42f,.97f)).transform.SetSiblingIndex(3);
        progress.transform.SetAsLastSibling();
        var view=root.gameObject.AddComponent<PictureAlbumCard>();
        Set(view,"button",button); Set(view,"cover",cover); Set(view,"title",name); Set(view,"progress",progress); Set(view,"locked",locked.gameObject);
        return SaveElement(view);
    }
    private static PictureLevelCard CreateLevelCard()
    {
        var root=Rect("PictureLevelCard",null,Vector2.zero,Vector2.one); root.gameObject.SetActive(false);
        var bg=root.gameObject.AddComponent<Image>(); bg.sprite=panel; bg.type=UnityEngine.UI.Image.Type.Sliced; bg.color=DarkBlue;
        var button=root.gameObject.AddComponent<Button>(); button.targetGraphic=bg;
        var picture=Image("Picture",root,Color.white,new Vector2(.025f,.15f),new Vector2(.975f,.975f)); picture.preserveAspect=true;
        var caption=Text("Caption",root,"Level",25,new Vector2(.03f,0),new Vector2(.97f,.15f));
        var locked=Text("Locked",root,"",36,new Vector2(.15f,.3f),new Vector2(.85f,.7f));
        var view=root.gameObject.AddComponent<PictureLevelCard>();
        Set(view,"button",button); Set(view,"picture",picture); Set(view,"caption",caption); Set(view,"locked",locked.gameObject);
        return SaveElement(view);
    }
    private static HomeAlbumPictureView CreateHomePicture(PictureCollectionConfig config)
    {
        var root=Rect("HomeAlbumPicture",null,Vector2.zero,Vector2.one); root.gameObject.SetActive(false);
        var bg=root.gameObject.AddComponent<Image>(); bg.sprite=panel; bg.type=UnityEngine.UI.Image.Type.Sliced; bg.color=Paper;
        var frame=Image("PictureFrame",root,DarkBlue,new Vector2(.025f,.14f),new Vector2(.975f,.975f));
        var fit=Rect("FittedPicture",frame.transform,Vector2.zero,Vector2.one);
        var fitter=fit.gameObject.AddComponent<AspectRatioFitter>(); fitter.aspectMode=AspectRatioFitter.AspectMode.FitInParent;
        var sample=config.albums.FirstOrDefault();
        fitter.aspectRatio=sample?.coverSprite != null ? sample.coverSprite.rect.width/sample.coverSprite.rect.height : .8f;
        var cover=Image("Cover",fit,Color.white,Vector2.zero,Vector2.one); cover.sprite=sample?.coverSprite;
        var puzzleRect=Rect("Puzzle",fit,Vector2.zero,Vector2.one);
        var puzzle=puzzleRect.gameObject.AddComponent<AlbumPuzzleGraphic>(); puzzle.raycastTarget=false;
        var title=Text("Title",root,"Album",45,new Vector2(.03f,.06f),new Vector2(.97f,.14f)); title.color=DarkBlue;
        var progress=Text("Progress",root,"0/0",27,new Vector2(.03f,0),new Vector2(.97f,.065f)); progress.color=DarkBlue;
        var view=root.gameObject.AddComponent<HomeAlbumPictureView>();
        Set(view,"collection",config); Set(view,"cover",cover); Set(view,"puzzle",puzzle); Set(view,"title",title); Set(view,"progress",progress);
        return SaveElement(view);
    }
    private static T BuildPage<T>(string name) where T:Popup
    {
        string path=UI+"/Popup/"+name+".prefab";
        GameObject go;
        if (File.Exists(path))
        {
            go=PrefabUtility.LoadPrefabContents(path);
            loadedPages.Add(go);
            go.SetActive(false);
            for(int i=go.transform.childCount-1;i>=0;i--) Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        }
        else
        {
            go=Rect(name,null,Vector2.zero,Vector2.one).gameObject; go.SetActive(false);
            go.AddComponent<Canvas>(); go.AddComponent<CanvasGroup>(); go.AddComponent<GraphicRaycaster>();
            go.AddComponent<T>();
        }
        var page=go.GetComponent<T>();
        go.layer=LayerMask.NameToLayer("UI");
        var canvas=page.Canvas; canvas.overrideSorting=true; canvas.sortingOrder=499;
        var root=(RectTransform)go.transform; root.anchorMin=Vector2.zero; root.anchorMax=Vector2.one; root.sizeDelta=Vector2.zero;
        var bg=Image("Background",root,Blue,Vector2.zero,Vector2.one); bg.raycastTarget=true;
        Set(page,"background",bg.rectTransform); Set(page,"container",root);
        var back=Button("BackButton",root,"‹",new Vector2(.02f,.905f),new Vector2(.14f,.97f),DarkBlue);
        Set(page,"backButton",back);
        var title=Text("Title",root,name=="PictureCollectionPopup"?"Picture Collection":"Album",43,new Vector2(.17f,.905f),new Vector2(.91f,.97f));
        if (new SerializedObject(page).FindProperty("title") != null) Set(page,"title",title);
        return page;
    }
    private static ScrollRect Scroll(Transform parent,int columns,float ratio)
    {
        var root=Rect("Scroll",parent,new Vector2(.035f,.035f),new Vector2(.965f,.885f));
        var scroll=root.gameObject.AddComponent<ScrollRect>(); scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped;
        var viewport=Rect("Viewport",root,Vector2.zero,Vector2.one);
        var bg=viewport.gameObject.AddComponent<Image>(); bg.color=new Color(0,0,0,.01f); bg.raycastTarget=true;
        viewport.gameObject.AddComponent<RectMask2D>();
        var content=Rect("Content",viewport,new Vector2(0,1),Vector2.one); content.pivot=new Vector2(.5f,1);
        var grid=content.gameObject.AddComponent<GridLayoutGroup>(); grid.spacing=new Vector2(22,22); grid.padding=new RectOffset(5,5,5,5);
        grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount=columns;
        grid.cellSize=new Vector2((990f-22*(columns-1))/columns,((990f-22*(columns-1))/columns)*ratio);
        var size=content.gameObject.AddComponent<ContentSizeFitter>(); size.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var responsive=content.gameObject.AddComponent<ResponsivePictureGrid>(); Set(responsive,"viewport",viewport);
        var so=new SerializedObject(responsive); so.FindProperty("columns").intValue=columns; so.FindProperty("heightOverWidth").floatValue=ratio; so.ApplyModifiedPropertiesWithoutUndo();
        scroll.viewport=viewport; scroll.content=content;
        return scroll;
    }
    private static void UpdateHome(HomeAlbumPictureView picture)
    {
        var go=PrefabUtility.LoadPrefabContents(HomePath);
        try
        {
            var container=go.transform.Find("Container");
            var old=container.Find("HomeAlbumPicture"); if(old!=null) Object.DestroyImmediate(old.gameObject);
            foreach(string name in new[]{"Progress_","frame","Character","BtnPlay_Hard","Cauldron_decor","Image"})
            { var child=container.Find(name); if(child!=null) child.gameObject.SetActive(false); }
            var view=(HomeAlbumPictureView)PrefabUtility.InstantiatePrefab(picture,container);
            view.gameObject.SetActive(true);
            var rect=(RectTransform)view.transform; rect.anchorMin=new Vector2(.12f,.29f); rect.anchorMax=new Vector2(.88f,.79f); rect.offsetMin=rect.offsetMax=Vector2.zero;
            Set(go.GetComponent<PopupHome>(),"albumPicture",view);
            var play=container.Find("BtnPlay") as RectTransform;
            if(play!=null) { play.anchoredPosition=new Vector2(0,-610); play.localScale=Vector3.one; }
            var task=container.Find("Btn_Task") as RectTransform;
            if(task!=null) { task.anchoredPosition=new Vector2(405,-610); task.SetAsLastSibling(); }
            PrefabUtility.SaveAsPrefabAsset(go,HomePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(go); }
    }
    private static void BindLevelController(PictureCollectionConfig config)
    {
        // Prefer the controller prefab; GameplayScene instance can also contain scene-only placement refs.
        string[] paths=AssetDatabase.FindAssets("LevelController t:Prefab").Select(AssetDatabase.GUIDToAssetPath).ToArray();
        foreach(string path in paths)
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try { var controller=root.GetComponentInChildren<LevelController>(true); if(controller!=null) { Set(controller,"pictureCollection",config); PrefabUtility.SaveAsPrefabAsset(root,path); } }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        const string scenePath="Assets/_Project/Scenes/GameplayScene.unity";
        var scene=SceneManager.GetSceneByPath(scenePath);
        bool opened=!scene.isLoaded;
        if(opened) scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
        try
        {
            foreach(var root in scene.GetRootGameObjects())
                foreach(var controller in root.GetComponentsInChildren<LevelController>(true)) Set(controller,"pictureCollection",config);
            EditorSceneManager.SaveScene(scene);
        }
        finally { if(opened) EditorSceneManager.CloseScene(scene,true); }
    }
    internal static void Set(Object target,string name,Object value)
    {
        var so=new SerializedObject(target); var property=so.FindProperty(name);
        if(property==null) throw new InvalidOperationException(target.GetType().Name+" missing field "+name);
        property.objectReferenceValue=value; so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {
        var go=new GameObject(name,typeof(RectTransform)); var rect=(RectTransform)go.transform;
        go.layer=LayerMask.NameToLayer("UI");
        if(parent!=null) rect.SetParent(parent,false);
        rect.anchorMin=min; rect.anchorMax=max; rect.offsetMin=rect.offsetMax=Vector2.zero;
        return rect;
    }
    private static Image Image(string name,Transform parent,Color color,Vector2 min,Vector2 max)
    {
        var image=Rect(name,parent,min,max).gameObject.AddComponent<Image>(); image.color=color; image.raycastTarget=false; return image;
    }
    private static TextMeshProUGUI Text(string name,Transform parent,string value,int size,Vector2 min,Vector2 max)
    {
        var text=Rect(name,parent,min,max).gameObject.AddComponent<TextMeshProUGUI>();
        text.font=font; text.text=value; text.fontSize=size; text.enableAutoSizing=true; text.fontSizeMin=size*.6f; text.fontSizeMax=size;
        text.alignment=TextAlignmentOptions.Center; text.color=Color.white; text.raycastTarget=false; return text;
    }
    private static Button Button(string name,Transform parent,string label,Vector2 min,Vector2 max,Color color)
    {
        var bg=Image(name,parent,color,min,max); bg.sprite=panel; bg.type=UnityEngine.UI.Image.Type.Sliced; bg.raycastTarget=true;
        var button=bg.gameObject.AddComponent<Button>(); button.targetGraphic=bg;
        Text("Label",bg.transform,label,45,Vector2.zero,Vector2.one); return button;
    }
    private static T SaveElement<T>(T view) where T:Component
    {
        view.gameObject.SetActive(true);
        var prefab=PrefabUtility.SaveAsPrefabAsset(view.gameObject,UI+"/Elements/"+view.gameObject.name+".prefab");
        Object.DestroyImmediate(view.gameObject);
        return prefab.GetComponent<T>();
    }
    private static T SavePage<T>(T page) where T:Popup
    {
        string path=UI+"/Popup/"+page.name+".prefab";
        // Inactive prefab roots prevent page callbacks until all serialized references are ready at runtime.
        var prefab=PrefabUtility.SaveAsPrefabAsset(page.gameObject,path);
        if(loadedPages.Remove(page.gameObject)) PrefabUtility.UnloadPrefabContents(page.gameObject);
        else Object.DestroyImmediate(page.gameObject);
        return prefab.GetComponent<T>();
    }
}
