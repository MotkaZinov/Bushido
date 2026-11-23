using UnityEngine;
using UnityEngine.InputSystem;

namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        const float MaxStamina = 100f;
        const float DeflectWindow = 0.18f;
        float stamina, recoveryDelay, guardHeldTime, guardFlash;
        enum FighterMove { Cut, Salute, MultiCut, Celebrate }
        float fighterReaction, fighterSwing, swingDuration, hurtFlash, guardPose;
        float postureFlash;
        FighterMove fighterMove;
        Vector2 fighterDirection;
        int strokeHits;
        bool guardHeld, guarding;
        Transform fighter;
        SamuraiRig fighterRig;
        AudioClip guardSound;
        AudioClip userParry01, userParry02;
        AudioClip postureSound;
        AudioSource battleMusic;

        void PlayBattleMusic()
        {
            SetupCutAudio();
            if (battleMusic == null)
            {
                battleMusic = gameObject.AddComponent<AudioSource>();
                battleMusic.playOnAwake = false;
                battleMusic.clip = Resources.Load<AudioClip>("Audio/AceroEnLaNiebla");
                battleMusic.loop = true;
                battleMusic.volume = 0.32f;
            }
            if (battleMusic.clip != null) battleMusic.Play();
        }

        void BeginDefense()
        {
            SetupSwingLayers();
            stamina = MaxStamina;
            recoveryDelay = guardFlash = hurtFlash = fighterReaction = fighterSwing = postureFlash = 0;
            guardPose = 0;
            strokeHits = 0;
            guardHeld = guarding = false;
            fighter = new GameObject("Samurái del jugador").transform;
            fighter.SetParent(world, false);
            CreateSamuraiModel(fighter, 2.65f);
            BeginKatanaProps();
            fighterRig = SamuraiRig.Bind(fighter);
            BeginCompanionFace();
            GuardFxBuild();
            if (fighterRig == null) Debug.LogWarning("Samurai.fbx: no se pudieron enlazar todos los huesos de reacción.");
            UpdateFighter(0);
        }

        void CreateSamuraiModel(Transform parent, float height)
        {
            var model = Instantiate(Resources.Load<GameObject>("Models/Samurai"), parent, false).transform;
            var renderers = model.GetComponentsInChildren<Renderer>();
            Bounds bounds = default;
            bool hasBody = false;
            foreach (var renderer in renderers)
            {
                if (!renderer.name.StartsWith("Katana", System.StringComparison.Ordinal))
                {
                    if (hasBody) bounds.Encapsulate(renderer.bounds);
                    else { bounds = renderer.bounds; hasBody = true; }
                }
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = art.ModelMaterial(materials[i]);
                renderer.sharedMaterials = materials;
            }
            // Calm swordsman: the eyes are closed ink lines (flattened and inked).
            var closed = new MaterialPropertyBlock();
            closed.SetColor("_BaseColor", PrototypeArt.Ink);
            foreach (var renderer in renderers)
            {
                if (renderer.name != "Ojo.L" && renderer.name != "Ojo.R") continue;
                renderer.SetPropertyBlock(closed);
                renderer.transform.localScale = Vector3.Scale(renderer.transform.localScale, new Vector3(1.25f, .26f, 1f));
            }
            float scale = height / Mathf.Max(bounds.size.y, 0.001f);
            Vector3 feet = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            model.localPosition -= parent.InverseTransformPoint(feet) * scale;
            model.localScale *= scale;
        }

        Transform CreateUmbrellaFigure(Vector3 position, float height)
        {
            var prefab = Resources.Load<GameObject>("Models/sombrilla");
            if (prefab == null) throw new System.InvalidOperationException("Falta el modelo de sombrilla.");
            var figure = new GameObject("Sombrilla de la lluvia").transform;
            figure.SetParent(world, false);
            var model = Instantiate(prefab, figure, false).transform;
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Destroy(figure.gameObject);
                throw new System.InvalidOperationException("La sombrilla no tiene mallas.");
            }

            model.localRotation = Quaternion.Euler(0, 180, 0) * model.localRotation;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = art.ModelMaterial(materials[i]);
                renderer.sharedMaterials = materials;
            }
            float scale = height / Mathf.Max(bounds.size.y, 0.001f);
            var feet = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            model.localPosition = (model.localPosition - figure.InverseTransformPoint(feet)) * scale;
            model.localScale *= scale;
            figure.position = position;
            return figure;
        }

        void UpdateDefense(float delta)
        {
            bool pressed = (Keyboard.current?.spaceKey.isPressed ?? false)
                || (Mouse.current?.rightButton.isPressed ?? false);
            if (Touchscreen.current != null)
            {
                int fingers = 0;
                foreach (var touch in Touchscreen.current.touches)
                    if (touch.press.isPressed) fingers++;
                pressed |= fingers >= 2;
            }
            if (!pressed || !guardHeld) guardHeldTime = 0;
            else guardHeldTime += delta;
            guardHeld = pressed;
            guarding = guardHeld && stamina > 0;
            recoveryDelay = Mathf.Max(0, recoveryDelay - delta);
            if (!guardHeld && recoveryDelay <= 0)
                stamina = Mathf.Min(MaxStamina, stamina + 30f * delta);
            GuardFxUpdate(delta);
        }

        bool TryGuard(Vector3 contact, float cost)
        {
            if (!guarding) return false;
            bool deflect = guardHeldTime <= DeflectWindow;
            stamina = Mathf.Max(0, stamina - (deflect ? 10f : cost));
            bool broken = stamina <= 0;
            recoveryDelay = broken ? 1.25f : 0.65f;
            guarding = !broken;
            if (deflect) JudgmentGain(JudgmentDeflectGain);
            guardFlash = 0.25f;
            fighterReaction = 0.3f;
            fighterSwing = 0;
            impactTime = deflect ? 0.075f : 0.035f;
            shakeTime = deflect ? 0.18f : 0.11f;
            PlayGuardSound(deflect);
            GuardBurst(contact, deflect);
            return true;
        }

        void GuardBurst(Vector3 contact, bool deflect)
        {
            if (guardFxPulse == null || fighter == null) return;
            if (deflect) ReactCompanion(CompanionMood.Parry, .7f);
            contact.z = -2.3f;
            contact.y = Mathf.Clamp(contact.y, 0.9f, 6.6f);
            // Deflected steel flies back up and away from the player.
            Vector3 away = contact - fighter.position;
            away.z = 0;
            away = (away.normalized + Vector3.up * 0.5f).normalized;
            // A deflect landing with the posture break must not shrink the bigger payoff.
            if (!(guardFxKind == GuardFxBreak && guardFxAge < 0.15f))
                GuardFxPulseAt(contact, deflect ? GuardFxDeflect : GuardFxBlock, away);
            if (deflect) GuardFxShards(contact, away, 9, 0.55f, 6f, 0.3f, PrototypeArt.Paper, PrototypeArt.Gold);
            else GuardFxShards(contact, away, 4, 0.9f, 2.2f, 0.13f, PrototypeArt.Ink, PrototypeArt.Paper);
        }

        AudioClip[] parrySoundVariants;
        AudioSource steelAudio;
        int parrySoundIndex;
        AudioClip MakeSteelImpact(bool precise, int variant)
        {
            const int rate=48000;
            int count=(int)(rate*(precise?.48f:.20f));
            var samples=new float[count];
            uint noiseState=(uint)(12345+variant*8917);
            float previousNoise=0;
            for(int i=0;i<count;i++)
            {
                float t=i/(float)rate;
                noiseState=noiseState*1664525u+1013904223u;
                float noise=(noiseState>>8)/8388607.5f-1;
                float highNoise=(noise-previousNoise)*.5f;previousNoise=noise;
                float tune=1+variant*.018f;
                float strike=highNoise*Mathf.Exp(-t*(precise?310:110))*.48f;
                float body=Mathf.Sin(2*Mathf.PI*(precise?690:380)*t)*Mathf.Exp(-t*65)*.24f;
                float ring=0;
                if(precise)
                {
                    ring=Mathf.Sin(2*Mathf.PI*1510*t*tune)*Mathf.Exp(-t*13)*.24f
                        +Mathf.Sin(2*Mathf.PI*2573*t*tune)*Mathf.Exp(-t*19)*.17f
                        +Mathf.Sin(2*Mathf.PI*4217*t*tune)*Mathf.Exp(-t*28)*.10f
                        +Mathf.Sin(2*Mathf.PI*6089*t*tune)*Mathf.Exp(-t*46)*.05f;
                    ring*=1+Mathf.Sin(2*Mathf.PI*31*t)*.08f;
                }
                else ring=Mathf.Sin(2*Mathf.PI*811*t)*Mathf.Exp(-t*37)*.16f;
                samples[i]=(float)System.Math.Tanh(strike+body+ring)*Mathf.Min(1,t*9000)*Mathf.Clamp01((count-i)/(rate*.018f));
            }
            var clip=AudioClip.Create(precise?"Parry metálico " + variant:"Bloqueo apagado",count,1,rate,false);
            clip.SetData(samples,0);return clip;
        }
        void PlayGuardSound(bool deflect)
        {
            SetupCutAudio();
            if(steelAudio==null)
            {
                steelAudio=gameObject.AddComponent<AudioSource>();
                steelAudio.playOnAwake=false;steelAudio.spatialBlend=0;steelAudio.priority=32;
            }
            if(userParry01==null)userParry01=Resources.Load<AudioClip>("Audio/Combat/Parry01");
            if(userParry02==null)userParry02=Resources.Load<AudioClip>("Audio/Combat/Parry02");
            if(userParry01!=null && userParry02!=null)
            {
                AudioClip hit=(parrySoundIndex++ & 1)==0 ? userParry01 : userParry02;
                steelAudio.PlayOneShot(hit,deflect?.85f:.65f);
                return;
            }
            if(guardSound==null)guardSound=MakeSteelImpact(false,0);
            if(parrySoundVariants==null || parrySoundVariants.Length==0)
            {
                parrySoundVariants=new AudioClip[4];
                for(int i=0;i<4;i++)parrySoundVariants[i]=MakeSteelImpact(true,i);
            }
            steelAudio.PlayOneShot(deflect?parrySoundVariants[(parrySoundIndex++ & int.MaxValue)%parrySoundVariants.Length]:guardSound,deflect?.95f:.52f);
        }
        void DamagePosture(int amount)
        {
            guard = Mathf.Min(guardLimit, guard + amount);
            if (postureFlash < 0.15f && guard < guardLimit) PlayPostureSound(false);
            postureFlash = 0.24f;
        }

        void PlayPostureSound(bool broken)
        {
            SetupCutAudio();
            if (broken) GuardFxBreakBurst();
            if (postureSound == null)
            {
                const int rate = 22050;
                var samples = new float[rate / 3];
                for (int i = 0; i < samples.Length; i++)
                {
                    float t = i / (float)rate;
                    float crack = Mathf.Sin(9600f * t) * Mathf.Exp(-t * 100f);
                    float body = (Mathf.Sin(970f * t) + Mathf.Sin(1490f * t) * 0.35f) * Mathf.Exp(-t * 19f);
                    samples[i] = (crack + body) * Mathf.Min(1, t * 4000f) * 0.38f;
                }
                postureSound = AudioClip.Create("Impacto de postura", samples.Length, 1, rate, false);
                postureSound.SetData(samples, 0);
            }
            cutAudio.pitch = broken ? 0.72f : 1.05f + guard * 0.045f;
            cutAudio.PlayOneShot(postureSound, broken ? 0.8f : 0.35f);
        }

        void FighterCut(Vector2 direction, bool rankedUp = false)
        {
            strokeHasSwing = true;
            FighterMove move = rankedUp && StyleLevel >= 4 ? FighterMove.Celebrate
                : strokeHits >= 2 ? FighterMove.MultiCut : rankedUp ? FighterMove.Salute : FighterMove.Cut;
            if (fighterSwing > 0 && (int)fighterMove > (int)move) return;
            fighterMove = move;
            fighterDirection = new Vector2(-direction.x, direction.y).normalized;
            switch (move)
            {
                case FighterMove.MultiCut:
                    swingDuration = SamuraiCutDuration * 1.7f;
                    break;
                case FighterMove.Salute:
                    swingDuration = 0.67f;
                    break;
                case FighterMove.Celebrate:
                    swingDuration = 0.95f;
                    break;
                default:
                    swingDuration = SamuraiCutDuration;
                    break;
            }
            fighterSwing = swingDuration;
        }

        void UpdateFighter(float delta)
        {
            fighterSwing = Mathf.Max(0, fighterSwing - delta);
            fighterReaction = Mathf.Max(0, fighterReaction - delta);
            guardFlash = Mathf.Max(0, guardFlash - delta);
            hurtFlash = Mathf.Max(0, hurtFlash - delta);
            postureFlash = Mathf.Max(0, postureFlash - delta);
            float recoil = fighterReaction / 0.3f;
            guardPose = Mathf.MoveTowards(guardPose, guarding || recoil > 0 || (DuelActive && duelPhase == DuelPhase.Telegraph && duelPrepared) ? 1 : 0, delta * 12f);
            float phase = fighterSwing > 0 ? 1f - fighterSwing / swingDuration : 0;
            // Readable stage timing: gather, sweep, hold the silhouette, then settle.
            float sweep = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.16f, .46f, phase));
            float recover = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.57f, 1f, phase));
            float windup = Mathf.Sin(Mathf.Clamp01(phase / .25f) * Mathf.PI);
            bool cutting = fighterMove == FighterMove.Cut || fighterMove == FighterMove.MultiCut;
            float motion = (cutting ? sweep * (1f - recover) : Mathf.Sin(phase * Mathf.PI)) * (1 - guardPose);
            float flourish = fighterMove == FighterMove.MultiCut ? Mathf.Sin(phase * Mathf.PI * 2) * motion : 0;
            float salute = fighterMove == FighterMove.Salute ? motion : 0;
            float celebration = fighterMove == FighterMove.Celebrate ? motion : 0;
            if (mode == Mode.Result && won) celebration = Mathf.MoveTowards(0, 1, companionReactionAge * 3);
            if (bossFinalStage == 1) salute = Mathf.Max(salute, .35f + .6f * drawPullProgress);
            float swing = fighterMove == FighterMove.Cut || fighterMove == FighterMove.MultiCut ? motion : 0;
            float arc = cutting ? Mathf.Lerp(-38f, 14f, sweep) * Mathf.Sign(fighterDirection.x + .01f) : 0;
            Vector2 gesture = Quaternion.AngleAxis(arc + 95f * flourish, Vector3.forward) * fighterDirection;
            // Real cuts: a kenjutsu arc chosen by the swipe (see BushidoSamuraiMoves).
            SamuraiCutPose cut = default;
            if (cutting) { cut = CurrentSamuraiCut(phase); swing = cut.weight * (1f - guardPose); gesture = cut.blade; flourish = 0; }
            else if (fighterSwing <= 0 && guardPose < .01f && mode == Mode.Battle)
            {
                // Idle kamae shift every few seconds: the blade drops from the high guard toward the enemy and rises again.
                float cycle = Mathf.Repeat(battleTime + 2f, 6.5f);
                float shift = cycle < 1.5f ? Mathf.Sin(cycle / 1.5f * Mathf.PI) : 0f;
                swing = shift * .5f;
                gesture = new Vector2(Mathf.Cos(28f * Mathf.Deg2Rad), Mathf.Sin(28f * Mathf.Deg2Rad));
            }
            samuraiCutCrouch = cut.crouch * cut.weight; samuraiCutLean = cut.lean * cut.weight;
            float cheerHop = celebration * 0.035f + salute * 0.014f + Mathf.Abs(flourish) * 0.012f;
            // Hurt reads heavier than a guard recoil: knocked further back, buckling and shuddering.
            float hurt = hurtFlash / 0.32f, buckle = Mathf.Sin(hurt * Mathf.PI);
            cheerHop -= buckle * 0.012f;
            float step = -swing * 0.012f - cut.lunge * cut.weight * 0.016f + recoil * 0.018f - salute * 0.008f
                + hurt * 0.012f + Mathf.Sin(hurtFlash * 90f) * hurt * 0.003f;
            float idleSway = Mathf.Sin(battleTime * 2.4f) * 0.0025f;
            fighter.position = view.ViewportToWorldPoint(new Vector3(0.875f + step + idleSway, 0.12f + cheerHop, 12f));
            fighter.position += Vector3.up * (Mathf.Sin(battleTime * 3f) * 0.035f);
            fighter.localScale = Vector3.one * (view.orthographicSize * 2f * 0.34f / 2.65f);
            float enthusiasm = Mathf.Sin(battleTime * 11f) * celebration;
            // The cut stays facing the camera: the arc is carried by arm and blade, not by turning the body away.
            fighter.rotation = Quaternion.Euler(-salute * 8f, 168f + (cutting ? 5f * windup * (1f - guardPose) : 0f) - swing * 10f + enthusiasm * 9f,
                recoil * 15f + hurt * 9f + flourish * 17f - swing * 12f + enthusiasm * 7f);
            float squash = 1 - celebration * 0.06f + swing * 0.025f - buckle * 0.05f;
            fighter.localScale = Vector3.Scale(fighter.localScale, new Vector3(1 / squash, squash, 1));
            fighterRig?.Pose(gesture, swing, flourish, salute, celebration, guardPose, recoil, Mathf.Sin(battleTime * 2.1f));
            fighterRig?.PlaySwordTrial(false, phase, guardPose, recoil); // the sampled clip only drives the block now; cuts are keyed
            UpdateSamuraiTrail(delta, cut.cutting * cut.weight);
            if (mode == Mode.Battle) UpdateCompanionFace(delta);
            else companionReactionAge += delta;
            PoseCompanion(delta, swing);
            if (mode == Mode.Battle) UpdateKatanaProps(delta);
        }

        void DrawDefense()
        {
            if (mode != Mode.Battle && !(mode == Mode.Transition && battleFrame == null)) return;
            if (guardFlash > 0)
            {
                // Deflect: a gold brush line snapping in from the full width. Plain block: a short dull tick.
                float fade = Mathf.Clamp01(guardFlash * 4);
                bool sharp = guardFxKind != GuardFxBlock;
                float half = sharp ? 640f * Mathf.Sqrt(fade) : 150f * fade, thick = sharp ? 5f : 2f;
                Color line = sharp ? new Color(1, 0.72f, 0.22f, fade) : new Color(PrototypeArt.Paper.r, PrototypeArt.Paper.g, PrototypeArt.Paper.b, fade * 0.7f);
                Panel(new Rect(640 - half, 133.5f - thick * 0.5f, half * 2, thick), line);
            }
            if (hurtFlash > 0)
            {
                // Red ink vignette: stacked bands, darkest at the paper's edge, quick release.
                float hurt = hurtFlash / 0.32f;
                Color ink = new Color(PrototypeArt.Red.r, PrototypeArt.Red.g, PrototypeArt.Red.b, hurt * hurt * 0.22f);
                for (int i = 1; i <= 4; i++)
                {
                    float band = i * 20f;
                    Panel(new Rect(0, 0, 1280, band), ink);
                    Panel(new Rect(0, 720 - band, 1280, band), ink);
                    Panel(new Rect(0, band, band, 720 - band * 2), ink);
                    Panel(new Rect(1280 - band, band, band, 720 - band * 2), ink);
                }
            }
        }
    }
}





