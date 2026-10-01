using UnityEngine;
namespace WesternLemegeton
{
    public sealed class WorldFootprint:MonoBehaviour
    {
        public Vector2 Offset,Size=Vector2.one;
        public bool BlocksDodge;
        public Rect Bounds
        {
            get{var c=transform.TransformPoint(new Vector3(Offset.x,0,Offset.y));var s=transform.lossyScale;return new Rect(c.x-Mathf.Abs(Size.x*s.x)/2,c.z-Mathf.Abs(Size.y*s.z)/2,Mathf.Abs(Size.x*s.x),Mathf.Abs(Size.y*s.z));}
        }
        void OnDrawGizmosSelected(){var r=Bounds;Gizmos.color=BlocksDodge?new Color(1,.4f,.2f,.65f):new Color(1,.8f,.2f,.65f);Gizmos.DrawWireCube(new Vector3(r.center.x,.05f,r.center.y),new Vector3(r.width,.1f,r.height));}
    }
}
