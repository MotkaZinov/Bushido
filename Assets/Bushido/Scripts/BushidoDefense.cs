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
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                if (!renderer.name.StartsWith("Katana", System.StringComparison.Ordinal))
                    bounds.Encapsulate(renderer.bounds);
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = art.ModelMaterial(materials[i]);
                renderer.sharedMaterials = materials;
            }
            float scale = height / Mathf.Max(bounds.size.y, 0.001f);
            Vector3 feet = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            model.localPosition -= parent.InverseTransformPoint(feet) * scale;
            model.localScale *= scale;
        }

        Transform CreateBossFigure(int node, Vector3 position, float height)
        {
            if(node==2)
            {
                var illustrated=CreatePaperOgre(position,height);
                if(illustrated!=null)return illustrated;
            }
            string modelResource = node == 2 ? "Drax/Drax/Drax_MarvelNow" : "OniSamurai";
            string modelName = node == 2 ? "Drax" : "Oni Samurai";
            GameObject prefab = Resources.Load<GameObject>("Models/Bosses/" + modelResource);
            if (prefab == null)
            {
                Debug.LogWarning("No se pudo cargar el modelo de jefe en Resources/Models/Bosses/" + modelResource);
                return CreateBossPlaceholder(node, position, height);
            }

            var figure = new GameObject(modelName).transform;
            figure.SetParent(world, false);
            var model = Instantiate(prefab, figure, false).transform;
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Destroy(figure.gameObject);
                return CreateBossPlaceholder(node, position, height);
            }

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

        Transform CreateBossPlaceholder(int node, Vector3 position, float height)
        {
            bool drax = node == 2;
            Color armor = drax ? new Color(0.22f, 0.34f, 0.32f) : new Color(0.58f, 0.17f, 0.11f);
            Transform figure = art.Samurai(world, position, armor, height / 2.2f);
            figure.name = drax ? "Drax provisional" : "Oni samurái provisional";
            art.Shape(figure, "Máscara yokai", PrimitiveType.Sphere, new Vector3(0, 1.51f, 0.18f),
                new Vector3(0.46f, 0.40f, 0.25f), drax ? PrototypeArt.Paper : armor);
            foreach (int side in new[] { -1, 1 })
            {
                art.Shape(figure, "Ojo", PrimitiveType.Cube, new Vector3(side * 0.105f, 1.56f, 0.297f),
                    new Vector3(0.073f, 0.038f, 0.025f), drax ? PrototypeArt.Ink : PrototypeArt.Gold);
                if (!drax)
                {
                    art.Shape(figure, "Cuerno", PrimitiveType.Cube, new Vector3(side * 0.23f, 1.92f, 0.12f),
                        new Vector3(0.11f, 0.35f, 0.13f), PrototypeArt.Paper).transform.localRotation =
                        Quaternion.Euler(0f, 0f, -side * 26f);
                    art.Shape(figure, "Hombrera", PrimitiveType.Cube, new Vector3(side * 0.44f, 1.28f, 0.04f),
                        new Vector3(0.42f, 0.23f, 0.50f), armor);
                }
            }
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
            FighterMove move = rankedUp && StyleLevel >= 4 ? FighterMove.Celebrate
                : strokeHits >= 2 ? FighterMove.MultiCut : rankedUp ? FighterMove.Salute : FighterMove.Cut;
            if (fighterSwing > 0 && (int)fighterMove > (int)move) return;
            fighterMove = move;
            fighterDirection = new Vector2(-direction.x, direction.y).normalized;
            switch (move)
            {
                case FighterMove.MultiCut:
                    swingDuration = 0.62f + Mathf.Min(3, strokeHits - 2) * 0.08f;
                    break;
                case FighterMove.Salute:
                    swingDuration = 0.67f;
                    break;
                case FighterMove.Celebrate:
                    swingDuration = 0.95f;
                    break;
                default:
                    swingDuration = fighterRig?.SwordDuration ?? 0.28f;
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
            guardPose = Mathf.MoveTowards(guardPose, guarding || recoil > 0 ? 1 : 0, delta * 12f);
            float phase = fighterSwing > 0 ? 1f - fighterSwing / swingDuration : 0;
            float motion = Mathf.Sin(phase * Mathf.PI) * (1 - guardPose);
            float flourish = fighterMove == FighterMove.MultiCut ? Mathf.Sin(phase * Mathf.PI * 2) * motion : 0;
            float salute = fighterMove == FighterMove.Salute ? motion : 0;
            float celebration = fighterMove == FighterMove.Celebrate ? motion : 0;
            float swing = fighterMove == FighterMove.Cut || fighterMove == FighterMove.MultiCut ? motion : 0;
            Vector2 gesture = Quaternion.AngleAxis(95f * flourish, Vector3.forward) * fighterDirection;
            float cheerHop = celebration * 0.035f + salute * 0.014f + Mathf.Abs(flourish) * 0.012f;
            // Hurt reads heavier than a guard recoil: knocked further back, buckling and shuddering.
            float hurt = hurtFlash / 0.32f, buckle = Mathf.Sin(hurt * Mathf.PI);
            cheerHop -= buckle * 0.012f;
            float step = -swing * 0.012f + recoil * 0.018f - salute * 0.008f
                + hurt * 0.012f + Mathf.Sin(hurtFlash * 90f) * hurt * 0.003f;
            float idleSway = Mathf.Sin(battleTime * 2.4f) * 0.0025f;
            fighter.position = view.ViewportToWorldPoint(new Vector3(0.875f + step + idleSway, 0.12f + cheerHop, 12f));
            fighter.position += Vector3.up * (Mathf.Sin(battleTime * 3f) * 0.035f);
            fighter.localScale = Vector3.one * (view.orthographicSize * 2f * 0.34f / 2.65f);
            float enthusiasm = Mathf.Sin(battleTime * 11f) * celebration;
            fighter.rotation = Quaternion.Euler(-salute * 8f, 168f - swing * 28f + enthusiasm * 9f,
                recoil * 15f + hurt * 9f + flourish * 17f - swing * 12f + enthusiasm * 7f);
            float squash = 1 - celebration * 0.06f + swing * 0.025f - buckle * 0.05f;
            fighter.localScale = Vector3.Scale(fighter.localScale, new Vector3(1 / squash, squash, 1));
            fighterRig?.Pose(gesture, swing, flourish, salute, celebration, guardPose, recoil);
            fighterRig?.PlaySwordTrial(fighterSwing > 0 && fighterMove == FighterMove.Cut, phase, guardPose, recoil);
            UpdateKatanaProps(delta);
            UpdateCompanionFace(delta);
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
                Color line = sharp ? new Color(1, 0.72f, 0.22f, fade) : new Color(0.9f, 0.82f, 0.65f, fade * 0.7f);
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





