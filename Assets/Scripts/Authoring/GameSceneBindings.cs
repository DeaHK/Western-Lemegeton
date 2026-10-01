using UnityEngine;
namespace WesternLemegeton
{
    public sealed class GameSceneBindings:MonoBehaviour
    {
        public RoomAuthoring Hirva;
        public RoomAuthoring[] Rooms;
        public Player Hunter;public Raven Raven;public Camera GameCamera;
        public Transform EnemyContainer;
        public Enemy[] EnemyPrefabs;
        public GameUIView UI;
    }
}
