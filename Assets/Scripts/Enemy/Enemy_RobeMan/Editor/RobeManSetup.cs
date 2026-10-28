using UnityEditor;
using UnityEngine;

namespace WesternLemegeton.EditorTools
{
    public static class RobeManSetup
    {
        public const string PrefabPath = "Assets/Editable/Actors/RobeMan.prefab";

        // 기존 프리팹이 없을 때만 공통 외형을 복사해 전용 공격·추적 이동 부품을 조립하고 저장합니다.
        [MenuItem("Western Lemegeton/Monsters/Create RobeMan Prefab")]
        public static void CreatePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
            {
                Debug.Log("RobeMan prefab already exists; existing settings were preserved.");
                return;
            }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editable/Actors/Enemy_0.prefab");
            if (!source) throw new System.InvalidOperationException("Enemy_0 prefab is required.");
            // 외형과 체력 바 연결은 복사하고 공격 부품만 RobeMan 전용으로 교체합니다.
            var root = Object.Instantiate(source);
            try
            {
                root.name = "RobeMan";
                foreach (var attack in root.GetComponents<EnemyAttackModule>()) Object.DestroyImmediate(attack);
                root.AddComponent<RobeManAttack>();
                if (!root.GetComponent<EnemyHealth>()) root.AddComponent<EnemyHealth>();
                if (!root.GetComponent<EnemyBasicBrain>()) root.AddComponent<EnemyBasicBrain>();
                foreach (var movement in root.GetComponents<EnemyMovementModule>()) Object.DestroyImmediate(movement);
                root.AddComponent<EnemyChaseMovement>();
                root.GetComponent<Enemy>().Kind = 0;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("ROBEMAN_PREFAB_CREATED: " + PrefabPath);
            }
            // 저장 성공 여부와 관계없이 작업용 복제 오브젝트를 정리합니다.
            finally { Object.DestroyImmediate(root); }
        }
    }
}
