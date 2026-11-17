using UnityEngine;

namespace WesternLemegeton
{
    internal sealed class PaladinEchoBarrier : MonoBehaviour
    {
        internal const float Spine = 1.9f;
        const int ArcSegments = 36;
        const int Pairs = (ArcSegments + 1) * 2;
        readonly Vector3[] vertices = new Vector3[Pairs * 2];
        Mesh veil;
        Material light;
        Vector2 center;
        public float Closure { get; private set; }
        public float Radius { get; private set; }

        internal void Build(Transform parent, Vector2 origin)
        {
            center = origin;
            transform.SetParent(parent, false);
            transform.position = PaperWorld.Point(origin);
            veil = new Mesh { name = "Echo court light curtain" };
            veil.MarkDynamic();
            var triangles = new int[Pairs * 6];
            for (int i=0;i<Pairs;i++)
            {
                int a=2*i, b=2*((i+1)%Pairs);
                int n=i*6;
                triangles[n]=a; triangles[n+1]=b; triangles[n+2]=a+1;
                triangles[n+3]=a+1; triangles[n+4]=b; triangles[n+5]=b+1;
            }
            light = new Material(Resources.Load<Shader>("Paladin/EchoCurtain"));
            SetClosure(0);
            veil.triangles = triangles;
            gameObject.AddComponent<MeshFilter>().sharedMesh = veil;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = light;
            renderer.sortingOrder = -4100;
        }

        public Vector2 Clamp(Vector2 point, float clearance)
        {
            var axis = center + Vector2.up*Mathf.Clamp(point.y-center.y,-Spine,Spine);
            return axis + Vector2.ClampMagnitude(point-axis, Mathf.Max(0,Radius-clearance));
        }

        internal void SetClosure(float fraction)
        {
            Closure = Mathf.Clamp01(fraction);
            Radius = Mathf.Lerp(PaladinArea.ArenaHalfSize.magnitude+1,2.9f,Closure);
            for (int i=0;i<Pairs;i++)
            {
                bool top = i<=ArcSegments;
                float angle=(i%(ArcSegments+1))*Mathf.PI/ArcSegments+(top?0:Mathf.PI);
                Vector3 foot=new Vector3(Mathf.Cos(angle)*Radius,.04f,Mathf.Sin(angle)*Radius+(top?Spine:-Spine));
                vertices[i*2]=foot;
                vertices[i*2+1]=foot+Vector3.up*1.55f;
            }
            veil.vertices=vertices;
            veil.RecalculateBounds();
            light.SetFloat("_Strength",Mathf.SmoothStep(0,1,Closure*3));
        }

        void OnDestroy()
        {
            if (veil) Destroy(veil);
            if (light) Destroy(light);
        }
    }
}
