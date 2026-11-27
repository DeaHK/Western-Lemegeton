// TEMP(spine-run-only): 달리기 스파인을 Hunter 프리팹에 연결하는 일회용 설정 스크립트.
// 유니티가 스크립트를 다시 읽을 때 자동으로 한 번 실행되고, 이미 연결돼 있으면 아무것도 하지 않는다.
// 메뉴 Western Lemegeton > Authoring > Setup Hunter Spine Run (Temp) 로 다시 만들 수도 있다.
// 모든 동작이 스파인으로 교체되면 이 파일은 지워도 된다.
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Spine.Unity;

namespace WesternLemegeton.EditorTools
{
    [InitializeOnLoad]
    public static class SpineHunterRunSetup
    {
        const string Folder = "Assets/Editable/Actors/Hunter_Spine/Run";
        const string PrefabPath = "Assets/Editable/Actors/Hunter.prefab";
        const string ChildName = "Spine_Run_TEMP";
        const string Define = "WNN_SPINE";
        const string SpineAnimationName = "animation"; // 스파인 파일 안의 달리기 애니메이션 이름
        const float SourceHeightPixels = 2153f;        // 스파인 json 머리말의 height
        const float TargetHeight = 1.9f;               // 기존 스프라이트의 키(프리팹 로컬 단위)
        const string RetryKey = "SpineHunterRunSetup.ImportRetries";

        static SpineHunterRunSetup() { EditorApplication.delayCall += () => Run(false); }

        [MenuItem("Western Lemegeton/Authoring/Setup Hunter Spine Run (Temp)")]
        static void Menu() { SessionState.SetInt(RetryKey, 0); Run(true); }

        static void Log(string message) { Debug.Log("[SpineHunterRunSetup] " + message); }

        static void Run(bool rebuild)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;

            // 1) WNN_SPINE 기호: SpineAnimationDriver의 실제 스파인 코드가 이 기호가 있어야 컴파일된다.
            var target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string defines = PlayerSettings.GetScriptingDefineSymbols(target);
            if (System.Array.IndexOf(defines.Split(';'), Define) < 0)
            {
                PlayerSettings.SetScriptingDefineSymbols(target, string.IsNullOrEmpty(defines) ? Define : defines + ";" + Define);
                Log("Scripting Define Symbols에 " + Define + " 추가 (" + target.TargetName + "). 다시 컴파일된 뒤 이어서 진행한다.");
                return;
            }

            // 2) 스파인 데이터 에셋: 런타임이 json/atlas를 가져오면서 자동으로 만든다.
            var data = FindData();
            if (!data)
            {
                int tries = SessionState.GetInt(RetryKey, 0);
                if (tries >= 3) { Debug.LogError("[SpineHunterRunSetup] SkeletonDataAsset을 찾지 못했다: " + Folder); return; }
                SessionState.SetInt(RetryKey, tries + 1);
                Log("스파인 파일 다시 가져오기 시도 " + (tries + 1));
                AssetDatabase.ImportAsset(Folder, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
                EditorApplication.delayCall += () => Run(rebuild);
                return;
            }

            FixTextures(data);

            // 3) Hunter 프리팹 연결
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var presentation = root.GetComponent<ActorPresentation>();
                if (!presentation || !presentation.CustomVisual) { Debug.LogError("[SpineHunterRunSetup] ActorPresentation 또는 Custom Visual 슬롯이 없다."); return; }
                var existing = presentation.CustomVisual.Find(ChildName);
                if (existing && !rebuild) return; // 이미 연결됨
                if (existing) Object.DestroyImmediate(existing.gameObject);

                var go = new GameObject(ChildName);
                go.transform.SetParent(presentation.CustomVisual, false);
                SkeletonAnimation.AddToGameObject(go, data);
                var animation = go.GetComponent<SkeletonAnimation>();
                animation.AnimationName = SpineAnimationName;
                animation.loop = true;

                // 크기: 스파인 원본 키를 기존 스프라이트 키에 맞춘다. 발바닥은 스파인 원점(0)에 있다.
                // 스파인 원본은 왼쪽을 보고 달리고 기존 스프라이트는 오른쪽이 기본이라 X를 뒤집는다.
                float s = TargetHeight / (SourceHeightPixels * data.scale);
                go.transform.localScale = new Vector3(-s, s, 1);
                go.transform.localPosition = Vector3.zero;

                var driver = root.GetComponent<SpineAnimationDriver>();
                if (!driver) driver = root.AddComponent<SpineAnimationDriver>();
                var serializedDriver = new SerializedObject(driver);
                var skeletonProperty = serializedDriver.FindProperty("Skeleton");
                if (skeletonProperty == null) { Debug.LogError("[SpineHunterRunSetup] SpineAnimationDriver.Skeleton 칸이 없다. " + Define + " 기호가 적용됐는지 확인."); return; }
                skeletonProperty.objectReferenceValue = animation;
                serializedDriver.ApplyModifiedPropertiesWithoutUndo();

                presentation.UseCustomVisual = true;
                presentation.SpineRunOnly = true;
                presentation.AnimationDriver = driver;
                foreach (var binding in presentation.Animations) if (binding.State == "Run") binding.Animation = SpineAnimationName;

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Log("Hunter 프리팹 연결 완료. data=" + AssetDatabase.GetAssetPath(data) + " dataScale=" + data.scale + " childScale=" + s);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // 스파인 머티리얼에 그림(텍스처)이 비어 있으면 atlas 파일에 적힌 순서대로 png를 다시 연결한다.
        // 처음 가져올 때 png보다 atlas가 먼저 처리되면 머티리얼이 빈 채로 만들어져 캐릭터가 보이지 않는다.
        static void FixTextures(SkeletonDataAsset data)
        {
            bool changed = false;
            foreach (var atlasBase in data.atlasAssets)
            {
                var atlas = atlasBase as SpineAtlasAsset;
                if (!atlas || !atlas.atlasFile) continue;
                var pages = new System.Collections.Generic.List<string>();
                foreach (string line in atlas.atlasFile.text.Split('\n')) { string t = line.Trim(); if (t.EndsWith(".png")) pages.Add(t); }
                for (int i = 0; i < atlas.materials.Length && i < pages.Count; i++)
                {
                    string path = Folder + "/" + pages[i];
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer && (importer.textureShape != TextureImporterShape.Texture2D || importer.npotScale != TextureImporterNPOTScale.None || importer.alphaIsTransparency || importer.mipmapEnabled || importer.maxTextureSize < 4096 || importer.textureCompression != TextureImporterCompression.Uncompressed))
                    {
                        // 그림이 평면(2D)이 아닌 큐브맵으로 잘못 가져와져 있으면 스파인이 그림을 찾지 못한다.
                        importer.textureShape = TextureImporterShape.Texture2D;
                        importer.npotScale = TextureImporterNPOTScale.None; // 2048x3070 원본 크기 그대로 유지
                        // 스파인 권장 설정: 미리 곱한 알파(pma) 그림이라 알파 보정을 끄고, 원본 크기(세로 3070)를 유지한다.
                        importer.textureType = TextureImporterType.Default;
                        importer.alphaIsTransparency = false; importer.mipmapEnabled = false; importer.maxTextureSize = 4096;
                        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.wrapMode = TextureWrapMode.Clamp;
                        importer.SaveAndReimport();
                    }
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    var material = atlas.materials[i];
                    if (!texture || !material) { Debug.LogError("[SpineHunterRunSetup] 텍스처 또는 머티리얼 없음: " + path); continue; }
                    if (material.mainTexture != texture) { material.mainTexture = texture; EditorUtility.SetDirty(material); changed = true; Log("머티리얼에 텍스처 연결: " + pages[i]); }
                }
                if (changed) atlas.Clear();
            }
            if (changed) { data.Clear(); AssetDatabase.SaveAssets(); }
        }

        static SkeletonDataAsset FindData()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:SkeletonDataAsset", new[] { Folder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset) return asset;
            }
            return null;
        }
    }
}
