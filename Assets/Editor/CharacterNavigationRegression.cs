using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.UI;
using Game.Characters;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CharacterNavigationRegression
{
    static int phase;
    static double deadline, ready;
    static readonly List<string> checks = new();
    static CinemachineInputAxisController axis;
    static PlayerInputHandler player;
    static CharacterEntryButton entry;
    static bool oldBackground;
    static UIHandle window;
    static CharacterNavigationRegression()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state =>
        {
            if(state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("CharacterNavigationRegression",false)) return;
            phase=0;checks.Clear();deadline=EditorApplication.timeSinceStartup+180;ready=EditorApplication.timeSinceStartup+2;
            oldBackground=Application.runInBackground;Application.runInBackground=true;
        };
    }
    [MenuItem("Tools/Characters/Verify Navigation and HUD")]
    public static void Begin()
    {
        if(EditorApplication.isPlaying) throw new Exception("Edit Mode required");
        Directory.CreateDirectory("Temp/CharacterNavigationRegression");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.isDirty) UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,"Temp/CharacterNavigationRegression/BeforeTest.unity",true);
        checks.Clear();phase=0;deadline=EditorApplication.timeSinceStartup+120;
        SessionState.SetBool("CharacterNavigationRegression",true);
        EditorApplication.isPlaying=true;
    }
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add("PASS "+text);}
    static void Next(int value){phase=value;ready=EditorApplication.timeSinceStartup+1.2;}
    static void Finish(string error=null)
    {
        File.WriteAllText("Temp/CharacterNavigationRegression/results.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=error==null,error,checks},Newtonsoft.Json.Formatting.Indented));
        SessionState.SetBool("CharacterNavigationRegression",false);Application.runInBackground=oldBackground;
        EditorApplication.isPlaying=false;
    }
    static void Tick()
    {
        if(!SessionState.GetBool("CharacterNavigationRegression",false)||!EditorApplication.isPlaying)return;
        EditorApplication.QueuePlayerLoopUpdate();
        if(EditorApplication.isCompiling||EditorApplication.timeSinceStartup<ready)return;
        try{
        if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timed out at phase "+phase);
        var ui=UIManager.Instance;if(ui==null)return;
        bool Visible()=>entry!=null&&entry.gameObject.activeInHierarchy&&entry.GetComponent<CanvasGroup>().alpha>.9f&&entry.GetComponent<CanvasGroup>().blocksRaycasts;
        switch(phase){
        case 0:
            player=UnityEngine.Object.FindFirstObjectByType<PlayerInputHandler>();axis=UnityEngine.Object.FindFirstObjectByType<CinemachineInputAxisController>();entry=UnityEngine.Object.FindFirstObjectByType<CharacterEntryButton>();
            if(player==null||axis==null||entry==null)return;
            Next(1);break;
        case 1:
            Check(axis.enabled,"camera axis initially enabled");Check(Visible(),"entry visible on main HUD");
            entry.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();Next(2);break;
        case 2:
            if(!ui.IsOpen(UIId.CharacterDetail))return;
            Check(!axis.enabled,"camera blocked while character UI open");Check(!Visible(),"main entry suspended during character UI");Check(ui.Back(),"UIManager.Back handles character return");Next(3);break;
        case 3:
            if(CharacterSceneNavigator.IsActive)return;
            Check(axis.enabled&&player.CanMove&&!ui.Input.Current.BlockCamera,"camera and movement restored after Back");Check(Visible(),"main entry restored after Back");
            window=ui.Open(UIId.Backpack);Next(4);break;
        case 4:
            Check(!Visible(),"entry hidden and not raycastable under backpack");Check(!axis.enabled,"backpack keeps camera locked");Check(CharacterSceneNavigator.TryEnter(),"navigation from an existing window accepted");Next(5);break;
        case 5:
            if(!ui.IsOpen(UIId.CharacterDetail))return;
            CharacterSceneNavigator.Instance.Return();Next(6);break;
        case 6:
            if(CharacterSceneNavigator.IsActive)return;
            Check(ui.IsOpen(UIId.Backpack)&&!axis.enabled&&ui.Input.Current.BlockCamera,"return preserves source window camera lock");Check(!Visible(),"entry stays hidden when source window restored");ui.Close(window);Next(7);break;
        case 7:
            Check(axis.enabled&&Visible(),"closing final window restores camera and entry");Check(PartySceneNavigator.TryEnter(SceneTransitionMode.Immediate),"real additive party entry accepted");Next(8);break;
        case 8:
            if(GameSceneManager.Instance.IsLoading)return;
            Check(!Visible(),"main entry hidden under party window");var party=UnityEngine.Object.FindFirstObjectByType<BattlePartyCanvasUI>();Check(party!=null&&party.GetComponentInChildren<CharacterEntryButton>(true)==null,"party prefab has no character entry");PartySceneNavigator.Return();Next(9);break;
        case 9:
            if(GameSceneManager.Instance.IsLoading)return;
            Check(axis.enabled&&Visible(),"party close restores main state");axis.enabled=false;Check(CharacterSceneNavigator.TryEnter(),"enter with pre-disabled camera");Next(10);break;
        case 10:
            if(!ui.IsOpen(UIId.CharacterDetail))return;CharacterSceneNavigator.Instance.Return();Next(11);break;
        case 11:
            if(CharacterSceneNavigator.IsActive)return;Check(!axis.enabled,"pre-disabled camera remains disabled");axis.enabled=true;entry.Open();Next(12);break;
        case 12:
            if(!ui.IsOpen(UIId.CharacterDetail))return;ui.Back();Next(13);break;
        case 13:
            if(CharacterSceneNavigator.IsActive)return;Check(axis.enabled&&Visible()&&player.CanMove,"repeated enter return leaves no input lock");Check(Time.timeScale>0,"time restored");Finish();break;
        }
        }catch(Exception e){Finish(e.ToString());}
    }
}
