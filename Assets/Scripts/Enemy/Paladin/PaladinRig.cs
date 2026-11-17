using UnityEngine;

namespace WesternLemegeton
{
    // The blade is attached at the painted grip. Body poses change without stretching the weapon.
    [DefaultExecutionOrder(240)]
    internal sealed class PaladinRig : MonoBehaviour
    {
        internal enum Pose { Guard, Call, Windup, Thrust, Slash, Miracle, Revealed }
        internal enum Action { Ready, Coil, Sweep, Lunge, Palm, Punch, Kick, Catch, Leap, Slam, Recoil, Kneel }
        static readonly Sprite[] actions = new Sprite[12];
        // Glove anchors in source pixels, relative to each cel's foot pivot.
        static readonly Vector2[] hands = {
            new Vector2(14,246),new Vector2(-97,225),new Vector2(150,250),new Vector2(164,220),
            new Vector2(-65,196),new Vector2(143,238),new Vector2(28,280),new Vector2(88,73),
            new Vector2(118,158),new Vector2(-22,22),new Vector2(125,218),new Vector2(69,133)
        };
        int action=-1;
        internal bool Armed=true;
        internal void SetOpacity(float alpha) => tint.a=alpha;
        internal Color BladeTint=new Color(.86f,.47f,1);
        internal float Lift => heightOffset;
        internal void Act(Action value,Vector2 direction)
        {
            action=(int)value;
            if (!actions[action]) actions[action]=Resources.Load<Sprite>("Paladin/Unjudged/"+value);
            body.sprite=actions[action];
            look=PaladinArea.Unit(direction);
            if (Mathf.Abs(look.x)>.025f) flip=look.x<0;
            body.flipX=flip;
            Shift();
        }
        static readonly Vector2[] GripPixels = {
            new Vector2(-99,202),new Vector2(-95,201),new Vector2(-164,252),new Vector2(190,276),
            new Vector2(119,146),new Vector2(-129,256),new Vector2(-159,352)
        };
        static readonly float[] BladeAngle = { -135,-135,133,-90,-130,-154,-118 };
        static readonly Sprite[] frames = new Sprite[7];
        SpriteRenderer body, blade, glow;
        public SpriteRenderer Body => body;
        internal SpriteRenderer HeldBlade => blade;
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
            action=-1;
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
            bool bare = action>=0 || pose == Pose.Revealed;
            // 458 source pixels from helmet to soles; revealed pose uses scalp to soles without horns.
            float unitsPerPixel = Height / (action>=0 ? 327 : bare ? 718 : 458);
            body.transform.localScale = Vector3.one * (100 * unitsPerPixel);
            Vector2 hand = (action>=0 ? hands[action] : GripPixels[(int)pose]) * unitsPerPixel;
            if (flip) hand.x = -hand.x;
            grip.localPosition = hand;
            float angle=BladeAngle[(int)pose]*(flip?-1:1);
            if (action>=0) angle=(action==(int)Action.Coil ? 52 : 24)*(flip?-1:1);
            if (action==(int)Action.Sweep || action==(int)Action.Lunge || (action<0 && (pose==Pose.Thrust || pose==Pose.Slash)))
                angle=Mathf.Atan2(look.y*Mathf.Sin(PaperWorld.Pitch*Mathf.Deg2Rad),look.x)*Mathf.Rad2Deg-90;
            grip.localRotation = Quaternion.Euler(0,0,angle+swordTurn*(flip?-1:1));
            // The cropped sword has 349 blade pixels, guard to tip. Belt to soles is 272 in the guard pose.
            float leg = action>=0 ? Height*.595f : bare ? 427 * unitsPerPixel : 272 * unitsPerPixel;
            blade.transform.localScale = Vector3.one * (leg * 100 / 349);
            glow.transform.localScale = Vector3.Scale(blade.transform.localScale,new Vector3(1.22f,1.015f,1));
        }

        void LateUpdate()
        {
            body.enabled = Visible;
            blade.enabled = Visible && Armed;
            glow.enabled = blade.enabled && (revealPulse>0 || pose == Pose.Revealed || action>=0);
            if (!Visible) return;
            Shift();
            float breath = Mathf.Sin(clock*3.1f)*.025f;
            var rotation = Quaternion.Euler(PaperWorld.Pitch,0,0);
            transform.SetPositionAndRotation(PaperWorld.Point(Ground+Vector2.down*.35f) +
                rotation*new Vector3(slide*(flip?-1:1),heightOffset+breath,0),
                rotation*Quaternion.Euler(0,0,roll*(flip?-1:1)));
            int order = WorldDepth.Order(Ground.y-Rules.RoomOrigin.y-.35f)*10+15;
            body.sortingOrder=order;
            // The drawn glove covers the middle of the hilt when the unarmored judge grips it.
            blade.sortingOrder=action>=0 ? order-1 : order+2;
            glow.sortingOrder=blade.sortingOrder-1;
            body.color=tint;
            blade.color=action>=0 ? new Color(BladeTint.r,BladeTint.g,BladeTint.b,tint.a) : pose==Pose.Revealed ? new Color(.85f,.49f,1,tint.a) : tint;
            glow.color=action>=0 ? new Color(BladeTint.r,BladeTint.g,BladeTint.b,tint.a*.75f) : pose==Pose.Revealed ? new Color(.77f,.22f,1,.85f) : new Color(1,.79f,.24f,.65f*revealPulse);
        }
    }
}

