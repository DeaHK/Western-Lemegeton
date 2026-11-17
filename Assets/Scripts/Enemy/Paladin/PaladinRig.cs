using UnityEngine;

namespace WesternLemegeton
{
    // The blade is attached at the painted grip. Body poses change without stretching the weapon.
    [DefaultExecutionOrder(240)]
    internal sealed class PaladinRig : MonoBehaviour
    {
        internal enum Pose { Guard, Call, Windup, Thrust, Slash, Miracle, Revealed }
        static readonly Vector2[] GripPixels = {
            new Vector2(-99,202),new Vector2(-95,201),new Vector2(-164,252),new Vector2(190,276),
            new Vector2(119,146),new Vector2(-129,256),new Vector2(-159,352)
        };
        static readonly float[] BladeAngle = { -135,-135,133,-90,-130,-154,-118 };
        static readonly Sprite[] frames = new Sprite[7];
        SpriteRenderer body, blade, glow;
        public SpriteRenderer Body => body;
        Transform grip;
        public float Height { get; private set; }
        float clock, heightOffset, slide, roll, swordTurn, revealPulse;
        bool flip;
        Color tint;
        Pose pose;
        Vector2 look=Vector2.right;
        public Vector2 Ground;
        public bool Visible = true;
        public Vector3 CastingHandOffset => Quaternion.Euler(PaperWorld.Pitch,0,0) *
            (new Vector3(flip ? -174 : 174,414,0)*(Height/458f)) +
            PaperWorld.Point(Vector2.down*.35f);

        public static PaladinRig Create(Transform parent, float bodyHeight, Color color)
        {
            var obj = new GameObject("Paladin pose rig");
            obj.transform.SetParent(parent,false);
            var rig = obj.AddComponent<PaladinRig>();
            rig.Height = bodyHeight;
            rig.tint = color;
            rig.body = rig.Layer("Ink character",obj.transform);
            rig.grip = new GameObject("Sword hand").transform;
            rig.grip.SetParent(obj.transform,false);
            rig.glow = rig.Layer("Blade radiance",rig.grip);
            rig.blade = rig.Layer("One handed sword",rig.grip);
            rig.blade.sprite = rig.glow.sprite = Resources.Load<Sprite>("Paladin/Sword");
            for (int i=0;i<frames.Length;i++)
                if (!frames[i]) frames[i] = Resources.Load<Sprite>("Paladin/"+(Pose)i);
            rig.Set(Pose.Guard,Vector2.right);
            return rig;
        }

        SpriteRenderer Layer(string label, Transform parent)
        {
            var layer = new GameObject(label).AddComponent<SpriteRenderer>();
            layer.transform.SetParent(parent,false);
            return layer;
        }

        public void Set(Pose next, Vector2 facing)
        {
            pose = next;
            look=PaladinArea.Unit(facing);
            if (Mathf.Abs(facing.x)>.025f) flip = facing.x<0;
            body.sprite = frames[(int)next];
            body.flipX = flip;
            Shift();
        }

        public void Motion(float lift=0, float lean=0, float offset=0, float turn=0)
        {
            heightOffset=lift; roll=lean; slide=offset; swordTurn=turn;
        }
        public void Tick(float dt) => clock+=dt;
        public void Ignite(float power) => revealPulse=power;

        void Shift()
        {
            bool bare = pose == Pose.Revealed;
            // 458 source pixels from helmet to soles; revealed pose uses scalp to soles without horns.
            float unitsPerPixel = Height / (bare ? 718 : 458);
            body.transform.localScale = Vector3.one * (100 * unitsPerPixel);
            Vector2 hand = GripPixels[(int)pose] * unitsPerPixel;
            if (flip) hand.x = -hand.x;
            grip.localPosition = hand;
            float angle=BladeAngle[(int)pose]*(flip?-1:1);
            if (pose==Pose.Thrust || pose==Pose.Slash)
                angle=Mathf.Atan2(look.y*Mathf.Sin(PaperWorld.Pitch*Mathf.Deg2Rad),look.x)*Mathf.Rad2Deg-90;
            grip.localRotation = Quaternion.Euler(0,0,angle+swordTurn*(flip?-1:1));
            // The cropped sword has 349 blade pixels, guard to tip. Belt to soles is 272 in the guard pose.
            float leg = bare ? 427 * unitsPerPixel : 272 * unitsPerPixel;
            blade.transform.localScale = Vector3.one * (leg * 100 / 349);
            glow.transform.localScale = Vector3.Scale(blade.transform.localScale,new Vector3(1.22f,1.015f,1));
        }

        void LateUpdate()
        {
            body.enabled = blade.enabled = Visible;
            glow.enabled = Visible && (revealPulse>0 || pose == Pose.Revealed);
            if (!Visible) return;
            Shift();
            float breath = Mathf.Sin(clock*3.1f)*.025f;
            var rotation = Quaternion.Euler(PaperWorld.Pitch,0,0);
            transform.SetPositionAndRotation(PaperWorld.Point(Ground+Vector2.down*.35f) +
                rotation*new Vector3(slide*(flip?-1:1),heightOffset+breath,0),
                rotation*Quaternion.Euler(0,0,roll*(flip?-1:1)));
            int order = WorldDepth.Order(Ground.y-Rules.RoomOrigin.y-.35f)*10+15;
            body.sortingOrder=order;
            blade.sortingOrder=order+2;
            glow.sortingOrder=order+1;
            body.color=tint;
            blade.color=pose==Pose.Revealed ? new Color(.85f,.49f,1,tint.a) : tint;
            glow.color=pose==Pose.Revealed ? new Color(.77f,.22f,1,.85f) : new Color(1,.79f,.24f,.65f*revealPulse);
        }
    }
}

