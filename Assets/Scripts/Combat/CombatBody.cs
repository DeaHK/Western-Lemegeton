using UnityEngine;

namespace WesternLemegeton
{
    // 이동 좌표와 분리된 표시 공간의 총구/피격 영역입니다. 별도 Rigidbody는 필요 없습니다.
    [DisallowMultipleComponent]
    public sealed class CombatBody : MonoBehaviour
    {
        [Tooltip("표시용 외형의 총구 Transform. 비워두면 몸통 중심과 Muzzle Offset을 사용합니다.")]
        public Transform MuzzlePoint;
        [Tooltip("표시 공간의 피격 중심. 비워두면 PaperCard 스프라이트에서 자동 계산합니다.")]
        public Transform BodyAnchor;
        public Vector3 BodySize = new Vector3(.8f, 1.4f, .5f);
        [Tooltip("자동 피격 영역의 스프라이트 대비 가로/세로 비율")]
        public Vector2 SpriteFraction = new Vector2(.7f, .8f);
        [Min(.01f)] public float Depth = .5f;
        public Vector3 MuzzleOffset;

        public static CombatBody For(Component actor)
        {
            var body = actor.GetComponent<CombatBody>();
            return body ? body : actor.gameObject.AddComponent<CombatBody>();
        }

        public void Geometry(out Vector3 center, out Vector3 half, out Quaternion rotation)
        {
            if (BodyAnchor)
            {
                center = BodyAnchor.position; rotation = BodyAnchor.rotation;
                half = Vector3.Scale(BodySize, Abs(BodyAnchor.lossyScale)) * .5f;
                half = Vector3.Max(half, Vector3.one * .01f);
                return;
            }
            var enemy = GetComponent<Enemy>();
            if (enemy && enemy.AuxiliaryTarget)
            {
                // 밧줄은 지면에 표시되므로 지면의 작은 피격 영역을 사용합니다.
                center = PaperWorld.Point(transform.position, .05f); half = Vector3.one * .3f;
                rotation = Quaternion.identity; return;
            }
            var player = GetComponent<Player>();
            SpriteRenderer source = player ? player.Presentation : enemy ? (enemy.Presentation ? enemy.Presentation : enemy.AuthoredSprite) : null;
            var presentation = GetComponent<ActorPresentation>();
            if (presentation && presentation.UseCustomVisual && presentation.CustomVisual)
            {
                // 커스텀 외형에는 BodyAnchor를 지정하면 정확한 영역을 직접 조절할 수 있습니다.
                center = presentation.CustomVisual.position;
                rotation = presentation.CustomVisual.rotation;
                half = Vector3.Max(Vector3.Scale(BodySize, Abs(presentation.CustomVisual.lossyScale)) * .5f, Vector3.one * .01f);
                return;
            }
            if (source && source.sprite)
            {
                var card = source.GetComponent<PaperCard>();
                rotation = Quaternion.Euler(card && card.Flat ? 90 : PaperWorld.Pitch, 0, 0) *
                    Quaternion.Euler(0, 0, source.transform.eulerAngles.z);
                Vector3 scale = source.transform.lossyScale;
                // PaperCard.LateUpdate와 동일한 변환을 직접 계산하여 전 프레임 표시를 참조하지 않습니다.
                Vector3 localCenter = source.sprite.bounds.center;
                if (source.flipX) localCenter.x = -localCenter.x;
                if (source.flipY) localCenter.y = -localCenter.y;
                center = PaperWorld.Point(source.transform.position, card && card.Flat ? .015f : 0) +
                    (card ? card.RenderOffset : Vector3.zero) + rotation * Vector3.Scale(localCenter, new Vector3(scale.x, scale.y, 1));
                Vector3 size = Vector3.Scale(source.sprite.bounds.size, Abs(scale));
                half = new Vector3(Mathf.Max(.05f, size.x * SpriteFraction.x * .5f),
                    Mathf.Max(.05f, size.y * SpriteFraction.y * .5f), Mathf.Max(.01f, Depth) * .5f);
                return;
            }
            center = PaperWorld.Point(transform.position, .9f); half = BodySize * .5f; rotation = Quaternion.identity;
        }
        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        public Vector3 Center { get { Geometry(out var center, out _, out _); return center; } }
        public Vector3 Muzzle => MuzzlePoint ? MuzzlePoint.position : Center + MuzzleOffset;
        public bool Cast(Ray ray, float length, float radius, out float distance)
        {
            Geometry(out var center, out var half, out var rotation);
            Quaternion inverse = Quaternion.Inverse(rotation);
            return ProjectileSpace.RayBox(new Ray(inverse * (ray.origin-center), inverse * ray.direction),
                -half - Vector3.one * radius, half + Vector3.one * radius, length, out distance);
        }
        void OnDrawGizmosSelected()
        {
            Geometry(out var center, out var half, out var rotation);
            var before = Gizmos.matrix; Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
            Gizmos.color = Color.cyan; Gizmos.DrawWireCube(Vector3.zero, half * 2); Gizmos.matrix = before;
            Gizmos.color = Color.yellow; Gizmos.DrawSphere(Muzzle, .06f);
        }
    }
}
