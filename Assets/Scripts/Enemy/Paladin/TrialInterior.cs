using UnityEngine;

namespace WesternLemegeton
{
    // Both faces sample the same painting through a fixed reference camera. Their
    // shared edge stays registered while the actual gameplay camera follows the player.
    public sealed class TrialInterior : MonoBehaviour
    {
        Mesh surface;
        Material paint;
        bool judicatorCourt;
        public float Dusk { get; private set; }

        public void Build(Vector2 center, bool gallery, Vector2 extent, bool parallel)
        {
            judicatorCourt = parallel;
            float rear = extent.y + .6f;
            var room = new GameObject("Painted floor and rear wall", typeof(MeshFilter), typeof(MeshRenderer));
            room.transform.SetParent(transform, false);
            room.transform.position = PaperWorld.Point(center);
            surface = new Mesh { name = "Joined sanctuary surfaces" };
            surface.vertices = new[] {
                new Vector3(-90,0,-50), new Vector3(90,0,-50),
                new Vector3(-90,0,rear), new Vector3(90,0,rear),
                new Vector3(-90,45,rear), new Vector3(90,45,rear)
            };
            surface.triangles = new[] { 0,2,1, 1,2,3, 2,4,3, 3,4,5 };
            surface.RecalculateBounds();
            room.GetComponent<MeshFilter>().sharedMesh = surface;

            paint = new Material(Resources.Load<Shader>("Paladin/TrialInterior"));
            var day = Resources.Load<Sprite>(gallery ? "Paladin/Gallery" : "Paladin/Sanctuary").texture;
            paint.mainTexture = day;
            paint.SetTexture("_DuskTex", gallery ? day : Resources.Load<Sprite>("Paladin/SanctuaryDusk").texture);
            var reference = Quaternion.Euler(PaperWorld.Pitch, 0, 0);
            paint.SetVector("_Eye", PaperWorld.CameraPosition(Vector2.zero));
            paint.SetVector("_Up", reference * Vector3.up);
            paint.SetVector("_Forward", reference * Vector3.forward);
            paint.SetFloat("_VerticalOffset", gallery ? -.103f : 0);
            paint.SetVector("_Extent", new Vector4(extent.x,extent.y,0,0));
            if (parallel)
            {
                // Register the painted wall foot at the rear of the enlarged court.
                float height = 32f;
                float width = height * day.width / day.height;
                paint.SetVector("_ParallelMap", new Vector4(1/width, 1/height,
                    .70f - rear*Mathf.Sin(PaperWorld.Pitch*Mathf.Deg2Rad)/height, 1));
            }
            var renderer = room.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = paint;
            renderer.sortingOrder = -22000;
        }

        void LateUpdate()
        {
            if (!judicatorCourt || !Dungeon.I || !Dungeon.I.Hero) return;
            Vector2 hunter=Dungeon.I.LocalPos;
            var wall=Dungeon.I.EchoBarrier;
            paint.SetVector("_HunterCourt",new Vector4(hunter.x,hunter.y,1,0));
            paint.SetVector("_EchoCourt",new Vector4(wall ? wall.Radius : 0,PaladinEchoBarrier.Spine,wall ? wall.Closure : 0,0));
        }

        public void SetDusk(float amount)
        {
            Dusk = Mathf.Clamp01(amount);
            paint.SetFloat("_Dusk", Dusk);
        }

        void OnDestroy()
        {
            if (surface) Destroy(surface);
            if (paint) Destroy(paint);
        }
    }
}
