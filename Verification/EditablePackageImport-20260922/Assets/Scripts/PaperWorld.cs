using UnityEngine;
using UnityEngine.Rendering;

namespace WesternLemegeton
{
    // Gameplay stays in XY. Visible cards live in XZ with a perspective camera.
    // A separate render representation prevents 3D presentation from changing combat distances.
    [DefaultExecutionOrder(200)]
    public class PaperWorld:MonoBehaviour
    {
        public const int LogicLayer=30;
        public const float Pitch=47;
        public static Vector3 Point(Vector2 p,float height=0)=>new Vector3(p.x,height,p.y);
        public static void Attach(SpriteRenderer source,bool flat)
        {
            if(!Application.isPlaying)return;
            source.gameObject.layer=LogicLayer;
            var card=source.gameObject.AddComponent<PaperCard>();card.Source=source;card.Flat=flat;
        }
        public static Vector2 Mouse(Camera camera,Vector3 screen)
        {
            var ray=camera.ScreenPointToRay(screen);
            if(new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float distance)){var p=ray.GetPoint(distance);return new Vector2(p.x,p.z);}return Vector2.zero;
        }
        public static Vector2 Screen(Camera camera,Vector2 logical,float height=0)
        {var p=camera.WorldToScreenPoint(Point(logical,height));return new Vector2(p.x/UnityEngine.Screen.width*1280,(1-p.y/UnityEngine.Screen.height)*720);}
        public static void Configure(Camera camera)
        {
            camera.orthographic=false;camera.fieldOfView=38;camera.nearClipPlane=.1f;camera.farClipPlane=130;
            camera.rect=new Rect(0,0,1,1);camera.cullingMask=~(1<<LogicLayer);
            camera.backgroundColor=new Color(.08f,.065f,.10f);camera.transform.rotation=Quaternion.Euler(Pitch,0,0);
        }
        public static Vector3 CameraPosition(Vector2 focus)
        {
            var angle=Quaternion.Euler(Pitch,0,0);
            return Point(focus,1.5f)-angle*Vector3.forward*26;
        }
    }
    
}


