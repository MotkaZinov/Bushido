using UnityEngine;

namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        AudioClip finalDrawStrikeSound, spiritDissolveSound;
        AudioSource finalDrawAudio, spiritDissolveAudio;

        void PrepareFinisherAudio()
        {
            if (finalDrawStrikeSound == null)
                finalDrawStrikeSound = Resources.Load<AudioClip>("Audio/Combat/FinalDrawStrike");
            if (spiritDissolveSound == null)
                spiritDissolveSound = Resources.Load<AudioClip>("Audio/Combat/SpiritDissolve");
            if (finalDrawAudio == null) finalDrawAudio = FinisherAudioSource();
            if (spiritDissolveAudio == null) spiritDissolveAudio = FinisherAudioSource();
        }

        AudioSource FinisherAudioSource()
        {
            SetupCutAudio();
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.priority = 24;
            source.pitch = 1;
            source.loop = false;
            return source;
        }

        void ResetFinisherAudio()
        {
            PrepareFinisherAudio();
            finalDrawAudio.Stop();
            spiritDissolveAudio.Stop();
        }

        void PlayFinisherDrawSound()
        {
            PrepareFinisherAudio();
            if (finalDrawStrikeSound != null) finalDrawAudio.PlayOneShot(finalDrawStrikeSound, .85f);
        }

        void PlaySpiritDissolveSound()
        {
            PrepareFinisherAudio();
            // This source survives the world replacement: its tail can finish over the map.
            if (spiritDissolveSound != null) spiritDissolveAudio.PlayOneShot(spiritDissolveSound, .8f);
        }
    }
}
