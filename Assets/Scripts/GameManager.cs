using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WesternLemegeton
{
    // 씬 전환의 유일한 소유자입니다.
    // 헌터·까마귀·인장·성흔·UI는 GameSession(파괴되지 않는 루트)에 그대로 남고,
    // 마을·방·적 같은 콘텐츠만 씬 단위로 교체합니다.
    [DefaultExecutionOrder(-1000)]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }
        public Dungeon Session;

        // 전환 요청부터 새 공간이 플레이 가능해질 때까지 true입니다. 오류 화면이 떠 있는 동안에도 true입니다.
        public bool IsLoading { get; private set; }
        // 진입 콜백(EnterTown 등)을 실행하는 동안 true입니다. 이때 Route는 전환을 다시 시작하지 않습니다.
        public bool ApplyingEntry { get; private set; }
        public SceneContent Content { get; private set; }
        public string LastError { get; private set; }
        public bool InGameScene => Content && Content.Space == GameSpace.Game && LastError == null;

        Canvas overlay;
        Text status, secondLabel;
        Image progress;
        Button retry, second;

        bool transitioning;   // Begin/Transition 코루틴이 도는 중
        bool holding;         // 씬은 열렸지만 생성형 던전의 방을 만드는 중이라 로딩 화면을 유지
        bool contentIntact;   // 실패했지만 이전 공간이 그대로 살아 있음(취소 가능)
        GameSpace failedSpace;
        Action failedEntry, retryAction, preparationRetry;

        public static void Bootstrap(SceneContent initial)
        {
            var prefab = Resources.Load<GameObject>("Session/GameSession");
            if (!prefab) throw new InvalidOperationException("Session/GameSession prefab is missing. Run Western Lemegeton > Scenes > Create Separated Scenes.");
            var instance = Instantiate(prefab).GetComponent<GameManager>();
            instance.StartCoroutine(instance.Begin(initial));
        }

        void Awake()
        {
            if (I && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
            BuildOverlay();
        }

        // 처음 실행(빌드의 MainMenu 또는 에디터에서 아무 씬이나 직접 Play)할 때의 진입입니다.
        IEnumerator Begin(SceneContent initial)
        {
            transitioning = true;
            IsLoading = true;
            ShowProgress(.1f, "게임 준비 중");
            yield return null;
            string error = null;
            try
            {
                initial.Validate();
                Content = initial;
                SceneManager.SetActiveScene(initial.gameObject.scene);
                Session.BindContent(initial);
                ApplyingEntry = true;
                EnterDefault(initial.Space);
            }
            catch (Exception ex) { error = ex.Message; Debug.LogException(ex); }
            finally { ApplyingEntry = false; }
            transitioning = false;
            if (error != null) { Fail(initial.Space, () => EnterDefault(initial.Space), error, false); yield break; }
            ShowProgress(1, "준비 완료");
            yield return null;
            if (holding || LastError != null) yield break;
            IsLoading = false;
            overlay.gameObject.SetActive(false);
        }

        // 다른 공간으로 가야 하면 전환을 시작하고 true를 돌려줍니다.
        // 이미 그 공간이거나 진입 콜백 안이라면 false를 돌려주어 호출한 쪽이 원래 동작을 이어갑니다.
        public bool Route(GameSpace destination, Action entry)
        {
            if (ApplyingEntry) return false;
            if (IsLoading) return true;
            if (Content && Content.Space == destination) return false;
            StartCoroutine(Transition(destination, entry));
            return true;
        }

        public void ReturnToMenu()
        {
            Route(GameSpace.MainMenu, Session.ShowMainMenu);
        }

        // 생성형 던전이 방을 만들기 전에 Game 씬을 준비합니다.
        // 코루틴은 GameManager가 소유하므로 던전 빌더가 재시작돼도 로딩이 중간에 끊기지 않습니다.
        public Coroutine PrepareGameScene(Action retryEntry)
        {
            preparationRetry = retryEntry;
            return StartCoroutine(PrepareRoutine());
        }

        IEnumerator PrepareRoutine()
        {
            // 진입 콜백 안에서 요청된 경우(Game 씬 직접 Play)는 이미 Game 씬이므로 기다리지 않고 화면만 유지합니다.
            if (ApplyingEntry && Content && Content.Space == GameSpace.Game) { holding = true; yield break; }
            while (transitioning) yield return null;
            if (LastError != null) yield break;
            if (Content && Content.Space == GameSpace.Game)
            {
                // 같은 Game 씬 안의 다음 스테이지는 로딩 화면 없이 진행합니다.
                // 로딩 화면이 이미 떠 있다면(직접 Play, 실패 후 재시도) 방이 준비될 때까지 유지합니다.
                if (IsLoading) holding = true;
                yield break;
            }
            holding = true;
            transitioning = true;
            IsLoading = true;
            ShowProgress(0, "공간 불러오는 중");
            yield return Transition(GameSpace.Game, null);
        }

        public void FinishPreparation()
        {
            if (!holding) return;
            holding = false;
            if (LastError != null || transitioning) return;
            IsLoading = false;
            overlay.gameObject.SetActive(false);
        }

        // 던전 빌더가 방 생성에 실패했을 때 호출합니다. 오류 화면을 이 매니저가 맡았으면 true입니다.
        public bool FailPreparation(string message, Action retryEntry)
        {
            if (LastError != null) return true;
            if (!holding) return false;
            holding = false;
            failedSpace = GameSpace.Game;
            failedEntry = null;
            Fail(GameSpace.Game, null, message, false, retryEntry);
            return true;
        }

        void EnterDefault(GameSpace space)
        {
            switch (space)
            {
                case GameSpace.MainMenu: Session.ShowMainMenu(); break;
                case GameSpace.Lobby: Session.EnterTown(); break;
                // Game 씬을 직접 Play해도 실제 게임과 같은 생성형 원정으로 시작합니다.
                case GameSpace.Game: if (!Session.RequestGeneratedRun()) Session.StartRun(); break;
                case GameSpace.Debug: Session.OpenTrialGallery(); break;
            }
        }

        IEnumerator Transition(GameSpace destination, Action entry)
        {
            transitioning = true;
            IsLoading = true;
            LastError = null;
            Time.timeScale = 1;
            ShowProgress(0, "공간 불러오는 중");
            yield return null;

            string path = "Assets/Scenes/" + destination + ".unity";
            var known = new HashSet<int>();
            for (int i = 0; i < SceneManager.sceneCount; i++) known.Add(SceneManager.GetSceneAt(i).handle);
            AsyncOperation load = null;
            string error = null;
            try { load = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive); }
            catch (Exception ex) { error = ex.Message; }
            if (load == null)
            {
                transitioning = false;
                Fail(destination, entry, error ?? "씬을 불러올 수 없습니다: " + path, true);
                yield break;
            }
            while (!load.isDone)
            {
                ShowProgress(load.progress * .8f, "공간 불러오는 중");
                yield return null;
            }

            // 이번 요청으로 새로 열린 씬만 고릅니다. 같은 씬을 재시도로 한 번 더 여는 경우도 구분됩니다.
            Scene scene = default;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var candidate = SceneManager.GetSceneAt(i);
                if (!known.Contains(candidate.handle) && candidate.path == path) scene = candidate;
            }
            SceneContent next = null;
            try
            {
                if (!scene.IsValid()) throw new InvalidOperationException("Loaded scene was not found: " + path);
                next = SceneContent.Find(scene);
                if (!next) throw new InvalidOperationException("SceneContent binding is missing in " + path);
                next.Validate();
            }
            catch (Exception ex) { error = ex.Message; Debug.LogException(ex); }
            if (error != null)
            {
                if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
                transitioning = false;
                Fail(destination, entry, error, true);
                yield break;
            }

            ShowProgress(.85f, "공간 준비 중");
            var previous = Content;
            try
            {
                Session.ReleaseContent();
                SceneManager.SetActiveScene(scene);
                Content = next;
                Session.BindContent(next);
                ApplyingEntry = true;
                entry?.Invoke();
            }
            catch (Exception ex) { error = ex.Message; Debug.LogException(ex); }
            finally { ApplyingEntry = false; }
            if (previous && previous != next) yield return SceneManager.UnloadSceneAsync(previous.gameObject.scene);
            transitioning = false;
            if (error != null) { Fail(destination, entry, error, false); yield break; }
            ShowProgress(1, "준비 완료");
            // 한 프레임 동안 카드·카메라가 제자리를 잡은 뒤에 화면을 엽니다.
            yield return null;
            if (holding || LastError != null || transitioning) yield break;
            IsLoading = false;
            overlay.gameObject.SetActive(false);
        }

        void Fail(GameSpace destination, Action entry, string message, bool intact, Action customRetry = null)
        {
            Debug.LogError("Scene transition failed (" + destination + "): " + message, this);
            LastError = message;
            failedSpace = destination;
            failedEntry = entry;
            contentIntact = intact && Content;
            // 던전 준비 중의 실패는 "씬만 다시 열기"가 아니라 "방 만들기부터 다시"여야 합니다.
            retryAction = customRetry ?? (holding ? preparationRetry : null);
            holding = false;
            IsLoading = true;
            overlay.gameObject.SetActive(true);
            status.text = "불러오기를 완료하지 못했습니다\n" + message;
            secondLabel.text = contentIntact ? "취소" : "메인으로";
            retry.gameObject.SetActive(true);
            second.gameObject.SetActive(true);
        }

        void Retry()
        {
            if (transitioning || LastError == null) return;
            var action = retryAction;
            var space = failedSpace; var entry = failedEntry;
            LastError = null;
            ShowProgress(0, "다시 시도하는 중");
            if (action != null) action(); else StartCoroutine(Transition(space, entry));
            // 재시도가 아무 작업도 시작하지 못했다면 화면이 잠기지 않도록 오류 화면으로 되돌립니다.
            if (!transitioning && !holding && LastError == null)
            {
                if (InGameScene && Session.State != RunState.Title) { IsLoading = false; overlay.gameObject.SetActive(false); }
                else Fail(space, entry, "재시도를 시작하지 못했습니다.", contentIntact, action);
            }
        }

        void SecondChoice()
        {
            if (transitioning || LastError == null) return;
            if (contentIntact)
            {
                // 이전 공간이 그대로이므로 이동만 취소합니다.
                LastError = null;
                IsLoading = false;
                overlay.gameObject.SetActive(false);
                Session.Tell("이동을 취소했습니다");
                return;
            }
            LastError = null;
            StartCoroutine(Transition(GameSpace.MainMenu, Session.ShowMainMenu));
        }

        void ShowProgress(float value, string message)
        {
            overlay.gameObject.SetActive(true);
            retry.gameObject.SetActive(false);
            second.gameObject.SetActive(false);
            progress.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1);
            status.text = message + "  " + Mathf.RoundToInt(value * 100) + "%";
        }

        void BuildOverlay()
        {
            var panel = new GameObject("Scene Loading", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            panel.transform.SetParent(transform, false);
            overlay = panel.GetComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.sortingOrder = 32700;
            var scaler = panel.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            var shade = Widget<Image>("Background", panel.transform, Vector2.zero, Vector2.one);
            shade.color = new Color(.035f, .025f, .05f, 1);
            status = Widget<Text>("Status", panel.transform, new Vector2(.1f, .46f), new Vector2(.9f, .66f));
            status.font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 24);
            status.fontSize = 24; status.alignment = TextAnchor.MiddleCenter;
            status.color = new Color(.96f, .86f, .66f);
            var track = Widget<Image>("Progress Track", panel.transform, new Vector2(.27f, .40f), new Vector2(.73f, .415f));
            track.color = new Color(.18f, .15f, .16f);
            progress = Widget<Image>("Progress", track.transform, Vector2.zero, Vector2.one);
            progress.color = new Color(.79f, .59f, .30f);
            retry = LoadingButton("다시 시도", new Vector2(.29f, .25f), new Vector2(.48f, .33f), out _);
            second = LoadingButton("메인으로", new Vector2(.52f, .25f), new Vector2(.71f, .33f), out secondLabel);
            retry.onClick.AddListener(Retry);
            second.onClick.AddListener(SecondChoice);
            panel.SetActive(false);
        }

        Button LoadingButton(string label, Vector2 min, Vector2 max, out Text text)
        {
            var background = Widget<Image>(label, overlay.transform, min, max);
            background.color = new Color(.25f, .18f, .12f);
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            text = Widget<Text>("Label", background.transform, Vector2.zero, Vector2.one);
            text.font = status.font; text.fontSize = 20; text.text = label; text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(.96f, .86f, .66f); text.raycastTarget = false;
            return button;
        }

        static T Widget<T>(string name, Transform parent, Vector2 min, Vector2 max) where T : Graphic
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(T));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return obj.GetComponent<T>();
        }

        void OnDestroy() { if (I == this) I = null; }
    }
}
