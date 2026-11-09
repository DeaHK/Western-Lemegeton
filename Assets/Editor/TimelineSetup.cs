using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using WesternLemegeton;
public static class TimelineSetup
{
 const string Folder="Assets/Resources/Cinematics";
 public static void EnsureAssets()
 {
  Directory.CreateDirectory(Folder);
  Create("DungeonEntrance",CinematicKind.DungeonEntrance,2.8);
  Create("PlayerDeath",CinematicKind.PlayerDeath,2.7);
  Create("BossEntrance",CinematicKind.BossEntrance,3.4);
  AssetDatabase.SaveAssets();
 }
 static void Create(string name,CinematicKind kind,double duration)
 {
  string path=Folder+"/"+name+".playable";if(AssetDatabase.LoadAssetAtPath<TimelineAsset>(path))return;
  var timeline=ScriptableObject.CreateInstance<TimelineAsset>();AssetDatabase.CreateAsset(timeline,path);
  timeline.editorSettings.fps=60;timeline.durationMode=TimelineAsset.DurationMode.FixedLength;timeline.fixedDuration=duration;
  var camera=Track(timeline,CinematicCue.Camera,"01 Camera · focus and zoom");
  if(kind==CinematicKind.BossEntrance)
  {Clip(camera,"Focus on the seal keeper",0,1,0,1);Clip(camera,"Hold boss close-up",1,1.1,1,1);Clip(camera,"Return to combat camera",2.1,1.3,1,0);}
  else Clip(camera,kind==CinematicKind.PlayerDeath?"Move closer to fallen hunter":"Pull back into the wilderness",0,duration,kind==CinematicKind.PlayerDeath?0:1,kind==CinematicKind.PlayerDeath?1:0);
  var actor=Track(timeline,CinematicCue.Actor,"02 Actor · arrival or collapse");
  if(kind==CinematicKind.PlayerDeath){Clip(actor,"Hunter collapse and raven retreat",0,.9,0,1);Clip(actor,"Hold fallen pose before results",.9,duration-.9,1,1);}
  else Clip(actor,kind==CinematicKind.BossEntrance?"Boss materializes":"Hunter and raven arrival",0,duration,0,1);
  Clip(Track(timeline,CinematicCue.Letterbox,"03 Cinematic frame"),"Letterbox",0,duration,1,1,.2f,.25f);
  var title=Track(timeline,CinematicCue.Title,"04 Name card");
  Clip(title,kind==CinematicKind.BossEntrance?"봉인의 파수꾼":kind==CinematicKind.DungeonEntrance?"Stage name":"쓰러진 사냥꾼",.4,kind==CinematicKind.PlayerDeath?1.1:duration-.75,1,1,.25f,.3f);
  var fade=Track(timeline,CinematicCue.Fade,"05 Fade");
  if(kind==CinematicKind.PlayerDeath)Clip(fade,"Fade to black before results",1.25,duration-1.25,0,1,0,0);
  else Clip(fade,"Reveal the scene",0,.48,1,0,0,0);
  Clip(Track(timeline,CinematicCue.Seal,"06 Ground effect"),kind==CinematicKind.PlayerDeath?"Fading stigma":"Summoning seal",.12,kind==CinematicKind.PlayerDeath?1.5:2.1,0,1,.2f,.45f);
  string audioPath=Folder+"/"+name+".wav";WriteAudio(audioPath,(float)duration,kind);AssetDatabase.ImportAsset(audioPath,ImportAssetOptions.ForceSynchronousImport);
  var sound=timeline.CreateTrack<AudioTrack>(null,"07 Audio · cinematic cue");var soundClip=sound.CreateClip<AudioPlayableAsset>();soundClip.start=0;soundClip.duration=duration;
  ((AudioPlayableAsset)soundClip.asset).clip=AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);soundClip.displayName=name+" ambience";
  EditorUtility.SetDirty(timeline);
 }
 static CinematicTrack Track(TimelineAsset t,CinematicCue channel,string name){var track=t.CreateTrack<CinematicTrack>(null,name);track.Channel=channel;EditorUtility.SetDirty(track);return track;}
 static void Clip(CinematicTrack track,string name,double start,double duration,float from,float to,float fadeIn=0,float fadeOut=0)
 {var c=track.CreateClip<CinematicClip>();c.displayName=name;c.start=start;c.duration=duration;var a=(CinematicClip)c.asset;a.Cue=track.Channel;a.From=from;a.To=to;a.FadeIn=fadeIn;a.FadeOut=fadeOut;EditorUtility.SetDirty(a);EditorUtility.SetDirty(track);}
 static void WriteAudio(string path,float duration,CinematicKind kind)
 {
  if(File.Exists(path))return;const int rate=22050;int count=(int)(duration*rate);var rng=new System.Random(720+(int)kind);double phase=0;float noise=0;
  using(var writer=new BinaryWriter(File.Create(path)))
  {
   writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
   for(int i=0;i<count;i++)
   {
    float time=i/(float)rate,p=time/duration;float frequency=kind==CinematicKind.PlayerDeath?Mathf.Lerp(150,45,p):kind==CinematicKind.BossEntrance?Mathf.Lerp(52,82,p):Mathf.Lerp(180,240,p);
    phase+=frequency*Math.PI*2/rate;noise=Mathf.Lerp(noise,(float)rng.NextDouble()*2-1,.06f);
    float env=Mathf.Min(1,time/.16f,(duration-time)/.6f);float value=((float)Math.Sin(phase)*.12f+(float)Math.Sin(phase*1.5)*.035f+noise*.18f)*env;
    writer.Write((short)(Mathf.Clamp(value,-1,1)*32767));
   }
  }
 }
 public static void Validate()
 {
  foreach(string name in new[]{"DungeonEntrance","PlayerDeath","BossEntrance"})
  {
   var t=AssetDatabase.LoadAssetAtPath<TimelineAsset>(Folder+"/"+name+".playable");if(!t||t.duration<2||t.duration>5)throw new Exception("Invalid cinematic duration: "+name);
   int count=0;foreach(var track in t.GetOutputTracks()){count++;foreach(var clip in track.GetClips())if(!clip.asset)throw new Exception("Missing cinematic clip: "+name);}
   if(count!=7)throw new Exception("Missing cinematic tracks: "+name);Debug.Log("PASS: Timeline "+name+" / seven editable tracks / "+t.duration+" sec");
  }
 }
 // Apply once to the newly authored death asset, without replacing its other tracks.
 public static void FinalizeDeathTimingAndBuild()
 {
  var timeline=AssetDatabase.LoadAssetAtPath<TimelineAsset>(Folder+"/PlayerDeath.playable");
  foreach(var output in timeline.GetOutputTracks())if(output is CinematicTrack track&&track.Channel==CinematicCue.Actor)
  {
   var clips=new System.Collections.Generic.List<TimelineClip>(track.GetClips());
   if(clips.Count==1){clips[0].duration=.9;Clip(track,"Hold fallen pose before results",.9,timeline.duration-.9,1,1);EditorUtility.SetDirty(track);EditorUtility.SetDirty(timeline);}
  }
  AssetDatabase.SaveAssets();ProjectSetup.ValidateAndBuild();
 }
 [MenuItem("Western Lemegeton/Timeline/Select Dungeon Entrance")]
 public static void SelectEntrance(){EnsureAssets();Selection.activeObject=AssetDatabase.LoadAssetAtPath<TimelineAsset>(Folder+"/DungeonEntrance.playable");}
}
