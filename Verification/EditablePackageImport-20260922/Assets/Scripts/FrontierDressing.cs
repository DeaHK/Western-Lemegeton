using UnityEngine;
namespace WesternLemegeton
{
    // Composition is deterministic and never consumes the combat random stream.
    // Tall masses belong to the banks; a continuous central trail joins real doors.
    public static class FrontierDressing
    {
        public static void Dress(ExplorationRoom room)
        {
            var map=room.Layout;
            int seed=room.Index*31;
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<5;i++)Cluster(room,new Vector2(side*(15.2f+(i%2)*.5f),-8.2f+i*4.0f),seed+i+(side+1)*9);
                for(int i=0;i<7;i++)Cluster(room,new Vector2(-12.5f+i*4.15f,side>0?9.2f+(i%2)*.4f:-10.3f),seed+20+i+(side+1)*9);
            }
            // A clear, low contrast hub lets silhouettes and attack warnings remain readable.
            Dust(map,Vector2.zero,new Vector2(13,9),new Color(.85f,.56f,.30f,.065f),-1360);
            foreach(var door in room.Doors)Approach(room,(Vector2)door.transform.localPosition);
            if(room.Index==0)Approach(room,new Vector2(-13.4f,0));
            foreach(var rect in map.Obstacles.ToArray())
            {
                if(Mathf.Abs(rect.center.x)>12||Mathf.Abs(rect.center.y)>7)continue;
                // Debris belongs with the cover instead of floating on its own in the arena.
                Vector2 p=new Vector2(rect.xMin-.55f,rect.yMin-.24f);
                if(Reserved(room,p,1.3f))continue;
                WesternEnvironment.Prop(map,"Deco5",p.x,p.y,.6f,false);
                Dust(map,rect.center,new Vector2(rect.width+1.3f,rect.height+1),new Color(.20f,.09f,.055f,.10f),-1350);
            }
        }
        static bool Reserved(ExplorationRoom room,Vector2 p,float margin)
        {
            foreach(var door in room.Doors)
            {
                Vector2 end=door.transform.localPosition;
                if(Vector2.Distance(p,end)<margin+2.5f)return true;
                Vector2 start=end*.46f,d=end-start;
                float t=Mathf.Clamp01(Vector2.Dot(p-start,d)/d.sqrMagnitude);
                if(Vector2.Distance(p,start+d*t)<margin+1.45f)return true;
            }
            return room.Index==0&&p.x<-9&&Mathf.Abs(p.y)<2.8f+margin;
        }
        static void Cluster(ExplorationRoom room,Vector2 p,int seed)
        {
            if(Reserved(room,p,.75f))return;
            var map=room.Layout;float width=3.0f+(seed%3)*.48f;
            if(seed%4==1)
            {
                WesternEnvironment.Prop(map,"Fence08",p.x,p.y,2.6f,false,seed%2==0);
                WesternEnvironment.Prop(map,"Deco3",p.x+1,p.y+.4f,1.1f);
                WesternEnvironment.Prop(map,"Deco10",p.x-1,p.y-.25f,1.4f,false);
                return;
            }
            var rock=WesternEnvironment.Prop(map,seed%2==0?"Deco9":"Deco4",p.x,p.y,width,true,seed%2==0);
            rock.color=new Color(.76f,.66f,.65f);
            // Only footprints inside the room affect walking; there is no invisible ring wall.
            if(Mathf.Abs(p.x)<15.8f&&p.y>-9&&p.y<8.8f)
            {
                float w=WesternEnvironment.PropWidth(rock.sprite.name,width)*.66f;
                var r=new Rect(p.x-w/2,p.y+.08f,w,.95f);
                map.Obstacles.Add(r);map.Buildings.Add(r);
            }
            Vector2 inward=-p.normalized;
            var tuft=p+inward*.9f+new Vector2((seed%2==0?1:-1)*1.05f,-.22f);
            if(!Reserved(room,tuft,.25f))WesternEnvironment.Prop(map,seed%3==0?"Deco10":"Deco5",tuft.x,tuft.y,1.0f+(seed%2)*.28f,false);
            if(seed%3==0)
            {
                Vector2 cactus=p+new Vector2(.9f,.5f);
                if(!Reserved(room,cactus,.55f))WesternEnvironment.Prop(map,"Deco3",cactus.x,cactus.y,1.1f);
            }
            Dust(map,p+inward*.5f,new Vector2(width*1.6f,2.5f),new Color(.19f,.075f,.05f,.14f),-1340);
        }
        static void Dust(WesternEnvironment.Layout map,Vector2 p,Vector2 size,Color color,int layer,float angle=0)
        {Ink.Shape("Trail soil",map.Root,p,size,color,layer,true,angle).sprite=Ink.SoftDisc;}
        static void Approach(ExplorationRoom room,Vector2 end)
        {
            var map=room.Layout;Vector2 direction=end.normalized,side=new Vector2(-direction.y,direction.x);
            float angle=Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg;
            for(int i=0;i<9;i++)
            {
                float t=(i+1)/9f;Vector2 p=end*t+side*(Mathf.Sin(t*Mathf.PI)*.65f);
                Dust(map,p,new Vector2(4.1f,4.5f),new Color(.89f,.62f,.36f,.16f),-1320,angle);
                if(i<2)continue;
                // Broken wagon ruts taper toward the opening, not across the entire arena.
                foreach(int s in new[]{-1,1})
                {
                    Vector2 rut=p+side*(.72f*s);
                    Dust(map,rut,new Vector2(.8f,.12f),new Color(.29f,.14f,.08f,.30f),-1290,angle);
                    Vector2 stone=p+side*(s*(2.15f+.18f*(i%3)));
                    Ink.Shape("Trail edge gravel",map.Root,stone,new Vector2(.13f+.04f*(i%3),.09f),new Color(.33f,.20f,.13f,.44f),-1280,true,i*37);
                }
            }
        }
        public static void GateBanks(ExplorationRoom room,Vector2 local)
        {
            bool horizontal=Mathf.Abs(local.x)>1;
            Vector2 transverse=horizontal?Vector2.up:Vector2.right;
            for(int s=-1;s<=1;s+=2)
            {
                var p=local+transverse*(horizontal&&s<0?-5.8f:s*3.7f);
                var rock=WesternEnvironment.Prop(room.Layout,s>0?"Deco9":"Deco4",p.x,p.y,3.2f,true,s<0);
                rock.color=new Color(.79f,.68f,.63f);
                Vector2 fence=p-local.normalized*1.3f+Vector2.down*.2f;
                WesternEnvironment.Prop(room.Layout,"Fence08",fence.x,fence.y,2.6f,true,s<0);
                Vector2 scrub=p-local.normalized*1.5f+transverse*s*.4f;
                WesternEnvironment.Prop(room.Layout,"Deco5",scrub.x,scrub.y,.95f,false);
            }
        }
    }
}
