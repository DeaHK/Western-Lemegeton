using UnityEditor;
using UnityEngine;

namespace WesternLemegeton.EditorTools
{
    public static class EnemyPairSetup
    {
        public const string BullPath = "Assets/Editable/Actors/HauntedBull.prefab";
        public const string CowboyPath = "Assets/Editable/Actors/Cowboy.prefab";
        // 두 전용 프리팹을 준비하되 이미 저장된 프리팹과 Inspector 설정은 덮어쓰지 않습니다.
        [MenuItem("Western Lemegeton/Monsters/Create HauntedBull and Cowboy Prefabs")]
        public static void Create()
        {
            CreateOne(BullPath, true);
            CreateOne(CowboyPath, false);
            AssetDatabase.SaveAssets();
        }
        // 기존 몬스터의 외형을 복사하고 체력·이동·공격 부품을 Cowboy 또는 HauntedBull 구성으로 교체합니다.
        static void CreateOne(string path, bool bull)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path)) return;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editable/Actors/Enemy_" + (bull ? "1" : "2") + ".prefab");
            if (!source) throw new System.InvalidOperationException("Source prefab is missing.");
            var root = Object.Instantiate(source);
            try
            {
                root.name = bull ? "HauntedBull" : "Cowboy";
                foreach (var attack in root.GetComponents<EnemyAttackModule>()) Object.DestroyImmediate(attack);
                foreach (var move in root.GetComponents<EnemyMovementModule>()) Object.DestroyImmediate(move);
                if (!root.GetComponent<EnemyHealth>()) root.AddComponent<EnemyHealth>();
                if (!root.GetComponent<EnemyBasicBrain>()) root.AddComponent<EnemyBasicBrain>();
                var health = root.GetComponent<EnemyHealth>();
                health.BaseHealth = bull ? 120 : 65; health.HealthPerRoom = 13;
                root.GetComponent<Enemy>().Kind = bull ? 1 : 2;
                if (bull)
                {
                    root.AddComponent<HauntedBullAttack>();
                    root.AddComponent<EnemyChaseMovement>().StopDistance = 4.5f;
                }
                else
                {
                    root.AddComponent<CowboyAttack>();
                    // 기존 정지 거리와 추적 동작을 보존하고, 스킬 감속은 Brain에서 처리합니다.
                    var movement = root.AddComponent<EnemyKeepDistanceMovement>();
                    movement.UseAttackRange = false;
                    movement.ApproachRange = 6;
                    movement.ApproachRangeFactor = 1;
                    movement.RetreatDistance = 0;
                    movement.SeparationStrength = 0;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}

