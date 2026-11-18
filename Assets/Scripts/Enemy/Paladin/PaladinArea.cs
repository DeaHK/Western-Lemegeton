using UnityEngine;

namespace WesternLemegeton
{
    // Logical XY footprints are also the parameters of the floor decal.
    internal struct PaladinArea
    {
        internal static readonly Vector2 ArenaHalfSize = new Vector2(22.5f,13.5f);
        public Vector2 Center, Direction, Size;
        public float Inner, Outer, HalfAngle;
        public bool Rectangular;

        public static Vector2 Unit(Vector2 direction) =>
            direction.sqrMagnitude < .00001f ? Vector2.right : direction.normalized;

        public static Vector2 Turn(Vector2 direction, float degrees) =>
            Quaternion.Euler(0, 0, degrees) * direction;

        public static PaladinArea Band(Vector2 center, float inner, float outer,
            Vector2 direction = default, float angle = 360)
        {
            return new PaladinArea { Center = center, Direction = Unit(direction), Inner = inner,
                Outer = outer, Size = Vector2.one * outer * 2, HalfAngle = angle / 2 };
        }

        public static PaladinArea Lane(Vector2 start, Vector2 end, float width)
        {
            Vector2 delta = end - start;
            return new PaladinArea { Center = (start + end) * .5f, Direction = Unit(delta),
                Size = new Vector2(Mathf.Max(.01f, delta.magnitude), width), Rectangular = true };
        }

        public bool Contains(Vector2 point)
        {
            Vector2 d = point - Center;
            if (Rectangular)
                return Mathf.Abs(Vector2.Dot(d, Direction)) <= Size.x * .5f &&
                    Mathf.Abs(Vector2.Dot(d, Vector2.Perpendicular(Direction))) <= Size.y * .5f;
            float r = d.magnitude;
            return r >= Inner && r <= Outer && (r < .0001f || HalfAngle >= 180 ||
                Vector2.Dot(d / r, Direction) >= Mathf.Cos(HalfAngle * Mathf.Deg2Rad)-.00001f);
        }

        public static float ToWall(Vector2 point, Vector2 direction, Vector2 center)
        {
            Vector2 local = point - center;
            float x = Mathf.Abs(direction.x) < .0001f ? float.PositiveInfinity :
                (Mathf.Sign(direction.x) * ArenaHalfSize.x - local.x) / direction.x;
            float y = Mathf.Abs(direction.y) < .0001f ? float.PositiveInfinity :
                (Mathf.Sign(direction.y) * ArenaHalfSize.y - local.y) / direction.y;
            return Mathf.Max(.01f, Mathf.Min(x, y));
        }

        // Sweep the curved luminous front between frames so fast projectiles cannot skip a hunter.
        public static bool WaveSwept(Vector2 point,Vector2 start,Vector2 direction,float step,float width,float scale=1)
        {
            Vector2 delta=point-start;
            float side=2*Vector2.Dot(delta,Vector2.Perpendicular(direction))/(width*scale);
            if (Mathf.Abs(side)>1) return false;
            float bow=(.23f-.58f*side*side)*scale;
            float thickness=(.035f+.10f*(1-side*side))*scale;
            float forward=Vector2.Dot(delta,direction);
            return forward>=bow-thickness && forward<=step+bow+thickness;
        }
    }

    internal sealed class PaladinDecal : MonoBehaviour
    {
        static Material shared;
        static Mesh quad;
        MeshRenderer surface;
        MaterialPropertyBlock properties;
        public void MarkEchoPreview()
        {
            properties.SetFloat("_EchoPreview",1);
            surface.SetPropertyBlock(properties);
        }
        public void Electrify()
        {
            properties.SetFloat("_ElectricField",1);
            surface.SetPropertyBlock(properties);
        }
        public void MarkRicochet()
        {
            properties.SetFloat("_Ricochet",1);
            surface.SetPropertyBlock(properties);
        }
        public void PaintWave(Vector2 p,Vector2 direction,float width,Vector2 center,float scale)
        {
            Vector2 half=direction*(.6f*scale);
            Paint(PaladinArea.Lane(p-half,p+half,width*scale),Color.white,12500,center);
            properties.SetVector("_Shape",new Vector4(2,0,0,0));
            surface.SetPropertyBlock(properties);
        }
        public void PaintSpotlights(Vector2 hunter,Vector2 judge,bool showJudge,float opacity)
        {
            properties.SetVector("_Shape",new Vector4(3,0,0,0));
            properties.SetVector("_Lights",new Vector4(hunter.x,hunter.y+.85f,judge.x,judge.y+1.65f));
            properties.SetVector("_LightRadii",new Vector4(2.65f,showJudge ? 3.45f : 0,0,0));
            properties.SetColor("_Tint",new Color(.025f,.016f,.052f,.975f*opacity));
            surface.SetPropertyBlock(properties);
        }
        public void PaintBeam(PaladinArea area,Vector2 center,float t)
        {
            Paint(area,Color.white,3100,center);
            properties.SetFloat("_DeathRay",1);
            properties.SetFloat("_Pulse",t);
            surface.SetPropertyBlock(properties);
        }
        public void Paint(PaladinArea area, Color tint, int order, Vector2 room, bool clip = true, float feather = 0)
        {
            if (!surface)
            {
                if (!shared) shared = new Material(Resources.Load<Shader>("Paladin/SanctuaryFloor"));
                if (!quad) quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                gameObject.AddComponent<MeshFilter>().sharedMesh = quad;
                surface = gameObject.AddComponent<MeshRenderer>();
                surface.sharedMaterial = shared;
                properties = new MaterialPropertyBlock();
            }
            transform.position = PaperWorld.Point(area.Center, .04f);
            float angle = Mathf.Atan2(area.Direction.y, area.Direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 0, angle);
            transform.localScale = new Vector3(area.Size.x, area.Size.y, 1);
            surface.sortingOrder = order;
            properties.SetColor("_Tint", tint);
            properties.SetVector("_Shape", new Vector4(area.Rectangular ? 1 : 0,
                area.Inner / Mathf.Max(.01f,area.Outer), Mathf.Cos(area.HalfAngle * Mathf.Deg2Rad), feather));
            Vector2 extent=clip ? PaladinArea.ArenaHalfSize : Vector2.one*10000;
            properties.SetVector("_Room", new Vector4(room.x,room.y,extent.x,extent.y));
            var wall=clip && Dungeon.I ? Dungeon.I.EchoBarrier : null;
            properties.SetVector("_EchoCourt", new Vector4(wall ? wall.Radius : 0,PaladinEchoBarrier.Spine,0,0));
            surface.SetPropertyBlock(properties);
        }
    }
}

