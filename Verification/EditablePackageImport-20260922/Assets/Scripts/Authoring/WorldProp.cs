using System.Collections.Generic;
using UnityEngine;
namespace WesternLemegeton
{
    [DefaultExecutionOrder(310)]
    public sealed class WorldProp:MonoBehaviour
    {
        public SpriteRenderer Visual;
        public bool Flat,FadeWhenOccluding;
        public float OcclusionWidth,OcclusionHeight;
        public Transform ContactRoot;
        public List<PropGrounding.Decal> Contacts=new List<PropGrounding.Decal>();
        PropGrounding grounding;float opacity=1;Color original;
        void Awake(){if(Visual)original=Visual.color;if(ContactRoot)grounding=new PropGrounding(ContactRoot,Contacts);}
        void LateUpdate()
        {
            var g=Dungeon.I;if(!g||!Visual)return;
            if(FadeWhenOccluding)
            {
                Vector2 delta=g.Pos-new Vector2(transform.position.x,transform.position.z);
                bool behind=delta.y>.15f&&delta.y<OcclusionHeight*transform.lossyScale.y&&Mathf.Abs(delta.x)<OcclusionWidth*Mathf.Abs(transform.lossyScale.x)*.44f;
                opacity=Mathf.MoveTowards(opacity,behind?.32f:1,Time.unscaledDeltaTime*4);var c=original;c.a*=opacity;Visual.color=c;
            }
            if(!Flat)Visual.sortingOrder=WorldDepth.Order(transform.position.z-g.RoomCenter.y)*10;
            grounding?.Update(Visual,g.Cam);
        }
        void OnDisable(){grounding?.SetVisible(false);}
    }
}
