using UnityEngine;
using UnityEngine.Rendering;
namespace WesternLemegeton
{
 public class WorldDepth : MonoBehaviour
    {
        SortingGroup group;
        public static int Order(float feetY) => -Mathf.RoundToInt(feetY*30);
        void Awake() { group=GetComponent<SortingGroup>(); if (!group) group=gameObject.AddComponent<SortingGroup>(); }
        void LateUpdate() { group.sortingOrder=Order(transform.position.y-Rules.RoomOrigin.y-.35f); }
    }
}
