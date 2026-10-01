using UnityEngine;
using UnityEngine.Rendering;
namespace WesternLemegeton
{
 public class SceneryOccluder : MonoBehaviour
    {
        public float Width, Height;
        SpriteRenderer sprite;
        void Awake() { sprite=GetComponent<SpriteRenderer>(); }
        void LateUpdate()
        {
            var game=Dungeon.I;if (!game || !game.Hero) return;
            Vector2 delta=game.Pos-(Vector2)transform.position;
            bool behind=delta.y>.15f && delta.y<Height && Mathf.Abs(delta.x)<Width*.44f;
            float opacity=behind?.32f:1;
            if(!behind)foreach(var enemy in game.Enemies)
            {
                if(!enemy||enemy.Dead)continue;Vector2 e=(Vector2)enemy.transform.position-(Vector2)transform.position;
                if(e.y>.15f&&e.y<Height&&Mathf.Abs(e.x)<Width*.44f){opacity=.55f;break;}
            }
            Color color=sprite.color;color.a=Mathf.MoveTowards(color.a,opacity,Time.unscaledDeltaTime*4);sprite.color=color;
        }
    }
}
