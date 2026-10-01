using UnityEngine;
namespace WesternLemegeton
{
 public abstract class ActorAnimationDriver:MonoBehaviour
 {
  public abstract void Play(string animationName,bool loop,float speed);
  public abstract void SetPlaybackSpeed(float speed);
 }
}
