using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
namespace WesternLemegeton
{
 public enum CinematicCue { Camera, Actor, Letterbox, Title, Fade, Seal }
 [System.Serializable]
 public sealed class CinematicClip:PlayableAsset,ITimelineClipAsset
 {
  public CinematicCue Cue;
  public float From,To=1;
  public AnimationCurve Curve=AnimationCurve.EaseInOut(0,0,1,1);
  public float FadeIn=.15f,FadeOut=.2f;
  public ClipCaps clipCaps=>ClipCaps.None;
  public override Playable CreatePlayable(PlayableGraph graph,GameObject owner)
  {var p=ScriptPlayable<CinematicBehaviour>.Create(graph);p.GetBehaviour().Clip=this;return p;}
 }
 public sealed class CinematicBehaviour:PlayableBehaviour {public CinematicClip Clip;}
 public sealed class CinematicMixer:PlayableBehaviour
 {
  public CinematicCue Channel;
  public override void ProcessFrame(Playable playable,FrameData info,object playerData)
  {
   var director=playerData as CinematicDirector;if(!director||!director.IsPlaying)return;
   float value=0,phase=0,envelope=0;
   for(int i=0;i<playable.GetInputCount();i++)
   {
    float weight=playable.GetInputWeight(i);if(weight<=0)continue;
    var p=(ScriptPlayable<CinematicBehaviour>)playable.GetInput(i);var c=p.GetBehaviour().Clip;
    float time=(float)p.GetTime(),duration=(float)p.GetDuration();phase=Mathf.Clamp01(time/Mathf.Max(.001f,duration));
    float ease=c.Curve.Evaluate(phase);value+=Mathf.LerpUnclamped(c.From,c.To,ease)*weight;
    envelope+=Mathf.Min(c.FadeIn<=0?1:Mathf.Clamp01(time/c.FadeIn),c.FadeOut<=0?1:Mathf.Clamp01((duration-time)/c.FadeOut))*weight;
   }
   director.ApplyCue(Channel,value,phase,envelope);
  }
 }
}
