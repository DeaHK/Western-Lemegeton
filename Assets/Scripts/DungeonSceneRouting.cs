using UnityEngine;

namespace WesternLemegeton
{
    public partial class Dungeon
    {
        [HideInInspector] public bool ManagedSession;
        bool ChangingScene => ManagedSession && GameManager.I && GameManager.I.IsLoading;

        internal void BindContent(SceneContent content)
        {
            Scene.Hirva = content.Town;
            Scene.Rooms = content.Rooms;
            Scene.EnemyContainer = content.EnemyContainer;
            actors = content.EnemyContainer;
            NpcDialogue.Rebind();
            IsTown = content.Space == GameSpace.Lobby;
            Hero.gameObject.SetActive(content.Space != GameSpace.MainMenu);
            Crow.gameObject.SetActive(content.Space != GameSpace.MainMenu);
            Rules.RoomOrigin = Vector2.zero;
            Obstacles = SolidObstacles = System.Array.Empty<Rect>();
        }

        internal void ReleaseContent()
        {
            Cinematics.Cancel();
            NpcDialogue?.Close();
            hud.CloseSettings();
            hud.CloseStigmaManagement();
            hud.StigmaDebugOpen = false;
            GeneratedMapCloseRequested?.Invoke();
            DestroyTrackedEnemies();
            ClearTransient();
            ResetEntrance();
            GeneratedStageReleased?.Invoke();
            ClearEnvironment();
            Hero.ReleaseTrialBind();
            Scene.Hirva = null;
            Scene.Rooms = System.Array.Empty<RoomAuthoring>();
            Scene.EnemyContainer = null;
            actors = null;
            IsTown = false;
            State = RunState.Title;
            RunMapInputBlocked = false;
        }

        // 마을 관문과 같은 방식으로 생성형 원정을 시작합니다. 시작하지 못하면 false입니다.
        internal bool RequestGeneratedRun()
        {
            if (GeneratedRunStartRequested != null && GeneratedRunStartRequested()) return true;
            GeneratedMapCloseRequested?.Invoke();
            return false;
        }

        internal void ShowMainMenu()
        {
            ResetRun();
            State = RunState.Title;
            IsTown = false;
            Hero.gameObject.SetActive(false);
            Crow.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (I != this) return;
            I = null;
            Obstacles = SolidObstacles = System.Array.Empty<Rect>();
            Rules.RoomOrigin = Vector2.zero;
        }
    }
}
