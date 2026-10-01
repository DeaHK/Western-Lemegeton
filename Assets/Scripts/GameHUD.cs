using System.Collections.Generic;
using UnityEngine;
namespace WesternLemegeton
{
 public sealed class GameHUD:MonoBehaviour
 {
  public GameUIView View;
  public bool SettingsOpen {get;private set;}
  public bool BuildTreeOpen {get;private set;}
  public int SelectedSigil {get;private set;}
  public OwnedPassive Toast {get;private set;}
  public float ToastOpacity=>Toast==null?0:Mathf.Clamp01(Mathf.Min((2.4f-toastTime)/.15f,toastTime/.3f));
  readonly float[] pulses=new float[3];readonly Queue<OwnedPassive> notices=new Queue<OwnedPassive>();
  RunBuild observed;float toastTime;RunState treeReturn;
  public float StackPulse(SeongheunType type)=>Mathf.Max(0,pulses[(int)type]-Time.unscaledTime);
  public int PendingPassiveNotices=>notices.Count+(Toast!=null?1:0);
  void Connect(){if(observed||!Dungeon.I||!Dungeon.I.Build)return;observed=Dungeon.I.Build;observed.StackChanged+=StackChanged;observed.PassiveAcquired+=Acquired;observed.Reset+=ResetBuild;}
  void OnEnable(){Connect();}
  void OnDisable(){if(!observed)return;observed.StackChanged-=StackChanged;observed.PassiveAcquired-=Acquired;observed.Reset-=ResetBuild;observed=null;}
  void StackChanged(SeongheunType t,int before,int after){if(after>before)pulses[(int)t]=Time.unscaledTime+.25f;}
  void Acquired(OwnedPassive p){notices.Enqueue(p);}
  void ResetBuild(){System.Array.Clear(pulses,0,3);notices.Clear();Toast=null;BuildTreeOpen=false;toastTime=0;}
  void Update()
  {
   Connect();float dt=Time.unscaledDeltaTime;
   if(Toast!=null&&Dungeon.I.State!=RunState.Route){toastTime-=dt;if(toastTime<=0)Toast=null;}
   if(Toast==null&&notices.Count>0){Toast=notices.Dequeue();toastTime=2.4f;}
   if(BuildTreeOpen&&Dungeon.I.State!=RunState.Paused)BuildTreeOpen=false;
  }
  public void OpenSettings(){SettingsOpen=true;}
  public void CloseSettings(){SettingsOpen=false;}
  public void SelectSigil(int index){SelectedSigil=Mathf.Clamp(index,0,2);}
  public void OpenBuildTree(){var g=Dungeon.I;if(!g.Running)return;BuildTreeOpen=true;treeReturn=g.State;g.BeforePause=g.State;g.State=RunState.Paused;}
  public void CloseBuildTree(){if(!BuildTreeOpen)return;BuildTreeOpen=false;Dungeon.I.State=treeReturn;}
 }
}
