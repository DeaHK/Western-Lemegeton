using UnityEngine;
namespace WesternLemegeton
{
 public enum GameUIAction {EnterTown,OpenSettings,CloseSettings,Resume,OpenMap,CloseMap,NextStage,OpenTree,SelectSigil,ClaimSigil,CloseSigil,ClaimCard,CloseService,ToggleFullscreen}
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
    case GameUIAction.OpenTree:hud.OpenBuildTree();break;
    case GameUIAction.SelectSigil:hud.SelectSigil(Parameter);break;
    case GameUIAction.ClaimSigil:g.ClaimSigil((SeongheunType)hud.SelectedSigil);break;
    case GameUIAction.CloseSigil:if(hud.BuildTreeOpen)hud.CloseBuildTree();else g.CloseService();break;
    case GameUIAction.ClaimCard:g.ClaimCard(Parameter);break;
    case GameUIAction.CloseService:g.CloseService();break;
    case GameUIAction.ToggleFullscreen:Screen.fullScreen=!Screen.fullScreen;break;
   }
  }
  public void SetVolume(float value){AudioListener.volume=value;}
 }
}
