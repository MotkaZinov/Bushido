using UnityEngine;
namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        Transform lanternRig;
        LineRenderer villageCable, maskSuspension;
        readonly Transform[] villageLanterns=new Transform[4];
        readonly LineRenderer[] lanternStrings=new LineRenderer[4];
        static readonly float[] LanternPositions={.07f,.29f,.65f,.74f};
        Vector3 CablePoint(float u)=>view.ViewportToWorldPoint(new Vector3(u,.89f-Mathf.Sin(u*Mathf.PI)*.075f,13));
        void BeginVillageLanterns()
        {
            lanternRig=new GameObject("Guirnalda / capas de faroles de papel").transform;
            lanternRig.SetParent(world,false);
            villageCable=art.Line(lanternRig,new Vector3[33],new Color(.15f,.12f,.085f),.035f);
            maskSuspension=art.Line(lanternRig,new Vector3[2],new Color(.22f,.16f,.09f),.022f);
            var texture=Resources.Load<Texture2D>("UI/Seals/PaperLantern");
            for(int i=0;i<4;i++)
            {
                var quad=art.Shape(lanternRig,"Farol en hoja independiente",PrimitiveType.Quad,Vector3.zero,new Vector3(.9f,1.12f,1),Color.white);
                var material=art.PaperWeaponMaterial(texture);material.SetFloat("_PaperAlphaCutoff",.5f);
                quad.GetComponent<Renderer>().sharedMaterial=material;
                villageLanterns[i]=quad.transform;
                lanternStrings[i]=art.Line(lanternRig,new Vector3[2],new Color(.22f,.16f,.09f),.018f);
            }
            lanternRig.gameObject.SetActive(false);
        }
        void UpdateVillageLanterns(bool visible)
        {
            if(lanternRig==null)return;
            lanternRig.gameObject.SetActive(visible);
            if(!visible)return;
            for(int i=0;i<33;i++)villageCable.SetPosition(i,CablePoint(i/32f));
            for(int i=0;i<4;i++)
            {
                Vector3 hook=CablePoint(LanternPositions[i]);
                float swing=Mathf.Sin(maskAge*1.15f+i*.8f)*3;
                float length=i==0||i==3?.65f:.9f;
                Vector3 center=hook+new Vector3(Mathf.Sin(swing*Mathf.Deg2Rad)*length,-length,0);
                villageLanterns[i].position=center;
                villageLanterns[i].rotation=Quaternion.Euler(0,0,swing);
                lanternStrings[i].SetPosition(0,hook);
                lanternStrings[i].SetPosition(1,center+villageLanterns[i].up*.43f);
            }
            maskSuspension.SetPosition(0,CablePoint(.5f));
            maskSuspension.SetPosition(1,cursedMask.position+cursedMask.up*.65f);
        }
    }
}

