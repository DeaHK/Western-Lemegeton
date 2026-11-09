using UnityEngine;

namespace WesternLemegeton
{
    public sealed partial class LoganBossAttack
    {
        [Header("Dynamite avoidance")]
        public bool EnableDynamiteDodge = true;
        [Min(.1f)] public float DodgeDistance = 4;
        [Min(.01f)] public float DodgeSeconds = .4f;
        [Min(0)] public float DodgeHeight = .7f;
        [Min(0)] public float DodgeSafetyMargin = .2f;
        bool dodging;
        float dodgeElapsed, resumeHeight;
        Vector2 dodgeStart, dodgeTarget;
        ActionState resumeAction;
        public bool IsDodgingDynamite => dodging;
        public Vector2 DodgeTarget => dodgeTarget;

        bool TickDynamiteDodge(Dungeon game, float dt)
        {
            if(!dodging)
            {
                if(!EnableDynamiteDodge) return false;
                var volleys=Object.FindObjectsByType<LoganDynamiteVolley>(FindObjectsSortMode.None);
                float margin=Mathf.Max(0,BodyRadius)+Mathf.Max(0,DodgeSafetyMargin);
                Vector2 position=transform.position;
                if(!Dangerous(volleys,position,margin) || !FindDodgeLanding(volleys,game.Pos,margin,out var landing)) return false;
                dodging=true; dodgeElapsed=0; dodgeStart=position; dodgeTarget=landing;
                resumeAction=Action; resumeHeight=visualHeight; Action=ActionState.Dodge;
                // 이미 총 난사 도약 중이었다면 회피 착지를 그 도약의 완료로 취급합니다.
                // 이전 보간 좌표로 되돌아가는 것을 막고 이후 조준부터 이어갑니다.
                if(resumeAction==ActionState.Leap) { resumeAction=ActionState.Aim; resumeHeight=0; elapsed=0; }
                if(owner) owner.SetMeleeWarning(false,0);
            }
            // 패턴 타이머와 쿨타임은 회피 중 보존합니다. 회피는 별도 공격 패턴을 시작하지 않습니다.
            dodgeElapsed+=dt;
            float t=Mathf.Clamp01(dodgeElapsed/Mathf.Max(.01f,DodgeSeconds));
            Vector2 from=transform.position, next=Vector2.Lerp(dodgeStart,dodgeTarget,t);
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(from,next)/.1f));
            bool blocked=false;
            for(int i=1;i<=steps;i++)
            {
                Vector2 p=Vector2.Lerp(from,next,i/(float)steps);
                if(HauntedBullAttack.Blocked(p,BodyRadius)) { blocked=true; break; }
                transform.position=p;
            }
            SetHeight(Mathf.Lerp(resumeHeight,0,t)+4*Mathf.Max(0,DodgeHeight)*t*(1-t));
            if(t>=1 || blocked)
            {
                dodging=false; Action=resumeAction; SetHeight(resumeHeight);
            }
            return true;
        }
        static bool Dangerous(LoganDynamiteVolley[] volleys,Vector2 p,float margin)
        {
            foreach(var volley in volleys) if(volley && volley.Threatens(p,margin)) return true;
            return false;
        }
        bool FindDodgeLanding(LoganDynamiteVolley[] volleys,Vector2 hunter,float margin,out Vector2 target)
        {
            Vector2 start=transform.position;
            target=start; bool found=false; float best=float.NegativeInfinity;
            int steps=Mathf.Max(1,Mathf.CeilToInt(Mathf.Max(.1f,DodgeDistance)/.1f));
            for(int a=0;a<72;a++)
            {
                float angle=a*Mathf.PI/36;
                Vector2 direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                for(int i=1;i<=steps;i++)
                {
                    Vector2 p=start+direction*(Mathf.Max(.1f,DodgeDistance)*i/steps);
                    if(HauntedBullAttack.Blocked(p,BodyRadius)) break;
                    if(Dangerous(volleys,p,margin)) continue;
                    float score=(p-hunter).sqrMagnitude;
                    if(!found || score>best) { found=true; best=score; target=p; }
                }
            }
            return found;
        }
        void CancelDodge()
        {
            if(!dodging) return;
            dodging=false; Action=resumeAction; SetHeight(resumeHeight);
        }
    }
}
