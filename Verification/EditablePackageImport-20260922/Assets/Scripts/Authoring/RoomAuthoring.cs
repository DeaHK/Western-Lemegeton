using System.Collections.Generic;
using UnityEngine;
namespace WesternLemegeton
{
    public sealed class RoomAuthoring:MonoBehaviour
    {
        public int RoomIndex;public bool Town;
        public Transform InitialSpawn,ServicePoint,TownExit;
        public RoomDoor[] Doors;
        public Transform[] EnemySpawnPoints;
        [Min(.1f)]public float SpawnRadius=2;
        public Vector2 Center=>new Vector2(transform.position.x,transform.position.z);
        public static Vector2 Point(Transform t)=>new Vector2(t.position.x,t.position.z);
        public WesternEnvironment.Layout Capture()
        {
            var layout=new WesternEnvironment.Layout{Root=transform,Town=Town,Authored=this};
            foreach(var f in GetComponentsInChildren<WorldFootprint>(true))
            {
                if(!f.enabled||!f.gameObject.activeSelf)continue;var r=f.Bounds;r.position-=Center;layout.Obstacles.Add(r);if(f.BlocksDodge)layout.Buildings.Add(r);
            }
            return layout;
        }
        public ExplorationRoom Bind(){var r=new ExplorationRoom{Index=RoomIndex,Layout=Capture()};r.Doors.AddRange(Doors);return r;}
        void OnDrawGizmosSelected(){Gizmos.color=new Color(.1f,.8f,1,.5f);Gizmos.DrawWireCube(transform.position,new Vector3(30,.1f,18));}
    }
}
