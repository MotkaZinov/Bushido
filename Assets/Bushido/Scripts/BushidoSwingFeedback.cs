using UnityEngine;
namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        AudioClip[] swingBegins, swingEnds, sliceImpacts;
        AudioClip userSlash;
        AudioSource swingStartAudio, swingEndAudio, sliceImpactAudio;
        int swingSequence;
        bool strokeWhooshPlayed;
        LineRenderer trailAura;
        Material[] trailAuraMaterials;
        AudioSource SwingSource()
        {
            var source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.priority=110;return source;
        }
        AudioClip MakeSwingLayer(int layer,int variant)
        {
            const int rate=44100;float duration=layer==0?.16f:layer==1?.23f:.12f;
            var data=new float[(int)(rate*duration)];uint state=(uint)(711+variant*947+layer*413);
            float low=0,phase=0;
            for(int i=0;i<data.Length;i++)
            {
                float t=i/(float)rate,u=t/duration;
                state=state*1664525u+1013904223u;float noise=(state>>8)/8388607.5f-1;
                low=Mathf.Lerp(low,noise,layer==1?.10f:.22f);float air=noise-low;
                float envelope=layer==0?Mathf.Pow(Mathf.Sin(Mathf.PI*u),1.7f):Mathf.Min(1,t*1600)*Mathf.Exp(-t*(layer==1?23:62));
                float frequency=(layer==0?1100:layer==1?780:1700)*(1+variant*.055f)*(1-u*.65f);
                phase+=frequency*Mathf.PI*2/rate;
                float steel=Mathf.Sin(phase)*.12f+Mathf.Sin(phase*2.61f)*.045f;
                float body=low*(layer==2?.28f:.16f);
                data[i]=(air*.35f+body+steel)*envelope*Mathf.Clamp01((1-u)*25);
            }
            var clip=AudioClip.Create("Tajo " + layer + " variante " + variant,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        void SetupSwingLayers()
        {
            if(swingBegins!=null)return;
            swingStartAudio=SwingSource();swingEndAudio=SwingSource();sliceImpactAudio=SwingSource();
            swingBegins=new AudioClip[6];swingEnds=new AudioClip[6];sliceImpacts=new AudioClip[6];
            for(int i=0;i<6;i++){swingBegins[i]=MakeSwingLayer(0,i);swingEnds[i]=MakeSwingLayer(1,i);sliceImpacts[i]=MakeSwingLayer(2,i);}
        }
        void BeginSwingSound()
        {
            SetupSwingLayers();swingSequence++;
            if(userSlash==null)userSlash=Resources.Load<AudioClip>("Audio/Combat/Slash01");
            swingStartAudio.PlayOneShot(userSlash!=null?userSlash:swingBegins[swingSequence%6],.48f+StyleLevel*.035f);
        }
        void EndSwingSound()
        {
            SetupSwingLayers();
            // The supplied slash includes its own ending.
            if(userSlash==null)swingEndAudio.PlayOneShot(swingEnds[swingSequence%6],.32f+StyleLevel*.03f);
        }
        void SliceImpactSound()
        {
            SetupSwingLayers();
            sliceImpactAudio.pitch = 0.96f + StyleLevel * 0.035f + (strokeHits % 3) * 0.025f;
            sliceImpactAudio.PlayOneShot(sliceImpacts[(swingSequence+strokeHits)%6],.38f+StyleLevel*.025f);
        }
        void RefreshSlashAura(Vector3[] positions,int count)
        {
            if(trailAura==null)
            {
                trailAura=art.Line(world,new[]{Vector3.zero,Vector3.zero},Color.white,.2f);
                trailAura.name="Aura del corte por rango";
                trailAura.numCornerVertices=3;trailAura.numCapVertices=4;
            }
            if(trailAuraMaterials==null || trailAuraMaterials.Length!=styleColors.Length || trailAuraMaterials[0]==null)
            {
                if(trailAuraMaterials!=null)foreach(var old in trailAuraMaterials)if(old!=null)Destroy(old);
                var shader=Resources.Load<Shader>("BushidoTrailGlow");
                trailAuraMaterials=new Material[styleColors.Length];
                for(int i=0;i<styleColors.Length;i++)
                {
                    var material=new Material(shader);material.SetColor("_Tint",styleColors[i]);trailAuraMaterials[i]=material;
                }
            }
            trailAura.positionCount=count;trailAura.SetPositions(positions);
            trailAura.enabled=count>1 && bossFinalStage!=2;
            trailAura.sharedMaterial=trailAuraMaterials[StyleLevel];
            trailAura.widthMultiplier=.22f+StyleLevel*.065f+comboPulse*.06f;
            trailAura.widthCurve=cutTrailCurve;
        }
        void DisposeSwingLayers()
        {
            foreach(var set in new[]{swingBegins,swingEnds,sliceImpacts})if(set!=null)foreach(var clip in set)if(clip!=null)Destroy(clip);
            if(trailAuraMaterials!=null)foreach(var material in trailAuraMaterials)if(material!=null)Destroy(material);
        }
    }
}


