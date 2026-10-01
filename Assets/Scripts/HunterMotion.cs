using UnityEngine;

namespace WesternLemegeton
{
    public enum MotionPhase { Idle, Anticipation, Action, Hit, Recovery }

    // Animation and combat read the same timing contract. Times are seconds, never frame counts.
    public static class HunterMotion
    {
        public const float SlashStart=.11f, SlashEnd=.30f, SlashDuration=.48f;
        public const float SlamHit=.8f, SlamEcho=1.05f, SlamDuration=2;
        public const float ShotHit=.12f, ShotDuration=.45f, BarrageInterval=.2f, BarrageDuration=2;
        static readonly float[][] starts={
            new[]{0f,.035f,SlashStart,.18f,.25f,.34f},
            new[]{0f,.08f,.27f,.65f,SlamHit,1.18f},
            new[]{0f,.035f,ShotHit,.155f,.23f,.32f}
        };
        public static float Duration(int skill)=>skill==1?SlashDuration:skill==2?SlamDuration:skill==3?ShotDuration:BarrageDuration;
        public static int Frame(int skill,float time)
        {
            if(skill==4)
            {
                if(time<.04f)return 0;if(time<BarrageInterval)return 1;
                if(time<1.2f){int shot=Mathf.Min(4,Mathf.FloorToInt((time+.00001f)/BarrageInterval)-1);return shot==0?2:shot%2==1?3:4;}
                return time<1.8f?5:0;
            }
            if(skill==2&&time>=1.8f)return 0;
            int result=0;var sequence=starts[Mathf.Clamp(skill-1,0,2)];
            for(int i=1;i<sequence.Length;i++)if(time>=sequence[i])result=i;
            return result;
        }
        public static MotionPhase Phase(int skill,float time)
        {
            int frame=Frame(skill,time);
            if(frame==0)return MotionPhase.Idle;if(frame==1)return MotionPhase.Anticipation;
            if(frame==5)return MotionPhase.Recovery;
            return skill==2?frame==4?MotionPhase.Hit:MotionPhase.Action:frame>=3?MotionPhase.Hit:MotionPhase.Action;
        }
        public static float Lift(int skill,float time)
        {
            if(skill!=2||time<=.22f||time>=SlamHit)return 0;
            return Mathf.Sin(Mathf.InverseLerp(.22f,SlamHit,time)*Mathf.PI)*.95f;
        }
    }

    // The source atlas stays untouched. A sprite material keys its flat magenta backdrop
    // during rendering; bounds analysis only establishes a common scale and foot pivots.
    public static class HunterAtlas
    {
        public const int Columns=6, Rows=4;
        static Sprite[,] frames;
        static Material material;
        public static float Scale {get;private set;}
        public static Material Material {get{Load();return material;}}
        public static Sprite Frame(int row,int column){Load();return frames[row,column];}
        public static bool IsInk(Color32 c)=>c.a>32 && !(Mathf.Min(c.r,c.b)-c.g>45);
        static void Load()
        {
            if(frames!=null)return;
            var texture=Resources.Load<Texture2D>("Motion/HunterSkills");
            if(!texture)throw new System.InvalidOperationException("HunterSkills animation atlas is missing.");
            var shader=Resources.Load<Shader>("Motion/CartoonAtlas");
            if(!shader)throw new System.InvalidOperationException("Cartoon atlas sprite shader is missing.");
            material=new Material(shader){name="Hunter cel animation"};
            var pixels=texture.GetPixels32();int w=texture.width/Columns,h=texture.height/Rows;
            frames=new Sprite[Rows,Columns];
            for(int row=0;row<Rows;row++)for(int col=0;col<Columns;col++)
            {
                int x0=col*w,y0=(Rows-1-row)*h,bottom=h,top=0;
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(IsInk(pixels[(y0+y)*texture.width+x0+x])){bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);}
                if(bottom>=top)throw new System.InvalidOperationException($"Empty animation cell {row}/{col}");
                // Use the feet/coat hem, not the extended weapon, as the horizontal anchor.
                float sum=0;int count=0,footBand=Mathf.Max(4,Mathf.RoundToInt((top-bottom)*.12f));
                for(int y=bottom;y<=bottom+footBand;y++)for(int x=0;x<w;x++)if(IsInk(pixels[(y0+y)*texture.width+x0+x])){sum+=x;count++;}
                float footX=count>0?sum/count:w*.5f;
                frames[row,col]=Sprite.Create(texture,new Rect(x0,y0,w,h),new Vector2(footX/w,(float)bottom/h),100,0,SpriteMeshType.FullRect);
                frames[row,col].name=$"HunterMotion_{row}_{col}";
                if(row==0&&col==0)Scale=1.9f/((top-bottom+1)/100f);
            }
        }
        public static void Pose(SpriteRenderer art,int row,int frame)
        {art.sprite=Frame(row,frame);art.sharedMaterial=Material;art.transform.localScale=Vector3.one*Scale;}
    }
}
