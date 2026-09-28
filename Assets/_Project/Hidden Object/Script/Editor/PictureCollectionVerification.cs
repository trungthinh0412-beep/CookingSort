using System;
using System.Collections.Generic;
using System.IO;
using MagicSoft.Differences;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class PictureCollectionVerification
{
    [MenuItem("Tools/Find Differences/Verify Picture Collection")]
    public static void Verify()
    {
        var config=AssetDatabase.LoadAssetAtPath<PictureCollectionConfig>(PictureCollectionPrefabSetup.ConfigPath);
        Check(config!=null && config.albums.Count>0,"Catalog exists");
        var ids=new HashSet<string>(); var numbers=new HashSet<int>(); var albumIds=new HashSet<string>();
        foreach(var album in config.albums)
        {
            Check(album!=null && !string.IsNullOrWhiteSpace(album.albumId) && albumIds.Add(album.albumId),"Unique album ID");
            Check(album.Total>0 && album.coverSprite!=null,"Album has a cover and levels");
            foreach(var entry in album.levels)
            {
                Check(entry!=null && !string.IsNullOrWhiteSpace(entry.levelId) && ids.Add(entry.levelId),"Unique level ID");
                Check(entry.levelNumber>0 && numbers.Add(entry.levelNumber),"Unique main level number");
                Check(entry.IsPlayable && entry.pictureA!=null && entry.pictureB!=null,"Prefab and cached A/B are assigned");
                string path=AssetDatabase.GUIDToAssetPath(entry.levelPrefab.AssetGUID);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Check(path.StartsWith(DifferencePrefabSetup.LevelDirectory+"/",StringComparison.Ordinal),"World prefab is in requested Level folder");
                var controller=prefab.GetComponent<DifferenceLevelController>();
                Check(controller!=null && controller.TryValidate(out _),"Valid world difference level");
                Check(entry.pictureA==DifferenceLevelEditing.Panel(controller,true).Picture.sprite &&
                    entry.pictureB==DifferenceLevelEditing.Panel(controller,false).Picture.sprite,"Metadata matches world pictures");
            }
        }
        for(int i=1;i<=numbers.Count;i++) Check(numbers.Contains(i),"Main level numbers are consecutive");
        var popupConfig=AssetDatabase.LoadAssetAtPath<PopupConfig>("Assets/_Project/Config/PopupConfig.asset");
        foreach(var type in new[]{typeof(PictureCollectionPopup),typeof(PictureAlbumPopup),typeof(PicturePreviewPopup)})
        {
            int matches=0;
            foreach(var popup in popupConfig.popups)
            {
                if(popup==null || popup.GetType()!=type) continue;
                matches++;
                foreach(var transform in popup.GetComponentsInChildren<Transform>(true))
                    Check(transform.gameObject.layer==LayerMask.NameToLayer("UI"),"UI layer for "+transform.name);
                Check(popup.Canvas!=null && popup.CanvasGroup!=null && popup.GetComponent<GraphicRaycaster>()!=null,"Popup canvas attachments");
                var so=new SerializedObject(popup);
                foreach(string field in type==typeof(PictureCollectionPopup)?new[]{"collection","backButton","scroll","cardPrefab"}:
                    type==typeof(PictureAlbumPopup)?new[]{"title","backButton","scroll","cardPrefab"}:
                    new[]{"title","status","pictureA","pictureB","backButton","replayButton"})
                    Check(so.FindProperty(field).objectReferenceValue!=null,"Explicit "+type.Name+"."+field);
            }
            Check(matches==1,"One registered instance of "+type.Name);
        }
        Check(AssetDatabase.LoadAssetAtPath<GameObject>(PictureCollectionPrefabSetup.HomePath).GetComponentInChildren<HomeAlbumPictureView>(true)!=null,"Home has authored picture prefab");
        VerifyProgress(config);
        RenderPages(config);
        Debug.Log("[PictureCollection] PASS: catalog, authored references, unlocks, migration, save roundtrip, first clear and free replay; rendered UI at three ratios.");
    }
    private static void VerifyProgress(PictureCollectionConfig config)
    {
        var entry=config.albums[0].levels[0];
        var player=new PlayerData();
        Check(config.IsUnlocked(config.albums[0],player),"First album unlocked");
        var session=new PicturePlaySession(config.albums[0],entry,1,false);
        Check(session.Complete(player) && player.CurrentLevelIndex==2 && player.HasCompletedPicture(entry.levelId),"First win records completion and advances");
        int ads=player.CountShowInterAds;
        Check(!session.Complete(player) && player.CurrentLevelIndex==2 && player.CountShowInterAds==ads,"Duplicate callback is idempotent");
        var second=new PicturePlaySession(config.albums[0],entry,2,false);
        Check(!second.Complete(player) && !second.CanClaimReward && player.CurrentLevelIndex==3,"Looped content cannot award a second piece");
        int level=player.CurrentLevelIndex, heart=player.CurrentHeart, gold=player.CurrentGold, stars=player.CurrentStar;
        ads=player.CountShowInterAds;
        var replay=new PicturePlaySession(config.albums[0],entry,1,true);
        Check(!replay.Complete(player) && !replay.CanClaimReward && player.CurrentLevelIndex==level && player.CurrentHeart==heart &&
            player.CurrentGold==gold && player.CurrentStar==stars && player.CountShowInterAds==ads,"Replay leaves progression, hearts, currencies and ads unchanged");
        var copy=JsonConvert.DeserializeObject<PlayerData>(JsonConvert.SerializeObject(player));
        Check(copy.HasCompletedPicture(entry.levelId) && copy.CompletedPictureLevelIds.Count==1,"JSON roundtrip keeps stable completion IDs");
        var legacy=new PlayerData {CurrentLevelIndex=2};
        Check(config.MigrateProgress(legacy) && legacy.HasCompletedPicture(entry.levelId) && !config.MigrateProgress(legacy),"Legacy migration runs once");
        var next=ScriptableObject.CreateInstance<PictureAlbumData>();
        var fixture=ScriptableObject.CreateInstance<PictureCollectionConfig>();
        fixture.albums.Add(config.albums[0]); fixture.albums.Add(next);
        Check(!fixture.IsUnlocked(next,new PlayerData()),"Next album locked before completion");
        var complete=new PlayerData(); foreach(var item in config.albums[0].levels) complete.MarkPictureCompleted(item.levelId);
        Check(fixture.IsUnlocked(next,complete),"Next album opens after all configured entries are completed");
        Object.DestroyImmediate(fixture); Object.DestroyImmediate(next);
    }
    private static void RenderPages(PictureCollectionConfig config)
    {
        var previous=SceneManager.GetActiveScene(); var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        var player=Data.PlayerData; Data.PlayerData=new PlayerData();
        foreach(var entry in config.albums[0].levels) Data.PlayerData.MarkPictureCompleted(entry.levelId);
        SceneManager.SetActiveScene(scene);
        try
        {
            var camera=new GameObject("UICamera",typeof(Camera)).GetComponent<Camera>();
            camera.cullingMask=1<<LayerMask.NameToLayer("UI");
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
            var root=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            var canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=2;
            var scaler=root.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1080,1920); scaler.matchWidthOrHeight=.5f;
            foreach(string page in new[]{"PopupHome","PictureCollectionPopup","PictureAlbumPopup","PicturePreviewPopup"})
            {
                string path=page=="PopupHome"?PictureCollectionPrefabSetup.HomePath:PictureCollectionPrefabSetup.UI+"/Popup/"+page+".prefab";
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),root.transform);
                var popup=go.GetComponent<Popup>();
                if(popup is PictureAlbumPopup album) album.SelectAlbum(config.albums[0]);
                if(popup is PicturePreviewPopup preview) preview.SelectPicture(config.albums[0],config.albums[0].levels[0]);
                if(page=="PopupHome") { go.SetActive(true); go.GetComponentInChildren<HomeAlbumPictureView>(true).Refresh(); }
                else popup.Show(PopupAnimation.None);
                foreach(var size in new[]{new Vector2Int(540,960),new Vector2Int(540,1200),new Vector2Int(768,1024)})
                {
                    var target=new RenderTexture(size.x,size.y,24);
                    camera.targetTexture=target; camera.pixelRect=new Rect(0,0,size.x,size.y);
                    scaler.SendMessage("Update"); Canvas.ForceUpdateCanvases();
                    foreach(var grid in go.GetComponentsInChildren<ResponsivePictureGrid>()) grid.SendMessage("OnEnable");
                    foreach(var grid in go.GetComponentsInChildren<ResponsivePictureGrid>()) grid.SendMessage("LateUpdate");
                    Canvas.ForceUpdateCanvases(); camera.Render();
                    var old=RenderTexture.active; RenderTexture.active=target;
                    var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,size.x,size.y),0,0); image.Apply();
                    File.WriteAllBytes($"Library/PictureUI-{page}-{size.x}x{size.y}.png",image.EncodeToPNG());
                    Object.DestroyImmediate(image); RenderTexture.active=old; camera.targetTexture=null; Object.DestroyImmediate(target);
                }
                if(popup is PictureAlbumPopup albumPage) VerifyScrollAndPuzzle(albumPage,root.transform,config.albums[0].levels[0]);
                Object.DestroyImmediate(go);
            }
        }
        finally { Data.PlayerData=player; SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene,true); }
    }
    private static void VerifyScrollAndPuzzle(PictureAlbumPopup page,Transform parent,PictureLevelEntry entry)
    {
        var fixture=ScriptableObject.CreateInstance<PictureAlbumData>(); fixture.title="Scroll fixture";
        for(int i=0;i<25;i++) fixture.levels.Add(entry);
        page.SelectAlbum(fixture);
        foreach(var grid in page.GetComponentsInChildren<ResponsivePictureGrid>()) { grid.SendMessage("OnEnable"); grid.SendMessage("LateUpdate"); }
        Canvas.ForceUpdateCanvases();
        var scroll=page.GetComponentInChildren<ScrollRect>();
        Check(scroll.content.rect.height>scroll.viewport.rect.height,"25 entries produce real vertical scrolling");
        scroll.verticalNormalizedPosition=.37f;
        page.Hide(PopupAnimation.None); page.Show(PopupAnimation.None); page.SelectAlbum(fixture);
        Canvas.ForceUpdateCanvases();
        Check(Mathf.Abs(scroll.verticalNormalizedPosition-.37f)<.01f,"Reopening same album preserves scroll position");
        Check(page.GetComponentsInChildren<PictureLevelCard>().Length==25,"Album reuses its card pool");
        Object.DestroyImmediate(fixture);
        var go=new GameObject("Puzzle mesh test",typeof(RectTransform)); go.layer=LayerMask.NameToLayer("UI"); go.transform.SetParent(parent,false);
        var rect=(RectTransform)go.transform; rect.sizeDelta=new Vector2(600,600);
        var puzzle=go.AddComponent<AlbumPuzzleGraphic>();
        foreach(int total in new[]{1,6,25})
        {
            puzzle.SetPieces(new bool[total]); Canvas.ForceUpdateCanvases(); var mesh=puzzle.canvasRenderer.GetMesh();
            float area=0; var vertices=mesh.vertices; var colors=mesh.colors32; var triangles=mesh.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                if(colors[a].r<140) continue; // Exclude seam triangles.
                area+=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).magnitude*.5f;
            }
            Check(Mathf.Abs(area-360000)<400,"Missing puzzle pieces cover the image without gaps, count="+total+" area="+area);
        }
        Object.DestroyImmediate(go);
    }
    public static void Check(bool value,string message)
    { if(!value) throw new InvalidOperationException("[PictureCollection verification] "+message); }
}
