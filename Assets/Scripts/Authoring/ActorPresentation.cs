using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
namespace WesternLemegeton
{
    [System.Serializable] public class AnimationRequest:UnityEvent<string,bool,float>{}
    [System.Serializable] public class MotionBinding
    {public string State,Animation;public bool Loop;public MotionBinding(string s,string a,bool loop){State=s;Animation=a;Loop=loop;}}
    [DefaultExecutionOrder(250)]
    public sealed class ActorPresentation:MonoBehaviour
    {
        [Tooltip("Enable after placing a Spine/Animator visual in the Custom Visual slot.")]
        public bool UseCustomVisual;
        // TEMP(spine-run-only): 달리기 스파인만 먼저 받은 동안 쓰는 임시 스위치.
        // 켜면 "Run" 상태에서만 Custom Visual(스파인)을 보이고, 나머지 상태는 기존 스프라이트를 보인다.
        // 모든 동작이 스파인으로 교체되면 이 필드와 아래 TEMP 블록을 지우고 showCustom을 UseCustomVisual로 되돌린다.
        [Tooltip("임시: 달리는 동안에만 Custom Visual을 보이고, 그 외에는 기존 스프라이트를 쓴다.")]
        public bool SpineRunOnly;
        public Transform CustomVisual;
        public SpriteRenderer SpriteFrames;
        public PaperCard SpriteCard;
        public Animator Animator;
        public ActorAnimationDriver AnimationDriver;
        public AnimationRequest AnimationRequested=new AnimationRequest();
        public MotionBinding[] Animations={new MotionBinding("Idle","idle",true),new MotionBinding("Run","run",true),new MotionBinding("Walk","walk",true),new MotionBinding("Dodge","dodge",false),new MotionBinding("Attack","attack",false),new MotionBinding("Attack01","attack01",false),new MotionBinding("Attack02","attack02",false),new MotionBinding("Attack03","attack03",false),new MotionBinding("Attack04","attack04",false),new MotionBinding("Slash","slash",false),new MotionBinding("Slam","slam",false),new MotionBinding("Shot","shot",false),new MotionBinding("Barrage","barrage",false),new MotionBinding("Death","death",false),new MotionBinding("Entrance","entrance",false),new MotionBinding("Follow","fly",true),new MotionBinding("Return","fly",true),new MotionBinding("ComboWindup","anticipation",false),new MotionBinding("ComboDash","dash",false),new MotionBinding("LinkAttack","link",false)};
        public Vector3 VisualOffset;
        public string CurrentMotion {get;private set;}
        Vector3 customScale;Player player;Raven raven;Enemy enemy;string last;long lastSequence=-1;
        void Awake(){player=GetComponent<Player>();raven=GetComponent<Raven>();enemy=GetComponent<Enemy>();if(CustomVisual)customScale=CustomVisual.localScale;}
        void LateUpdate()
        {
            if(!SpriteFrames)return;var g=Dungeon.I;
            string state="Idle";float speed=1;
            if(player){state=player.ActiveSkill>0?new[]{"","Slash","Slam","Shot","Barrage"}[player.ActiveSkill]:player.BasicBusy?"Attack"+player.BasicStage.ToString("D2"):player.Dodging?"Dodge":player.Locomotion.State==LocomotionState.Moving?"Run":"Idle";speed=player.Locomotion.State==LocomotionState.Moving?player.Locomotion.Speed/5.6f:1;}
            else if(raven)state=raven.CurrentState.ToString();else if(enemy)state=enemy.Dead?"Death":enemy.Telegraphing?"Attack":"Run";
            if(g&&g.Cinematics&&g.Cinematics.IsPlaying&&(player||enemy&&g.IsBossWave))state=g.Cinematics.Kind==CinematicKind.PlayerDeath?"Death":"Entrance";
            CurrentMotion=state;
            // TEMP(spine-run-only) 시작 ── 전체 스파인 교체 후 이 블록을 지우고 아래 두 줄의 showCustom을 UseCustomVisual로 바꾼다.
            // 방향키를 바꿔 누르는 순간처럼 입력이 한두 프레임 비면 이동 상태가 잠깐 Settling(멈추는 중, 0.12초)이 된다.
            // 그때마다 스프라이트로 바뀌면 깜빡여 보이므로, Settling 동안에는 계속 달리기로 취급한다.
            if(SpineRunOnly&&player&&state=="Idle"&&player.Locomotion.State==LocomotionState.Settling)state="Run";
            bool showCustom=UseCustomVisual&&(!SpineRunOnly||state=="Run");
            // TEMP(spine-run-only) 끝
            if(SpriteCard)SpriteCard.OverrideHidden=showCustom;
            if(!CustomVisual)return;CustomVisual.gameObject.SetActive(showCustom);if(!showCustom)return;
            CustomVisual.position=PaperWorld.Point(SpriteFrames.transform.position)+VisualOffset;
            CustomVisual.rotation=Quaternion.Euler(PaperWorld.Pitch,0,0)*Quaternion.Euler(0,0,SpriteFrames.transform.eulerAngles.z);
            var scale=customScale;scale.x*=SpriteFrames.flipX?-1:1;CustomVisual.localScale=scale;
            var sort=CustomVisual.GetComponent<SortingGroup>();if(sort&&g){sort.sortAtRoot=true;sort.sortingOrder=WorldDepth.Order(transform.position.y-g.RoomCenter.y-.35f)*10+12;}
            float playback=g&&(g.Running||g.Cinematics.IsPlaying&&!g.Cinematics.IsPaused)?speed:0;
            if(Animator)Animator.speed=playback;if(AnimationDriver)AnimationDriver.SetPlaybackSpeed(playback);
            long sequence=player?player.PresentationSequence:0;if(last==state&&lastSequence==sequence)return;last=state;lastSequence=sequence;
            foreach(var binding in Animations)if(binding.State==state)
            {if(Animator&&!string.IsNullOrEmpty(binding.Animation)&&Animator.HasState(0,UnityEngine.Animator.StringToHash(binding.Animation)))Animator.Play(binding.Animation,0,0);if(AnimationDriver)AnimationDriver.Play(binding.Animation,binding.Loop,playback);AnimationRequested.Invoke(binding.Animation,binding.Loop,playback);break;}
        }
    }
}
