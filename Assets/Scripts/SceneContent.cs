using UnityEngine;
using UnityEngine.SceneManagement;

namespace WesternLemegeton
{
    public enum GameSpace { MainMenu, Lobby, Game, Debug }

    // 콘텐츠 씬(MainMenu/Lobby/Game/Debug)마다 하나씩 두는 연결표입니다.
    // GameManager가 이 컴포넌트를 읽어 공용 세션(Dungeon)에 마을·방·적 보관함을 연결합니다.
    public sealed class SceneContent : MonoBehaviour
    {
        public GameSpace Space;
        public RoomAuthoring Town;
        public RoomAuthoring[] Rooms = System.Array.Empty<RoomAuthoring>();
        public Transform EnemyContainer;

        // 어느 씬에서 Play를 눌러도 공용 세션이 없으면 여기서 한 번만 만듭니다.
        void Awake()
        {
            if (!GameManager.I) GameManager.Bootstrap(this);
        }

        public void Validate()
        {
            if (Space == GameSpace.Lobby && (!Town || !Town.InitialSpawn || !Town.TownExit))
                throw new System.InvalidOperationException("Lobby requires a town and arrival/exit anchors.");
            if (Space == GameSpace.Game)
            {
                if (Rooms == null || Rooms.Length != 6) throw new System.InvalidOperationException("Game requires six authored room templates.");
                foreach (var room in Rooms)
                    if (!room || !room.InitialSpawn) throw new System.InvalidOperationException("Game room binding is incomplete.");
            }
            if (!EnemyContainer) throw new System.InvalidOperationException("Content enemy container is missing.");
        }

        public static SceneContent Find(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var content = root.GetComponentInChildren<SceneContent>(true);
                if (content) return content;
            }
            return null;
        }
    }

    public static class SceneOwnership
    {
        // 실행 중에 부모 없이 만든 보조 오브젝트는 "활성 씬"에 생깁니다.
        // 주인과 같은 씬으로 옮겨 두면, 공용 세션의 보조물은 씬을 바꿔도 남고
        // 콘텐츠의 보조물은 그 씬과 함께 사라집니다.
        public static void Follow(GameObject created, GameObject owner)
        {
            if (!created || !owner || created.transform.parent) return;
            var scene = owner.scene;
            if (!scene.IsValid() || created.scene == scene) return;
            if (scene.buildIndex == -1 && scene.name == "DontDestroyOnLoad") Object.DontDestroyOnLoad(created);
            else if (scene.isLoaded) SceneManager.MoveGameObjectToScene(created, scene);
        }
    }
}
