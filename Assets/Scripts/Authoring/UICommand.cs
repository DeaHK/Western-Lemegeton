using UnityEngine;
namespace WesternLemegeton
{
 // Values are serialized in existing prefabs. Retired actions 8–10 stay unassigned.
 public enum GameUIAction {EnterTown=0,OpenSettings=1,CloseSettings=2,Resume=3,OpenMap=4,CloseMap=5,NextStage=6,OpenStigma=7,ClaimCard=11,CloseService=12,ToggleFullscreen=13}
 public sealed class UICommand:MonoBehaviour
 {
  public GameUIAction Action;public int Parameter;
  public void Execute()
  {
   var g=Dungeon.I;if(!g)return;var hud=g.GetComponent<GameHUD>();
   switch(Action)
   {
    case GameUIAction.EnterTown:hud.CloseSettings();g.EnterTown();break;
    case GameUIAction.OpenSettings:hud.OpenSettings();break;
    case GameUIAction.CloseSettings:hud.CloseSettings();break;
    case GameUIAction.Resume:g.State=g.BeforePause;break;
    case GameUIAction.OpenMap:g.OpenRoute();break;
    case GameUIAction.CloseMap:g.CloseRoute();break;
    case GameUIAction.NextStage:if(!g.IsTown&&g.Room==4)g.FinishExpedition();else g.ChooseRoom(g.NextRoom);break;
    case GameUIAction.OpenStigma:hud.OpenStigmaManagement();break;
    case GameUIAction.ClaimCard:g.ClaimCard(Parameter);break;
    case GameUIAction.CloseService:g.CloseService();break;
    case GameUIAction.ToggleFullscreen:Screen.fullScreen=!Screen.fullScreen;break;
   }
  }
  public void SetVolume(float value){AudioListener.volume=value;}
 }
}
