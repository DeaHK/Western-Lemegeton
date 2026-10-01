using System.Collections.Generic;

namespace WesternLemegeton
{
    // Each actual attack has an id. Two pellets from one stage cannot count as two combo hits.
    public sealed class ComboTracker
    {
        class Progress { public int next; public float start; public long lastAttack; }
        readonly Dictionary<int,Progress> targets=new Dictionary<int,Progress>();
        public bool Hit(int target,int stage,long attackId,float now)
        {
            targets.TryGetValue(target,out var p);
            if(p!=null&&p.lastAttack==attackId)return false;
            if(stage==1){targets[target]=new Progress{next=2,start=now,lastAttack=attackId};return false;}
            if(p==null||p.next!=stage||now-p.start>4){targets.Remove(target);return false;}
            p.lastAttack=attackId;p.next++;
            if(stage==4){targets.Remove(target);return true;}return false;
        }
        public void Clear()=>targets.Clear();
    }
}
