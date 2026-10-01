using UnityEngine;
using UnityEngine.Rendering;
namespace WesternLemegeton
{
 [ExecuteAlways]
 [DefaultExecutionOrder(300)]
 public class PaperCard:MonoBehaviour
    {
        public SpriteRenderer Source;public bool Flat;public Vector3 RenderOffset;
        public SpriteRenderer Visible;
        public bool OverrideHidden;
        SpriteRenderer visible;SortingGroup group;PropGrounding grounding;
        void Start()
        {
            visible=Visible;
            if(!visible&&Application.isPlaying){visible=new GameObject("Card / "+name,typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();Visible=visible;}
            if(!visible)return;
            group=GetComponentInParent<SortingGroup>();
            var landscape=GetComponent<LandscapeParallax>();
            string asset=Source.sprite?Source.sprite.name:"";
            bool scenery=asset.StartsWith("Wnn")||asset.StartsWith("OBJ")||asset.StartsWith("Deco")||asset.StartsWith("Fence");
            // Dust veils and skill effects are also cards, but must never cast prop shadows.
            if(Application.isPlaying&&scenery && !Flat && Source.sprite && Source.sprite.bounds.size.y*Mathf.Abs(transform.lossyScale.y)>.75f && !group && (!landscape || landscape.Factor<=0))
                grounding=new PropGrounding(Source);
        }
        void LateUpdate()
        {
            if(!visible)visible=Visible;if(!visible||!Source)return;
            visible.enabled=Source.enabled && Source.gameObject.activeInHierarchy&&!OverrideHidden;
            visible.sprite=Source.sprite;visible.flipX=Source.flipX;visible.flipY=Source.flipY;
            visible.sharedMaterial=Source.sharedMaterial;
            Color tint=Source.color;
            if(!group && !Flat)tint*=new Color(.94f,.85f,.80f,1);
            visible.color=tint;
            visible.sortingOrder=group?group.sortingOrder*10+Source.sortingOrder:Source.sortingOrder*10;
            var scale=transform.lossyScale;var parentScale=visible.transform.parent?visible.transform.parent.lossyScale:Vector3.one;
            visible.transform.localScale=new Vector3(scale.x/parentScale.x,scale.y/parentScale.y,1/parentScale.z);
            var renderGroup=visible.GetComponent<SortingGroup>();if(renderGroup){renderGroup.sortAtRoot=true;renderGroup.sortingOrder=visible.sortingOrder;}
            float height=Flat?.015f:0;
            visible.transform.position=PaperWorld.Point(transform.position,height)+RenderOffset;
            visible.transform.rotation=Quaternion.Euler(Flat?90:PaperWorld.Pitch,0,0)*Quaternion.Euler(0,0,transform.eulerAngles.z);
            grounding?.Update(visible,Dungeon.I?Dungeon.I.Cam:Camera.main);
        }
        void OnDisable(){if(visible)visible.enabled=false;grounding?.SetVisible(false);}
        void OnDestroy(){if(Application.isPlaying&&visible&&!visible.transform.IsChildOf(transform))Destroy(visible.gameObject);grounding?.Dispose();}
    }
}
