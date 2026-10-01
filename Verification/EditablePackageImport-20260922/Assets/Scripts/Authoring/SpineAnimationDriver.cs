using UnityEngine;
namespace WesternLemegeton
{
 // Install the matching official spine-unity runtime, then enable WNN_SPINE.
 public sealed class SpineAnimationDriver:ActorAnimationDriver
 {
#if WNN_SPINE
  public Spine.Unity.SkeletonAnimation Skeleton;
  public int Track;
  public override void Play(string animationName,bool loop,float speed)
  {
   if(!Skeleton||string.IsNullOrEmpty(animationName))return;
   Skeleton.Initialize(false);
   if(Skeleton.Skeleton.Data.FindAnimation(animationName)==null){Debug.LogWarning("Spine animation not found: "+animationName,this);return;}
   Skeleton.AnimationState.SetAnimation(Track,animationName,loop);SetPlaybackSpeed(speed);
  }
  public override void SetPlaybackSpeed(float speed){if(Skeleton)Skeleton.timeScale=speed;}
#else
  [TextArea]public string Setup="Install spine-unity, add WNN_SPINE to Scripting Define Symbols, then assign SkeletonAnimation here and this driver to ActorPresentation.AnimationDriver.";
  public override void Play(string animationName,bool loop,float speed){}
  public override void SetPlaybackSpeed(float speed){}
#endif
 }
}
