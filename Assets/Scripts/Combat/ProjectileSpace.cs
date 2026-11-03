using UnityEngine;

namespace WesternLemegeton
{
    public static class ProjectileSpace
    {
        public static Vector2 Ground(Vector3 world) => new Vector2(world.x, world.z);
        public static Vector3 Direction(Vector2 direction) => new Vector3(direction.x, 0, direction.y);

        // 선분과 상자의 교차 구간을 구합니다. 시작점이 안에 있으면 거리 0으로 처리합니다.
        public static bool RayBox(Ray ray, Vector3 min, Vector3 max, float length, out float distance)
        {
            float enter = 0, exit = length;
            for (int axis = 0; axis < 3; axis++)
            {
                float d = ray.direction[axis], p = ray.origin[axis];
                if (Mathf.Abs(d) < .000001f) { if (p < min[axis] || p > max[axis]) { distance = 0; return false; } continue; }
                float a = (min[axis]-p)/d, b = (max[axis]-p)/d;
                enter = Mathf.Max(enter, Mathf.Min(a,b)); exit = Mathf.Min(exit, Mathf.Max(a,b));
                if (enter > exit) { distance = 0; return false; }
            }
            distance = enter; return exit >= 0;
        }

        public static Vector3 Aim(Dungeon game, Vector3 screen)
        {
            var muzzle = CombatBody.For(game.Hero).Muzzle;
            Ray ray = game.Cam.ScreenPointToRay(screen);
            float nearest = float.PositiveInfinity;
            Vector3 target = muzzle + Direction(game.Hero.Facing) * 20;
            // 카메라의 선은 목표 선택용입니다. 실제 엄폐물 검사는 총구에서 시작합니다.
            foreach (var enemy in game.Enemies)
                if (enemy && !enemy.Dead && enemy.gameObject.activeInHierarchy &&
                    CombatBody.For(enemy).Cast(ray, 1000, 0, out float t) && t < nearest)
                { nearest = t; target = ray.GetPoint(t); }
            if (!float.IsPositiveInfinity(nearest)) return target;
            var plane = new Plane(Vector3.up, new Vector3(0, muzzle.y, 0));
            if (plane.Raycast(ray, out float distance)) return ray.GetPoint(distance);
            return target;
        }

        // 기존 맵에는 장애물 높이 정보가 없으므로 기존처럼 그 지면 영역은 모든 높이에서 탄환을 막습니다.
        public static float WallDistance(Ray ray, float length, float radius)
        {
            float nearest = length;
            foreach (Rect r in Dungeon.Obstacles)
                if (RayBox(ray, new Vector3(r.xMin-radius,-10000,r.yMin-radius),
                    new Vector3(r.xMax+radius,10000,r.yMax+radius), length, out float t)) nearest = Mathf.Min(nearest, t);
            // 방 경계 및 지면도 탄환을 막습니다.
            Vector2 room = Rules.RoomOrigin;
            if (ray.origin.x < room.x-14.8f || ray.origin.x > room.x+14.8f ||
                ray.origin.z < room.y-8.8f || ray.origin.z > room.y+8.8f || ray.origin.y < 0) return 0;
            if (ray.direction.x > 0) nearest = Mathf.Min(nearest, (room.x+14.8f-ray.origin.x)/ray.direction.x);
            if (ray.direction.x < 0) nearest = Mathf.Min(nearest, (room.x-14.8f-ray.origin.x)/ray.direction.x);
            if (ray.direction.z > 0) nearest = Mathf.Min(nearest, (room.y+8.8f-ray.origin.z)/ray.direction.z);
            if (ray.direction.z < 0) nearest = Mathf.Min(nearest, (room.y-8.8f-ray.origin.z)/ray.direction.z);
            if (ray.direction.y < 0) nearest = Mathf.Min(nearest, -ray.origin.y/ray.direction.y);
            return Mathf.Max(0, nearest);
        }

        public static bool Sweep(Dungeon game, Vector3 from, Vector3 to, bool hostile, float radius,
            out Vector3 point, out Enemy enemy, out bool hunter)
        {
            enemy = null; hunter = false;
            Vector3 delta = to-from; float length = delta.magnitude;
            Ray ray = new Ray(from, length > .000001f ? delta/length : Vector3.right);
            float nearest = WallDistance(ray, length, radius);
            bool hit = nearest < length;
            if (hostile)
            {
                if (game.Hero && game.Hero.Hp > 0 && CombatBody.For(game.Hero).Cast(ray, length, radius, out float t) &&
                    (t < nearest || (!hit && t <= nearest)))
                { nearest = t; hit = hunter = true; }
            }
            else
            {
                foreach (var candidate in game.Enemies)
                    if (candidate && !candidate.Dead && candidate.gameObject.activeInHierarchy &&
                        CombatBody.For(candidate).Cast(ray, length, radius, out float t) &&
                        (t < nearest || (!hit && t <= nearest)))
                    { nearest = t; enemy = candidate; hit = true; }
            }
            point = hit ? ray.GetPoint(nearest) : to;
            return hit;
        }
    }
}
