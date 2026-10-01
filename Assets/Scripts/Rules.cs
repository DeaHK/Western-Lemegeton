using UnityEngine;

namespace WesternLemegeton
{
    // Runtime rules shared by the game and the editor smoke checks.
    public static class Rules
    {
        public static Vector2 RoomOrigin;
        public static int AmmoCost(int combo) => combo % 2 == 0 ? 1 : 2;
        public static Vector2 ClampToRoom(Vector2 p, float radius = .4f) => new Vector2(Mathf.Clamp(p.x, RoomOrigin.x-15 + radius, RoomOrigin.x+15 - radius), Mathf.Clamp(p.y, RoomOrigin.y-9 + radius, RoomOrigin.y+9 - radius));
        public static Vector2 CameraTarget(Vector2 p, float halfHeight, float aspect)
        {
            float x = Mathf.Max(0, 15.8f - halfHeight * aspect);
            float y = Mathf.Max(0, 9.8f - halfHeight);
            return new Vector2(Mathf.Clamp(p.x, -x, x), Mathf.Clamp(p.y, -y, y));
        }
        public static bool InCone(Vector2 origin, Vector2 direction, Vector2 target, float range, float dot)
        {
            Vector2 delta = target - origin;
            return delta.sqrMagnitude <= range * range && (delta.sqrMagnitude < .01f || Vector2.Dot(direction.normalized, delta.normalized) >= dot);
        }
        public static Vector2 ResolveObstacles(Vector2 old, Vector2 desired, float radius)
        { return Resolve(old,desired,radius,Dungeon.Obstacles); }
        public static Vector2 ResolveDash(Vector2 old, Vector2 desired, float radius)
        {
            Vector2 delta=desired-old;
            int steps=Mathf.Max(1,Mathf.CeilToInt(delta.magnitude/.15f));
            Vector2 step=delta/steps;
            for(int i=0;i<steps;i++)old=Resolve(old,old+step,radius,Dungeon.SolidObstacles);
            return old;
        }
        static Vector2 Resolve(Vector2 old, Vector2 desired, float radius, Rect[] obstacles)
        {
            desired = ClampToRoom(desired, radius);
            foreach (Rect r in obstacles)
            {
                Rect expanded = Rect.MinMaxRect(r.xMin-radius, r.yMin-radius, r.xMax+radius, r.yMax+radius);
                if (!expanded.Contains(desired)) continue;
                Vector2 xOnly = new Vector2(desired.x, old.y);
                Vector2 yOnly = new Vector2(old.x, desired.y);
                if (!expanded.Contains(xOnly)) desired = xOnly;
                else if (!expanded.Contains(yOnly)) desired = yOnly;
                else desired = old;
            }
            return desired;
        }
        public static Vector2 OutsideCover(Vector2 p, float radius)
        {
            foreach(Rect r in Dungeon.Obstacles)
            {
                Rect b=Rect.MinMaxRect(r.xMin-radius,r.yMin-radius,r.xMax+radius,r.yMax+radius);
                if(!b.Contains(p))continue;
                float left=p.x-b.xMin,right=b.xMax-p.x,bottom=p.y-b.yMin,top=b.yMax-p.y;
                float min=Mathf.Min(left,right,bottom,top);
                if(min==left)p.x=b.xMin-.01f;else if(min==right)p.x=b.xMax+.01f;else if(min==bottom)p.y=b.yMin-.01f;else p.y=b.yMax+.01f;
            }
            return ClampToRoom(p,radius);
        }
    }
}

