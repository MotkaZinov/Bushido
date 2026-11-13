using UnityEngine;
namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        Transform cursedMask;
        float maskAge;
        Material maskSpriteMaterial;
        int maskSpriteFrame;
        void BeginCursedMask()
        {
            cursedMask=null;maskAge=0;
            if(!IsBoss)return;
            ritualAltar=null;
            var atlas=Resources.Load<Texture2D>("UI/Seals/OniMaskSprites");
            if(atlas==null){Debug.LogWarning("Faltan los sprites de la máscara.");return;}
            cursedMask=new GameObject("Máscara maldita de papel / sprites").transform;
            cursedMask.SetParent(world,false);
            var quad=art.Shape(cursedMask,"Recorte de máscara",PrimitiveType.Quad,Vector3.zero,new Vector3(1.55f,1.55f,1),Color.white);
            maskSpriteMaterial=art.PaperWeaponMaterial(atlas);
            maskSpriteMaterial.SetFloat("_PaperAlphaCutoff",.25f);
            quad.GetComponent<Renderer>().sharedMaterial=maskSpriteMaterial;
            BeginVillageLanterns();
            maskSpriteFrame=-1;            cursedMask.gameObject.SetActive(false);
        }
        void UpdateCursedMask(float delta)
        {

            if(cursedMask==null)return;
            maskAge+=delta;
            int frame=Mathf.Clamp(landedBossSeals,0,3);
            if(frame!=maskSpriteFrame)
            {
                maskSpriteFrame=frame;
                maskSpriteMaterial.SetTextureScale("_BaseMap",new Vector2(.498f,.498f));
                maskSpriteMaterial.SetTextureOffset("_BaseMap",new Vector2((frame%2)*.5f+.001f,(1-frame/2)*.5f+.001f));
            }
            bool visible=opponent!=null&&opponent.gameObject.activeInHierarchy&&bossFinalStage<2;
            cursedMask.gameObject.SetActive(visible);
            Vector3 anchor=CablePoint(.5f)+Vector3.down*1.15f;
            float struggle=1-landedBossSeals/3f;
            cursedMask.position=anchor+new Vector3(0,Mathf.Sin(maskAge*1.8f)*.07f,0)*struggle;
            cursedMask.rotation=Quaternion.Euler(0,0,Mathf.Sin(maskAge*1.3f)*3*struggle);
            UpdateVillageLanterns(visible);
        }
        Vector3 CursedSealTarget()
        {
            return cursedMask.position+new Vector3((bossSeals-2)*.32f,-.22f,-.3f);
        }
    }
}





