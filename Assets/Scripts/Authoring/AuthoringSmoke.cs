using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WesternLemegeton.Passives;
namespace WesternLemegeton
{
 public sealed class AuthoringSmoke:MonoBehaviour
 {
  int checks;Dungeon g;GameUIView ui;
  void Check(bool ok,string text){if(!ok){Debug.LogError("AUTHORING_FAILED: "+text);Application.Quit(2);throw new System.Exception(text);}checks++;Debug.Log("AUTHORING_PASS: "+text);}
  void Click(string key)
  {
   var button=ui.Element<Button>(key);Check(button&&button.IsActive(),"Native button is active: "+key);
   var rect=button.GetComponent<RectTransform>();Vector3[] corners=new Vector3[4];rect.GetWorldCorners(corners);
   var pointer=new PointerEventData(EventSystem.current){position=(corners[0]+corners[2])*.5f,button=PointerEventData.InputButton.Left};
   var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
   Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Canvas raycast reaches button: "+key);
   ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
  }
  IEnumerator Start()
  {
   Application.runInBackground=true;yield return new WaitForSeconds(.5f);g=Dungeon.I;ui=g.Scene.UI;var hud=g.GetComponent<GameHUD>();
   var originalHunter=g.Hero;var originalTown=g.Scene.Hirva;var originalRoom=g.Scene.Rooms[0];
   Check(ui.TitlePanel.activeInHierarchy&&ui.GetComponent<Canvas>(),"Serialized Canvas shows title");
   Click("Start");yield return null;yield return new WaitForEndOfFrame();Check(g.IsTown&&ui.HudPanel.activeInHierarchy,"Title button enters authored Hirva");
   hud.OpenSettings();yield return null;yield return new WaitForEndOfFrame();Check(!g.Running&&ui.SettingsPanel.activeInHierarchy,"Settings suspend game input");Click("Settings_Close");yield return null;yield return new WaitForEndOfFrame();
   Click("Map_Button");yield return null;yield return new WaitForEndOfFrame();Check(ui.RoutePanel.activeInHierarchy&&!g.RouteTravel,"HUD map is read-only");Click("Route_Close");yield return null;yield return new WaitForEndOfFrame();
   Click("Inspect_Stigma0");yield return null;yield return new WaitForEndOfFrame();Check(ui.SigilPanel.activeInHierarchy&&!g.Running,"HUD opens build tree");Click("Sigil_Select_4");Check(hud.SelectedSigil==2,"Persistent button parameter selects Butterfly");Click("Sigil_Close");yield return null;
   g.ApplySeongheunStack(SeongheunType.Fire,2);Check(hud.StackPulse(SeongheunType.Fire)>0,"Stack event starts punch immediately");yield return null;yield return new WaitForEndOfFrame();Check(ui.Element<Text>("Stack0").text=="\uAC01\uC778 2","Stack event updates native text: "+ui.Element<Text>("Stack0").text);
   int[] passiveIds={1,4,5};var sources=new[]{PassiveAcquisitionSource.MonsterDrop,PassiveAcquisitionSource.ShopPurchase,PassiveAcquisitionSource.EventReward};
   for(int i=0;i<3;i++)Check(g.Passives.AcquirePassiveById(passiveIds[i],sources[i]).WasAdded,"Database acquisition path "+i);
   yield return null;yield return new WaitForEndOfFrame();Check(ui.PassiveContent.childCount==3&&ui.ToastPanel.activeInHierarchy,"Prefab inventory slots and toast created");
   for(int i=0;i<3;i++){var rarity=g.Passives.Database.GetById(passiveIds[i]).Rarity;int index=(int)rarity;var expected=index<ui.RarityColors.Length?ui.RarityColors[index]:rarity==PassiveRarity.Legendary?new Color(1f,.72f,.18f):ui.InactiveColor;Check(ui.PassiveContent.GetChild(i).GetComponent<PassiveSlotView>().Border.color==expected,"Definition rarity color "+i);}
   g.Hero.transform.position=RoomAuthoring.Point(g.Scene.Hirva.TownExit);Check(g.TryTravel(),"Authored town exit opens route");yield return null;yield return new WaitForEndOfFrame();Click("RouteTravel");yield return null;g.Cinematics.Skip();
   Check(g.Scene.Rooms[0]==originalRoom&&originalRoom.gameObject.activeInHierarchy&&g.Hero==originalHunter,"Stage uses saved objects and keeps player identity");
   foreach(var enemy in g.Enemies){Check(enemy.AuthoredArt&&enemy.GetComponent<ActorPresentation>()&&enemy.GetComponentInChildren<PaperCard>(),"Enemy spawned from editable prefab");enemy.enabled=false;}
   g.Hero.enabled=false;g.Crow.enabled=false;
   var presentation=g.Hero.GetComponent<ActorPresentation>();presentation.UseCustomVisual=true;string requested="";presentation.AnimationRequested.AddListener((a,l,s)=>requested=a);
   g.Hero.SafeEntry();g.Hero.AdvanceCombat(.1f,Vector2.right);yield return new WaitForEndOfFrame();
   Check(presentation.CustomVisual.gameObject.activeInHierarchy&&presentation.SpriteCard.OverrideHidden&&!presentation.SpriteCard.Visible.enabled,"Custom visual hides fallback sprite");Check(requested=="run","Animation adapter receives locomotion");
   g.Hero.TryAttack();yield return new WaitForEndOfFrame();Check(requested=="attack01","Animation adapter receives combo stage");
   presentation.UseCustomVisual=false;g.Hero.SafeEntry();yield return null;yield return new WaitForEndOfFrame();Check(presentation.SpriteCard.Visible.enabled&&!presentation.CustomVisual.gameObject.activeSelf,"Sprite fallback can be restored");
   g.Hero.transform.position=g.RoomCenter;yield return new WaitForSeconds(.4f);yield return new WaitForEndOfFrame();
   string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Logs"));var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,"authoring-combat.png"),image.EncodeToPNG());Destroy(image);
   g.EnterTown();yield return null;yield return new WaitForEndOfFrame();yield return null;Check(g.Hero==originalHunter&&g.Scene.Hirva==originalTown&&g.Scene.Rooms[0]==originalRoom,"New run preserves all authored references");Check(ui.PassiveContent.childCount==0,"New run clears only acquired inventory slots");
   File.WriteAllText(Path.Combine(output,"authoring-checks.txt"),"PASS: "+checks+" serialized scene, Canvas interaction and animation adapter checks.");Debug.Log("WNN_AUTHORING_SMOKE_SUCCESS: "+checks);Application.Quit(0);
  }
 }
}
