using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WesternLemegeton;

public static class ProjectSetup
{
    [MenuItem("Western Lemegeton/Configure Build Settings")]
    public static void CreateScene()
    {
        if(!File.Exists("Assets/Scenes/Main.unity"))throw new Exception("Editable Main scene is missing.");
        if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())throw new Exception("Build settings cancelled; unsaved scene preserved.");
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/Scenes/Main.unity")EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        if(!UnityEngine.Object.FindFirstObjectByType<GameSceneBindings>())throw new Exception("Run EditableProjectSetup.CreateEditableProject once to migrate the legacy scene.");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Main.unity",true)};
        PlayerSettings.companyName="Team ARS Prototype";PlayerSettings.productName="Western Lemegeton";
        PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=false;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        ConfigureSplash();
        AssetDatabase.SaveAssets();
    }
    static void ConfigureSplash()
    {
        const string path="Assets/Branding/WnnLogo.png";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
        importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var logo=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if(!logo)throw new Exception("Game splash logo is missing");
        PlayerSettings.SplashScreen.show=true;
        PlayerSettings.SplashScreen.showUnityLogo=false;
        PlayerSettings.SplashScreen.backgroundColor=Color.black;
        PlayerSettings.SplashScreen.logos=new[]{PlayerSettings.SplashScreenLogo.Create(2.5f,logo)};
        Require(!PlayerSettings.SplashScreen.showUnityLogo&&PlayerSettings.SplashScreen.logos.Length==1&&PlayerSettings.SplashScreen.logos[0].logo==logo,"Startup splash uses supplied WNN logo only");
    }
    [MenuItem("Western Lemegeton/Build Windows Player")]
    public static void ValidateAndBuild()
    {
        CreateScene();WesternLemegeton.EditorTools.EditableValidation.ValidateImportedProject();ValidateRules();ValidateWesternArt();ValidateCombat();ValidateExpedition();ValidateRoomAccess();TimelineSetup.Validate();MotionSetup.Validate();
        Directory.CreateDirectory("Builds/Windows");
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Main.unity"},locationPathName="Builds/Windows/Western Lemegeton.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Build failed: "+result.summary.result);
        Debug.Log("WNN_BUILD_SUCCESS");
    }
    public static void ValidateRules()
    {
        Require(Rules.AmmoCost(0)+Rules.AmmoCost(1)+Rules.AmmoCost(2)+Rules.AmmoCost(3)==6,"Four-hit revolver combo consumes six rounds");
        Require(Rules.ClampToRoom(new Vector2(100,-100))==new Vector2(14.6f,-8.6f),"Dodge is contained by room walls");
        Require(Rules.CameraTarget(new Vector2(100,100),7,16f/9).x<=15.8f-7*16f/9+.001f,"Camera right bound respects viewport");
        Require(Rules.CameraTarget(Vector2.one,20,2)==Vector2.zero,"Large viewport is centered without inverted clamp");
        Require(Rules.InCone(Vector2.zero,Vector2.right,new Vector2(1,0),2,0),"Melee hits forward enemy");
        Require(!Rules.InCone(Vector2.zero,Vector2.right,new Vector2(-1,0),2,0),"Melee excludes rear enemy");
        Require(!Rules.InCone(Vector2.zero,Vector2.right,new Vector2(3,0),2,0),"Melee excludes out-of-range enemy");
        var saved=Dungeon.Obstacles;var savedSolid=Dungeon.SolidObstacles;
        Dungeon.Obstacles=new[]{new Rect(-7,-3,1.7f,2)};
        Dungeon.SolidObstacles=Dungeon.Obstacles;
        var pos=Rules.ResolveObstacles(new Vector2(-7.5f,-2),new Vector2(-7,-2),.38f);
        Require(pos.x<-7.38f,"Walking cannot enter pillar");
        var recovered=Rules.OutsideCover(new Vector2(-6,-2),.38f);
        Require(!Rect.MinMaxRect(-7.38f,-3.38f,-4.92f,-.62f).Contains(recovered),"Dodge ending inside cover is recovered");
        Require(Rules.ResolveDash(new Vector2(-7.5f,-2),new Vector2(-7,-2),.38f).x<-7.38f,"Dodge cannot enter building footprint");
        Require(Rules.ResolveDash(new Vector2(-8,-2),new Vector2(-3,-2),.38f).x<-7.38f,"Dodge cannot tunnel through buildings on a long frame");
        Dungeon.Obstacles=saved;Dungeon.SolidObstacles=savedSolid;
        Debug.Log("WNN_RULE_CHECKS_PASSED: 11");
    }
    static void ValidateWesternArt()
    {
        var textures=Resources.LoadAll<Texture2D>("Western");Require(textures.Length==41,"All 41 supplied PNG assets are included");
        foreach(var texture in textures)Require(WesternArt.Get(texture.name).rect.width>0,"Sprite rectangle: "+texture.name);
        for(int i=-1;i<6;i++)
        {
            var map=WesternEnvironment.Build(i<0,0,Mathf.Max(0,i));
            Require(map.Root.GetComponentsInChildren<SpriteRenderer>().Length>20,"Environment populated: "+i);
            Require(map.Obstacles.Count>0,"Environment collision footprints: "+i);
            foreach(var rect in map.Obstacles)Require(!rect.Contains(new Vector2(13,0)),"Exit approach is clear: "+i);
            UnityEngine.Object.DestroyImmediate(map.Root.gameObject);
        }
        Debug.Log("WNN_WESTERN_ART_CHECKS_PASSED");
    }
    static void ValidateCombat()
    {
        foreach(string name in new[]{"Hunter","Raven","RavenPortrait","RavenLink","SlashIcon","SlamIcon","ShotIcon","BarrageIcon","Enemy"})Require(CombatArt.Get(name).rect.width>0,"Combat source art: "+name);
        var chain=new ComboTracker();
        Require(!chain.Hit(1,1,1,0)&&!chain.Hit(1,2,2,1)&&!chain.Hit(1,2,2,1)&&!chain.Hit(1,3,3,2)&&chain.Hit(1,4,4,3),"Combo ignores duplicate pellets and completes four stages");
        chain.Clear();chain.Hit(1,1,10,0);chain.Hit(2,2,11,1);chain.Hit(1,3,12,2);Require(!chain.Hit(1,4,13,3),"Combo cannot combine different targets");
        chain.Clear();chain.Hit(1,1,20,0);chain.Hit(1,2,21,1);chain.Hit(1,3,22,2);Require(!chain.Hit(1,4,23,5),"Combo expires after four seconds");
    }
    static void ValidateExpedition()
    {
        var p=new ExpeditionProgress();p.Reset(-1957);int rooms=0,waves=0,services=0;
        for(int segment=0;segment<5;segment++)
        {
            Require(!p.TryVisit(1)&&!p.NextSegment(),"Combat locks room doors and stage exit");
            int[] path={0,1,2,3,2,4,5};
            foreach(int room in path)
            {
                if(p.Map!=room)Require(p.TryVisit(room),"Adjacent rooms are traversable both ways");
                if(p.MapComplete)continue;
                Require(!p.TryVisit(99),"Invalid room rejected");
                if(RoomGraph.Kind(room)==ExplorationRoomKind.Combat)
                {
                    int total=p.WaveCount;Require(total==2||total==3,"Combat room has two or three waves");
                    for(int w=0;w<total;w++){bool done=p.CompleteWave();waves++;Require(done==(w==total-1),"Only final wave clears room");}
                    int count=p.ClearedMaps;Require(!p.CompleteWave()&&p.ClearedMaps==count,"Cleared room does not repeat rewards");
                }
                else{Require(p.WaveCount==0&&p.CompleteService()&&!p.CompleteService(),"Service room resolves once without waves");services++;}
                rooms++;
                if(p.ClearedMaps<6)Require(!p.NextSegment(),"All six rooms required for next stage");
            }
            Require(p.ClearedMaps==6&&p.VisitedMaps==6,"All rooms explored and resolved");
            Require(p.TryVisit(4)&&p.TryVisit(2)&&p.TryVisit(3)&&p.MapComplete&&p.ClearedMaps==6,"Backtracking preserves cleared rooms");
            if(segment<4)Require(p.NextSegment()&&p.Map==0&&p.VisitedMaps==1&&p.ClearedMaps==0,"Next stage resets local exploration");
        }
        Require(rooms==30&&services==10&&waves==50&&!p.NextSegment(),"Five stages contain thirty rooms and fifty combat waves");
        for(int a=0;a<6;a++)for(int b=a+1;b<6;b++)Require(Vector2.Distance(RoomGraph.Centers[a],RoomGraph.Centers[b])>30,"Room footprints are spatially separate");
    }
    static void ValidateRoomAccess()
    {
        var root=new GameObject("Room access validation");
        try
        {
            var rooms=ExplorationStage.Create(root.transform,0);
            foreach(var room in rooms)
            {
                const int width=57,height=33;bool[,] blocked=new bool[width,height],seen=new bool[width,height];
                Vector2 start=room.Index==0?new Vector2(-10,0):RoomGraph.EntryLocal(room.Doors[0].Destination,room.Index);
                var queue=new System.Collections.Generic.Queue<Vector2Int>();
                for(int x=0;x<width;x++)for(int y=0;y<height;y++)
                {
                    var p=new Vector2(-14+x*.5f,-8+y*.5f);
                    foreach(var r in room.Layout.Obstacles)if(Rect.MinMaxRect(r.xMin-.4f,r.yMin-.4f,r.xMax+.4f,r.yMax+.4f).Contains(p)){blocked[x,y]=true;break;}
                    if(!blocked[x,y]&&Vector2.Distance(p,start)<.8f){seen[x,y]=true;queue.Enqueue(new Vector2Int(x,y));}
                }
                var dirs=new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};
                while(queue.Count>0){var at=queue.Dequeue();foreach(var d in dirs){var n=at+d;if(n.x<0||n.y<0||n.x>=width||n.y>=height||blocked[n.x,n.y]||seen[n.x,n.y])continue;seen[n.x,n.y]=true;queue.Enqueue(n);}}
                var targets=new System.Collections.Generic.List<Vector2>();foreach(var door in room.Doors)targets.Add(door.transform.localPosition);
                if(RoomGraph.Kind(room.Index)!=ExplorationRoomKind.Combat)targets.Add(Vector2.zero);
                foreach(var target in targets)
                {
                    bool reachable=false;for(int x=0;x<width;x++)for(int y=0;y<height;y++)if(seen[x,y]&&Vector2.Distance(new Vector2(-14+x*.5f,-8+y*.5f),target)<1.5f)reachable=true;
                    Require(reachable,"Room "+(room.Index+1)+" entrance can reach interaction "+target);
                }
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    static void Require(bool result,string name){if(!result)throw new Exception("CHECK FAILED: "+name);Debug.Log("PASS: "+name);}
}



