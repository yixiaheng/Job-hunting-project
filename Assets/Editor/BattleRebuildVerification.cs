using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Battle.Logic;
using Battle.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class BattleRebuildVerification
{
    [MenuItem("Tools/Battle/Presentation/Verify Rebuilt System")]
    public static void Run()
    {
        var checks = new List<string>();
        void Check(bool condition,string text) { if(!condition)throw new InvalidOperationException(text);checks.Add(text); }
        var layout=ScriptableObject.CreateInstance<BattlefieldLayout>();
        var profile=ScriptableObject.CreateInstance<BattleCameraProfile>();
        var basic=ScriptableObject.CreateInstance<SkillConfig>();
        var skill=ScriptableObject.CreateInstance<SkillConfig>();
        var root=new GameObject("Rebuild verification");
        string error=null;
        try
        {
            layout.InitializeDefaults();profile.InitializeShots();
            Check(layout.Allies.Length==4,"four logical ally slots have authored visual poses");
            for(int count=1;count<=5;count++)
            {
                var slots=Enumerable.Range(0,count).Select(i=>layout.GetSlot(Faction.Enemy,i,count)).ToArray();
                Check(slots.Length==count && Mathf.Abs(slots.Average(x=>x.position.x))<.001f,"enemy formation centered for count "+count);
            }
            Check(layout.GetSlot(Faction.Enemy,0,1,"Boss").position.z>layout.GetSlot(Faction.Enemy,0,1).position.z,"Boss has a separate depth preset");
            Check(Enum.GetValues(typeof(BattleShotMode)).Cast<BattleShotMode>().All(m=>profile.Find(m)!=null),"all public shot modes configured");
            basic.kind=SkillKind.Basic;basic.targetRule=TargetRule.Single;basic.targetSide=TargetSide.OtherSide;
            skill.kind=SkillKind.Skill;skill.targetRule=TargetRule.Spread;skill.targetSide=TargetSide.OtherSide;
            basic.effectConfigs=new List<EffectConfig>{new(){effectType=EffectType.Damage,value=1,scaleStat=StatType.Attack}};
            skill.effectConfigs=basic.effectConfigs;
            BattleUnit Make(Faction side,int pos,int speed)=>new(){displayName=side+" "+pos,faction=side,pos=pos,tieBreaker=pos,maxHealth=100,currentHealth=100,attack=10,defence=0,speed=speed,maxEnergy=100};
            var ally=Make(Faction.OurSide,0,200);ally.skills.Add(basic);ally.skills.Add(skill);
            var enemies=Enumerable.Range(0,3).Select(i=>Make(Faction.Enemy,i,100)).ToArray();
            var flow=new BattleFlow(new[]{ally}.Concat(enemies));flow.Ready();
            var selection=new BattleSelectionState(flow);selection.Begin(ally);
            Check(selection.Skill==basic && selection.Target==enemies[0],"turn begins with legal basic and canonical target");
            int hp=enemies[0].currentHealth;double time=flow.CurrentTime;int sp=flow.SkillPoints.Current;
            Check(selection.SelectSkill(SkillKind.Skill)&&selection.SelectTarget(enemies[1])&&selection.Preview.Targets.Count==3,"spread preview selects exact neighboring logical slots");
            Check(enemies[0].currentHealth==hp && flow.CurrentTime==time && flow.SkillPoints.Current==sp,"changing selection has no battle side effects");
            Check(!selection.SelectTarget(Make(Faction.Enemy,0,100)),"foreign battle target refused");
            var field=root.AddComponent<BattlefieldDirector>();field.Configure(layout,root.transform);
            var identity=new GameObject("Identity");identity.transform.SetParent(root.transform,false);
            var visual=new GameObject("PresentationRoot");visual.transform.SetParent(identity.transform,false);
            var model=GameObject.CreatePrimitive(PrimitiveType.Capsule);model.transform.SetParent(visual.transform,false);
            var actor=model.AddComponent<BattleUnitActor>();actor.ConfigurePresentationRoot(visual.transform,model.transform);actor.Bind(ally);field.Register(actor,4);
            var home=identity.transform.position;int logical=ally.pos;field.SetReady(ally,true);
            Check(ally.pos==logical&&identity.transform.position==home&&Vector3.Distance(visual.transform.position,home)>.1f,"ready motion moves only visual root");
            field.ReturnHome(ally,true);Check(Vector3.Distance(visual.transform.position,home)<.001f,"return home restores authored pose");
            var bounds=new[]{new Bounds(new Vector3(0,1,0),new Vector3(10,3,1))};
            foreach(float aspect in new[]{4f/3f,16f/9f,21f/9f})
            {
                var safe=new Rect(.12f,.22f,.76f,.65f);var pos=BattleShotDirector.Fit(new Vector3(0,1,-2),Quaternion.identity,45,bounds,safe,aspect);
                var go=new GameObject("Framing camera");go.transform.SetParent(root.transform,false);var cam=go.AddComponent<Camera>();cam.enabled=false;cam.aspect=aspect;cam.fieldOfView=45;cam.transform.position=pos;
                for(int i=0;i<8;i++){var point=bounds[0].center+Vector3.Scale(bounds[0].extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));var v=cam.WorldToViewportPoint(point);Check(v.z>0&&v.x>=safe.xMin-.001f&&v.x<=safe.xMax+.001f&&v.y>=safe.yMin-.001f&&v.y<=safe.yMax+.001f,"safe framing corner at aspect "+aspect);}
                var actorPoint=new Vector3(-2,1,-3);var actorViewport=new Vector2(.22f,.28f);
                foreach(var yaw in new[]{-25f,0f,30f})
                {
                    var rotation=Quaternion.Euler(8,yaw,0);var locked=BattleShotDirector.LockToViewport(new Vector3(2,2,-5),rotation,45,actorPoint,actorViewport,aspect);
                    cam.transform.SetPositionAndRotation(locked,rotation);var actual=cam.WorldToViewportPoint(actorPoint);
                    Check(actual.z>0&&Vector2.Distance(actual,actorViewport)<.0001f,"actor viewport lock survives target direction at aspect "+aspect+" yaw "+yaw);
                }
            }
            var director=root.AddComponent<BattleShotDirector>();director.ConfigureBattlefield(field);
            var shot=new BattleCameraShotBehaviour{lookAtTarget=false,keys=new[]{new BattleCameraKey{time=0,position=new Vector3(0,2,-5),fov=40},new BattleCameraKey{time=2,position=new Vector3(2,3,-4),fov=60}}};
            var pose=shot.Evaluate(director,.5f,1);Check(Vector3.Distance(pose.Position,new Vector3(1,2.5f,-4.5f))<.001f&&Mathf.Abs(pose.Fov-50)<.001f,"camera keys interpolate position and FOV in seconds");
            shot.keys[1].cut=true;shot.ResetReference();pose=shot.Evaluate(director,.9f,1.8f);Check(Mathf.Abs(pose.Fov-40)<.001f,"cut key holds preceding pose until cut timestamp");
            var asset=ScriptableObject.CreateInstance<BattleCameraProfile>();asset.InitializeShots();
            string assetPath="Assets/Config/Battle/Presentation/VerificationPersistence_"+Guid.NewGuid().ToString("N")+".asset";
            AssetDatabase.CreateAsset(asset,assetPath);
            var copy=Object.Instantiate(asset); var restored=ScriptableObject.CreateInstance<BattleCameraProfile>();
            var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
            string file=(string)typeof(BattlePresentationStudio).GetMethod("DraftPath",flags).Invoke(null,new object[]{asset});
            try
            {
                string baseline=JsonUtility.ToJson(asset);copy.Find(BattleShotMode.EnemySingle).fov=67;
                typeof(BattlePresentationStudio).GetMethod("Persist",flags).Invoke(null,new object[]{asset,copy,baseline});
                Object.DestroyImmediate(copy);copy=null;
                object[] arguments={asset,restored,null};typeof(BattlePresentationStudio).GetMethod("LoadDraft",flags).Invoke(null,arguments);
                Check(restored.Find(BattleShotMode.EnemySingle).fov==67 && asset.Find(BattleShotMode.EnemySingle).fov!=67,"disk draft restores after transient instance destruction without changing source asset");
                Check((restored.hideFlags & HideFlags.NotEditable)==0,"Studio draft does not disable Inspector numeric editing");
                using (var serialized = new SerializedObject(restored))
                {
                    var fov = serialized.FindProperty("shots").GetArrayElementAtIndex(0).FindPropertyRelative("fovValue");
                    Check(fov.editable,"Studio camera FOV SerializedProperty is editable");
                    fov.floatValue=69;serialized.ApplyModifiedPropertiesWithoutUndo();
                    Check(restored.Find(BattleShotMode.EnemySingle).fov==69,"numeric serialized edit reaches camera draft");
                }
                restored.Find(BattleShotMode.EnemySingle).fov=67;
                asset.Find(BattleShotMode.EnemySingle).fov=51;
                Check(!BattlePresentationStudio.SyncCleanDraft(asset,restored,ref baseline) && restored.Find(BattleShotMode.EnemySingle).fov==67,"external Config edit preserves unsaved Studio changes");
                EditorUtility.CopySerialized(asset,restored);baseline=JsonUtility.ToJson(asset);
                asset.Find(BattleShotMode.EnemySingle).fov=53;
                Check(BattlePresentationStudio.SyncCleanDraft(asset,restored,ref baseline) && restored.Find(BattleShotMode.EnemySingle).fov==53,"clean Studio draft follows Config edits");
                typeof(BattlePresentationStudio).GetMethod("Persist",flags).Invoke(null,new object[]{asset,restored,baseline});
                asset.Find(BattleShotMode.EnemySingle).fov=55;
                arguments=new object[]{asset,restored,null};typeof(BattlePresentationStudio).GetMethod("LoadDraft",flags).Invoke(null,arguments);
                Check(restored.Find(BattleShotMode.EnemySingle).fov==55,"reopening Studio ignores stale unchanged disk draft after Config edit");
                restored.Find(BattleShotMode.EnemySingle).fov=67;
                string guid=AssetDatabase.AssetPathToGUID(assetPath);BattlePresentationStudio.SaveDraft(asset,restored);
                Check(asset.Find(BattleShotMode.EnemySingle).fov==67 && AssetDatabase.AssetPathToGUID(assetPath)==guid,"explicit save persists restored draft and preserves GUID");
            }
            finally{if(copy!=null)Object.DestroyImmediate(copy);Object.DestroyImmediate(restored);AssetDatabase.DeleteAsset(assetPath);if(File.Exists(file))File.Delete(file);}
            var window=ScriptableObject.CreateInstance<BattlePresentationStudio>();
            try{Check(window!=null,"Studio creates isolated preview without entering Play Mode");}finally{Object.DestroyImmediate(window);}
        }
        catch(Exception e){error=e.ToString();Debug.LogException(e);}
        finally
        {
            Object.DestroyImmediate(root);Object.DestroyImmediate(layout);Object.DestroyImmediate(profile);Object.DestroyImmediate(basic);Object.DestroyImmediate(skill);
            Directory.CreateDirectory("Temp/BattlePresentation");File.WriteAllText("Temp/BattlePresentation/rebuild.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=error==null,error,checks,count=checks.Count},Newtonsoft.Json.Formatting.Indented));
        }
        if(error!=null)throw new InvalidOperationException(error);
    }
}
