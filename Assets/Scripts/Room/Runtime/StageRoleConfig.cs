using System.Collections.Generic;
using UnityEngine;

namespace WesternLemegeton
{
    // Keep ScriptableObject in a matching filename so Unity can persist a valid MonoScript reference.
    [CreateAssetMenu(fileName = "StageRoleConfig", menuName = "Western Lemegeton/Stage Role Config")]
    public class StageRoleConfig : ScriptableObject
    {
        public StageType Type = StageType.Basic;
        public List<RoleCount> Counts = new List<RoleCount>();
    }
}
