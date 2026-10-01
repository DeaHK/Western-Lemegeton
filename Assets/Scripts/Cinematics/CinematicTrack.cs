using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
namespace WesternLemegeton
{
 [TrackColor(.67f,.27f,.20f),TrackClipType(typeof(CinematicClip)),TrackBindingType(typeof(CinematicDirector))]
 public sealed class CinematicTrack:TrackAsset
 {
  public CinematicCue Channel;
  public override Playable CreateTrackMixer(PlayableGraph graph,GameObject go,int inputCount)
  {var p=ScriptPlayable<CinematicMixer>.Create(graph,inputCount);p.GetBehaviour().Channel=Channel;return p;}
 }
}
