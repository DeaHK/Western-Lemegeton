using UnityEngine;
namespace WesternLemegeton
{
    public enum LocomotionState { Idle, Moving, Settling }

    // A gait follows distance actually travelled, so a wall cannot produce running in place.
    public sealed class HunterLocomotion
    {
        public const float StrideLength=2.4f, SettleDuration=.12f;
        public LocomotionState State {get;private set;}
        public float Cycle {get;private set;}
        public float Speed {get;private set;}
        public int Sector {get;private set;}
        public int Row=>Sector==2?3:Sector==1||Sector==3?2:Sector==6?0:1;
        public bool Mirror=>Sector>=3&&Sector<=5;
        public int Frame=>State==LocomotionState.Moving?1+Mathf.Min(5,Mathf.FloorToInt(Cycle*6)):State==LocomotionState.Settling?7:0;
        float settle;
        public void Reset(Vector2 direction)
        {Cycle=Speed=settle=0;State=LocomotionState.Idle;SetDirection(direction,false);}
        void SetDirection(Vector2 direction,bool hysteresis)
        {
            if(direction.sqrMagnitude<.0001f)return;
            float angle=Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg;
            if(hysteresis&&Mathf.Abs(Mathf.DeltaAngle(Sector*45,angle))<28)return;
            Sector=(Mathf.RoundToInt(angle/45)+8)%8;
        }
        public void Advance(Vector2 displacement,float dt,bool suppressed)
        {
            if(dt<=0)return;
            if(suppressed){Speed=0;State=LocomotionState.Idle;settle=0;return;}
            float distance=displacement.magnitude;Speed=distance/dt;
            if(Speed>.08f)
            {
                SetDirection(displacement,State==LocomotionState.Moving);
                Cycle=Mathf.Repeat(Cycle+distance/StrideLength,1);
                State=LocomotionState.Moving;settle=0;
            }
            else
            {
                if(State==LocomotionState.Moving){State=LocomotionState.Settling;settle=0;}
                if(State==LocomotionState.Settling){settle+=dt;if(settle>=SettleDuration){State=LocomotionState.Idle;Cycle=0;}}
            }
        }
    }

    public static class HunterLocomotionAtlas
    {
        public const int Columns=8,Rows=4;
        static Sprite[,] frames;
        public static float Scale {get;private set;}
        public static Sprite Frame(int row,int column){Load();return frames[row,column];}
        static void Load()
        {
            if(frames!=null)return;
            var texture=Resources.Load<Texture2D>("Motion/HunterLocomotion");
            if(!texture||texture.width%Columns!=0||texture.height%Rows!=0)throw new System.InvalidOperationException("Hunter locomotion needs an 8 x 4 atlas.");
            var pixels=texture.GetPixels32();int w=texture.width/Columns,h=texture.height/Rows;
            frames=new Sprite[Rows,Columns];
            for(int row=0;row<Rows;row++)
            {
                int y0=(Rows-1-row)*h,bottom=h,top=0;
                // Keep the ground baseline fixed per direction. Following each raised boot
                // would cancel the drawn stride. Register the head horizontally to compensate
                // for sheet spacing without following the swinging hands, coat or feet.
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(HunterAtlas.IsInk(pixels[(y0+y)*texture.width+x])){bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);}
                if(bottom>=top)throw new System.InvalidOperationException("Empty locomotion idle row "+row);
                if(row==0)Scale=1.9f/((top-bottom+1)/100f);
                for(int col=0;col<Columns;col++)
                {
                    int x0=col*w,poseTop=0,poseBottom=h;
                    for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(HunterAtlas.IsInk(pixels[(y0+y)*texture.width+x0+x])){poseTop=Mathf.Max(poseTop,y);poseBottom=Mathf.Min(poseBottom,y);}
                    int headLow=Mathf.RoundToInt(Mathf.Lerp(poseTop,poseBottom,.25f)),headHigh=Mathf.RoundToInt(Mathf.Lerp(poseTop,poseBottom,.10f));
                    float sum=0;int count=0;
                    for(int y=headLow;y<=headHigh;y++)for(int x=0;x<w;x++)if(HunterAtlas.IsInk(pixels[(y0+y)*texture.width+x0+x])){sum+=x;count++;}
                    float anchorX=count>0?sum/count:w*.5f;
                    frames[row,col]=Sprite.Create(texture,new Rect(x0,y0,w,h),new Vector2(anchorX/w,(float)bottom/h),100,0,SpriteMeshType.FullRect);
                    frames[row,col].name=$"HunterMove_{row}_{col}";
                }
            }
        }
        public static void Pose(SpriteRenderer art,int row,int frame)
        {art.sprite=Frame(row,frame);art.sharedMaterial=HunterAtlas.Material;art.transform.localScale=Vector3.one*Scale;}
    }
}
