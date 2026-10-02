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
        public Transform CustomVisual;
        public SpriteRenderer SpriteFrames;
        public PaperCard SpriteCard;
        public Animator Animator;
        public ActorAnimationDriver AnimationDriver;
        [Tooltip("Optional frame collection. Raven SpriteAnimationCollection.asset is a VisualCatalog asset; no additional animator component is required.")]
        public VisualCatalog SpriteAnimationCatalog;
        [SerializeField] SpriteAnimator spriteAnimator=new SpriteAnimator();
        public SpriteAnimator FramePlayer=>spriteAnimator;
        public AnimationRequest AnimationRequested=new AnimationRequest();
        public MotionBinding[] Animations={new MotionBinding("Idle","idle",true),new MotionBinding("Run","run",true),new MotionBinding("Walk","walk",true),new MotionBinding("Dodge","dodge",false),new MotionBinding("Attack","attack",false),new MotionBinding("Attack01","attack01",false),new MotionBinding("Attack02","attack02",false),new MotionBinding("Attack03","attack03",false),new MotionBinding("Attack04","attack04",false),new MotionBinding("Slash","slash",false),new MotionBinding("Slam","slam",false),new MotionBinding("Shot","shot",false),new MotionBinding("Barrage","barrage",false),new MotionBinding("Death","death",false),new MotionBinding("Entrance","entrance",false),new MotionBinding("Follow","fly",true),new MotionBinding("Return","fly",true),new MotionBinding("ComboWindup","anticipation",false),new MotionBinding("ComboDash","dash",false),new MotionBinding("LinkAttack","link",false)};
        public Vector3 VisualOffset;
        [Min(.01f)] public float VisualScale=1;
        public string CurrentMotion {get;private set;}
        Vector3 customScale,baseRenderOffset;Quaternion baseSpriteRotation;
        Player player;Raven raven;Enemy enemy;string last;long lastSequence=-1;bool lastCustom;
        PaperCard shadowCard;SpriteRenderer shadowSprite;
        void Awake()
        {
            player=GetComponent<Player>();raven=GetComponent<Raven>();enemy=GetComponent<Enemy>();
            if(CustomVisual)customScale=CustomVisual.localScale;
            if(SpriteFrames)baseSpriteRotation=SpriteFrames.transform.localRotation;
            if(SpriteCard)baseRenderOffset=SpriteCard.RenderOffset;
            foreach(var card in GetComponentsInChildren<PaperCard>(true))
                if(card.Source&&card.Source.name=="Shadow"){shadowCard=card;shadowSprite=card.Source;break;}
        }
        void LateUpdate()
        {
            if(!SpriteFrames)return;var g=Dungeon.I;
            string state="Idle";float speed=1;
            if(player){state=player.ActiveSkill>0?new[]{"","Slash","Slam","Shot","Barrage"}[player.ActiveSkill]:player.BasicBusy?"Attack"+player.BasicStage.ToString("D2"):player.Dodging?"Dodge":player.Locomotion.State==LocomotionState.Moving?"Run":"Idle";speed=player.Locomotion.State==LocomotionState.Moving?player.Locomotion.Speed/5.6f:1;}
            else if(raven)state=string.IsNullOrEmpty(raven.AnimationName)?"Idle":raven.AnimationName;else if(enemy)state=enemy.Dead?"Death":enemy.Telegraphing?"Attack":"Run";
            if(g&&g.Cinematics&&g.Cinematics.IsPlaying&&(player||enemy&&g.IsBossWave))state=g.Cinematics.Kind==CinematicKind.PlayerDeath?"Death":"Entrance";
            CurrentMotion=state;
            bool visible=!raven||raven.IsVisible;
            float playback=g&&(g.Running||g.Cinematics.IsPlaying&&!g.Cinematics.IsPaused)?speed:0;
            long sequence=player?player.PresentationSequence:raven?raven.PresentationSequence:0;
            bool requested=last!=state||lastSequence!=sequence||lastCustom!=UseCustomVisual;
            var collection=SpriteAnimationCatalog?SpriteAnimationCatalog.FrameAnimations:null;
            if(raven)
            {
                // Visibility belongs to presentation. The Raven root and its FSM keep running.
                SpriteFrames.enabled=visible;SpriteFrames.flipX=raven.FacingLeft;
                if(shadowSprite)shadowSprite.enabled=visible;
                if(collection!=null)ApplyRavenFrames(collection,state,sequence,visible?playback:0);
            }
            if(SpriteCard)SpriteCard.OverrideHidden=UseCustomVisual;
            if(CustomVisual)CustomVisual.gameObject.SetActive(UseCustomVisual&&visible);
            last=state;lastSequence=sequence;lastCustom=UseCustomVisual;
            if(!CustomVisual||!UseCustomVisual)return;
            CustomVisual.position=PaperWorld.Point(SpriteFrames.transform.position)+VisualOffset+(raven&&collection!=null?collection.VisualOffset:Vector3.zero);
            bool modern=WorldDepth.Modern(g?g.Cam:Camera.main);
            CustomVisual.rotation=modern?SpriteBillboard.Rotation(g?g.Cam:Camera.main,false,SpriteFrames.transform.eulerAngles.z):Quaternion.Euler(PaperWorld.Pitch,0,0)*Quaternion.Euler(0,0,SpriteFrames.transform.eulerAngles.z);
            var scale=customScale*VisualScale;scale.x*=SpriteFrames.flipX?-1:1;CustomVisual.localScale=scale;
            var sort=CustomVisual.GetComponent<SortingGroup>();if(sort&&g){sort.sortAtRoot=true;if(modern)sort.sortingLayerName=WorldDepth.Objects;sort.sortingOrder=WorldDepth.RenderOrder(transform.position.y-.35f,g.RoomCenter.y)+12;}
            if(!visible)playback=0;
            if(Animator)Animator.speed=playback;if(AnimationDriver)AnimationDriver.SetPlaybackSpeed(playback);
            if(!requested)return;
            string animation=raven?state:null;bool loop=raven&&(state=="Idle"||state=="Move");
            var frameClip=collection?.Get(state);if(frameClip!=null)loop=frameClip.Loop;
            if(Animations!=null)foreach(var binding in Animations)if(binding!=null&&binding.State==state){animation=binding.Animation;loop=binding.Loop;break;}
            if(string.IsNullOrEmpty(animation))return;
            if(Animator&&Animator.HasState(0,UnityEngine.Animator.StringToHash(animation)))Animator.Play(animation,0,0);
            if(AnimationDriver)AnimationDriver.Play(animation,loop,playback);
            AnimationRequested.Invoke(animation,loop,playback);
        }

        void ApplyRavenFrames(SpriteAnimationCollection collection,string state,long sequence,float playback)
        {
            if(shadowCard)shadowCard.ShadowGroundOffset=collection.ShadowGroundOffset;
            if(shadowSprite&&shadowSprite.sprite)
            {
                var bounds=shadowSprite.sprite.bounds.size;
                shadowSprite.transform.localScale=new Vector3(collection.Footprint.x/Mathf.Max(.001f,bounds.x),collection.Footprint.y/Mathf.Max(.001f,bounds.y),1);
            }
            if(UseCustomVisual)return;
            var clip=collection.Get(state);
            if(clip==null||!clip.IsValid)return;
            spriteAnimator.Play(clip,sequence);
            spriteAnimator.Tick(Time.deltaTime,playback);
            if(spriteAnimator.CurrentFrame)SpriteFrames.sprite=spriteAnimator.CurrentFrame;
            // One scale and ground anchor for the whole sheet, including wide attack poses.
            SpriteFrames.transform.localScale=Vector3.one*Mathf.Max(.001f,collection.Scale);
            SpriteFrames.transform.localRotation=baseSpriteRotation;
            if(SpriteCard)SpriteCard.RenderOffset=baseRenderOffset+VisualOffset+collection.VisualOffset;
        }
    }
}
