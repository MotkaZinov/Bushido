using UnityEngine;
namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        Transform ritualAltar;
        Transform[] altarFlames;
        void BeginRitualAltar()
        {
            ritualAltar=new GameObject("Altar de papel detrás del teatro").transform;
            ritualAltar.SetParent(world,false);altarFlames=new Transform[2];
            for(int side=0;side<2;side++)
            {
                float sign=side==0?-1:1;
                for(int i=0;i<3;i++)
                {
                    var post=art.Shape(ritualAltar,"Marco de madera entintada",PrimitiveType.Quad,new Vector3(sign*(6.4f-i*.15f),4,5+i*.04f),new Vector3(.13f,10,1),PrototypeArt.Ink);
                    post.transform.localRotation=Quaternion.Euler(0,0,sign*i*2);
                }
                art.Shape(ritualAltar,"Lámpara de papel",PrimitiveType.Quad,new Vector3(sign*4.4f,3,4.5f),new Vector3(.5f,1.05f,1),new Color(.65f,.48f,.23f));
                art.Shape(ritualAltar,"Tinta del soporte",PrimitiveType.Quad,new Vector3(sign*4.4f,2.3f,4.4f),new Vector3(.7f,.13f,1),PrototypeArt.Ink);
                var flame=art.Shape(ritualAltar,"Luz ilustrada",PrimitiveType.Quad,new Vector3(sign*4.4f,3,4.4f),new Vector3(.28f,.7f,1),new Color(.9f,.77f,.45f));
                altarFlames[side]=flame.transform;
                for(int j=0;j<3;j++)
                {
                    float x=sign*(3.5f+j*.8f);
                    var slip=art.Shape(ritualAltar,"Papel votivo colgante",PrimitiveType.Quad,new Vector3(x,6.65f-j*.14f,4.8f),new Vector3(.18f,.55f,1),PrototypeArt.Paper);
                    slip.transform.localRotation=Quaternion.Euler(0,0,sign*(j*8-10));
                    art.Line(ritualAltar,new[]{new Vector3(x-.04f,6.7f-j*.14f,4.75f),new Vector3(x+.03f,6.5f-j*.14f,4.75f)},PrototypeArt.Red,.025f);
                }
            }
            var rope=new Vector3[25];
            for(int i=0;i<25;i++){float u=i/24f;rope[i]=new Vector3(Mathf.Lerp(-7,7,u),7.8f-Mathf.Sin(u*Mathf.PI)*.65f,5);}
            art.Line(ritualAltar,rope,new Color(.48f,.37f,.22f),.08f);
            ritualAltar.gameObject.SetActive(false);
        }
        void UpdateRitualAltar()
        {
            if(ritualAltar==null)return;
            ritualAltar.gameObject.SetActive(IsBoss&&paperOpening>.15f&&bossFinalStage<2);
            for(int i=0;i<2;i++)
            {
                float flicker=1+Mathf.Sin(Time.time*3.3f+i)*.025f+landedBossSeals*.055f;
                altarFlames[i].localScale=new Vector3(.28f*flicker,.7f*flicker,1);
            }
        }
    }
}
