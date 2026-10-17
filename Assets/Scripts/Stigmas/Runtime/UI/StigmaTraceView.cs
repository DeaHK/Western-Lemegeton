using UnityEngine;

namespace WesternLemegeton.Stigmas
{
    // Presentation helper owned by Enemy; it does not own or mutate combat state.
    public sealed class StigmaTraceView
    {
        static Sprite left, right;
        SpriteRenderer player, raven;
        public void Show(Enemy enemy)
        {
            int marks = enemy.Stigma.Marks;
            if (!player && marks != 0)
            {
                if (!left) { left = Half(true); right = Half(false); }
                player = Ink.Shape("Player stigma trace", enemy.transform, new Vector2(0, 1.7f), Vector2.one * .45f, Ink.Gold, 1100);
                raven = Ink.Shape("Raven stigma trace", enemy.transform, new Vector2(0, 1.7f), Vector2.one * .45f, Ink.Cyan, 1100);
                player.sprite = left; raven.sprite = right;
            }
            // PaperCard mirrors renderer visibility into the XZ presentation.
            if (player) player.enabled = (marks & 1) != 0;
            if (raven) raven.enabled = (marks & 2) != 0;
        }
        static Sprite Half(bool isLeft)
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var pixels = new Color[1024];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(16,16));
                pixels[y * 32 + x] = new Color(1,1,1, ((x < 16) == isLeft && distance >= 10 && distance <= 15) ? 1 : 0);
            }
            texture.SetPixels(pixels); texture.Apply();
            return Sprite.Create(texture, new Rect(0,0,32,32), Vector2.one * .5f, 32);
        }
    }
}
