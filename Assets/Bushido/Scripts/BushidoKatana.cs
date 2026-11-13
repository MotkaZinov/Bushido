using UnityEngine;
namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        Transform sheathedKatana, counterKatana;
        float katanaCutTime;
        Vector3 katanaCutDirection;
        float katanaParryTime;
        void PlayKatanaParry(Vector3 direction)
        {
            PlayKatanaCounter(direction);
            katanaParryTime = .34f;
            ReactBoss(BossReaction.Deflected);
            impactTime = Mathf.Max(impactTime, .085f);
            shakeTime = Mathf.Max(shakeTime, .13f);
            guardFlash = .28f;
            fighterReaction = .25f;
        }
        Transform drawSaya;
        bool drawGripHeld;
        float drawGripStart, drawPullTarget;
        void BeginDrawGrip(Vector2 pointer)
        {
            Vector2 grip = view.WorldToScreenPoint(drawHiltPosition);
            drawGripHeld = bossFinalStage == 1 && (!RitualModelKatana || katanaEquipTime >= 1.8f) && Vector2.Distance(pointer, grip) < Screen.height * .14f;
            drawGripStart = drawPullProgress;
        }
        float drawSwordScale;
        Mesh paperSwordMesh;
        readonly Vector3[] paperSwordVertices = new Vector3[4];
        readonly Vector2[] paperSwordUV = new Vector2[4];
        float paperBladeBottom = float.NaN;
        const float PaperSayaMouth = .43f;
        Material katanaBladeMaterial, katanaSayaMaterial;
        KatanaSpiritVfx katanaSpirit;
        float katanaEquipTime;
        bool finalKatanaLoaded;
        // El ogro y el jefe final comparten la presentación 3D del remate.
        // Las defensas normales siguen usando las ilustraciones de papel.
        bool RitualModelKatana => mission == 2 || mission == titles.Length - 1;

        void UpdateRitualKatana(float delta)
        {
            if (RitualModelKatana)
            {
                if (!finalKatanaLoaded)
                {
                    counterKatana.gameObject.SetActive(false);
                    drawSaya.gameObject.SetActive(false);
                    counterKatana = CreateModelWeapon(false);
                    drawSaya = CreateModelWeapon(true);
                    paperSwordMesh = null;
                    var effects = new GameObject("Espíritu de la katana");
                    effects.transform.SetParent(world, false);
                    katanaSpirit = effects.AddComponent<KatanaSpiritVfx>();
                    katanaSpirit.Initialize(view);
                    finalKatanaLoaded = true;
                    katanaEquipTime = 0;
                    drawPullTarget = drawPullProgress = 0;
                    drawGripHeld = false;
                }
                UpdateKatanaPresentation(delta);
                return;
            }
            float height = view.orthographicSize * 2;
            float scale = Mathf.Min(height * .60f / 2.3f, height * view.aspect * .40f / 1.55f); drawSwordScale = scale;
            Vector3 axis = new Vector3(-.707f, .707f, 0);
            // Leave room below the guard for the full saya, including on narrow screens.
            Vector3 grip = view.ViewportToWorldPoint(new Vector3(.57f, .46f, 6));
            if (!wasPressed) { drawGripHeld = false; drawPullTarget = 0; }
            drawPullProgress = Mathf.MoveTowards(drawPullProgress, drawPullTarget, delta * (drawGripHeld ? 5 : 1.5f));
            SetPaperBladeReveal(true);
            Vector3 origin = grip - axis * (.9f * scale);
            drawSaya.gameObject.SetActive(true);
            drawSaya.SetPositionAndRotation(origin, Quaternion.Euler(0, 0, 45)); drawSaya.localScale = Vector3.one * scale;
            counterKatana.gameObject.SetActive(true); counterKatana.SetPositionAndRotation(origin + axis * (drawPullProgress * 1.5f * scale), drawSaya.rotation);
            counterKatana.localScale = Vector3.one * scale; drawHiltPosition = grip + axis * (drawPullProgress * 1.5f * scale);
        }

        // El FBX original contiene espada y funda en una sola malla, separadas en Y.
        // Se extrae la funda y se alinea su abertura con la guarda de la espada.
        // Si se sustituye el modelo, hay que revisar este criterio de separación.
        Transform CreateModelWeapon(bool saya)
        {
            var swordAsset = Resources.Load<GameObject>("Models/Weapons/katana/katana no case");
            var source = Resources.Load<GameObject>("Models/Weapons/katana/" + (saya ? "katana" : "katana no case"));
            if (source == null || swordAsset == null) throw new System.InvalidOperationException("Falta el modelo 3D de katana");
            var swordBounds = swordAsset.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
            var original = source.GetComponentInChildren<MeshFilter>().sharedMesh;
            var vertices = original.vertices;
            var selected = new System.Collections.Generic.List<int>();
            var bounds = new Bounds(); bool first = true;
            var tris = original.triangles;
            for (int i = 0; i < tris.Length; i += 3)
            {
                if (saya && (vertices[tris[i]].y >= swordBounds.min.y - .01f || vertices[tris[i + 1]].y >= swordBounds.min.y - .01f || vertices[tris[i + 2]].y >= swordBounds.min.y - .01f)) continue;
                for (int j = 0; j < 3; j++)
                {
                    int index = tris[i + j]; selected.Add(index);
                    if (first) { bounds = new Bounds(vertices[index], Vector3.zero); first = false; } else bounds.Encapsulate(vertices[index]);
                }
            }
            float unit = 2.3f / swordBounds.size.x;
            float mouth = saya ? bounds.max.x : swordBounds.max.x - swordBounds.size.x * .265f;
            float centerY = saya ? bounds.center.y : swordBounds.center.y;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i];
                vertices[i] = new Vector3(-(p.y - centerY) * unit, (p.x - mouth) * unit + PaperSayaMouth, (p.z - swordBounds.center.z) * unit);
            }
            var mesh = new Mesh { name = saya ? "Funda 3D separada" : "Katana 3D normalizada" };
            mesh.vertices = vertices; mesh.uv = original.uv; mesh.triangles = selected.ToArray(); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            var root = new GameObject(mesh.name).transform; root.SetParent(world, false);
            root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh; root.gameObject.AddComponent<SliceMeshOwner>();
            var material = art.KatanaMaterial(); if (!saya) katanaBladeMaterial = material; else katanaSayaMaterial = material;
            root.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material; return root;
        }

        void UpdateKatanaPresentation(float delta)
        {
            katanaEquipTime += delta;
            float enter = 1 - Mathf.Pow(1 - Mathf.Clamp01(katanaEquipTime / .85f), 3);
            float reveal = Mathf.SmoothStep(0, 1, (katanaEquipTime - 1.05f) / .75f) * .16f;
            float scale = view.orthographicSize * 2 * .62f / 2.3f; drawSwordScale = scale;
            if (!wasPressed) { drawGripHeld = false; drawPullTarget = reveal; }
            drawPullProgress = Mathf.MoveTowards(drawPullProgress, Mathf.Max(reveal, drawPullTarget), delta * (drawGripHeld ? 5 : 1.5f));
            float settle = Mathf.Sin(katanaEquipTime * 9) * Mathf.Exp(-katanaEquipTime * 3) * 3;
            Quaternion pose = Quaternion.Euler(0, 0, Mathf.Lerp(15, 8, enter) + settle + Mathf.Sin(katanaEquipTime * 1.8f) * .45f) * Quaternion.Euler(0, Mathf.Lerp(174, 188, enter) + Mathf.Sin(katanaEquipTime * .9f) * 3, 0);
            Vector3 axis = pose * Vector3.up;
            // Keep the marked upright pose and turn the blade around its long axis.
            Vector3 grip = view.ViewportToWorldPoint(new Vector3(.565f, .32f, 3.8f));
            grip += new Vector3((1 - enter) * scale * .6f, -(1 - enter) * scale * .95f, 0);
            grip += Vector3.up * (Mathf.Sin(katanaEquipTime * 2) * scale * .006f);
            Vector3 origin = grip - axis * (.9f * scale);
            drawSaya.gameObject.SetActive(true); drawSaya.SetPositionAndRotation(origin, pose); drawSaya.localScale = Vector3.one * scale;
            counterKatana.gameObject.SetActive(true); counterKatana.SetPositionAndRotation(origin + axis * (drawPullProgress * 1.5f * scale), pose);
            counterKatana.localScale = Vector3.one * scale; drawHiltPosition = counterKatana.TransformPoint(Vector3.up * .9f);
            SetPaperBladeReveal(true);
            Vector3 mouth = drawSaya.TransformPoint(Vector3.up * PaperSayaMouth);
            float charge = Mathf.Clamp01((drawPullProgress - .16f) / .56f);
            katanaSpirit.Sample(katanaEquipTime, mouth, counterKatana.TransformPoint(Vector3.up * PaperSayaMouth), axis, scale * 1.4f, drawPullProgress, charge);
            float reflectedSpirit = katanaSpirit.SmokeStrength * (.6f + charge * .4f);
            katanaBladeMaterial.SetFloat("_Age", katanaEquipTime);
            katanaSayaMaterial.SetFloat("_Age", katanaEquipTime);
            katanaBladeMaterial.SetFloat("_Spirit", reflectedSpirit);
            katanaSayaMaterial.SetFloat("_Spirit", reflectedSpirit);
        }
        Transform CreatePaperWeapon(string resource, bool saya)
        {
            var texture = Resources.Load<Texture2D>("Weapons/" + resource);
            if (texture == null) throw new System.InvalidOperationException("Falta ilustración: " + resource);
            var root = new GameObject(resource).transform; root.SetParent(world, false);
            var quad = art.Shape(root, resource + " recorte", PrimitiveType.Quad, Vector3.zero, Vector3.one, Color.white);
            quad.GetComponent<Renderer>().sharedMaterial = art.PaperWeaponMaterial(texture);
            Mesh mesh = new Mesh { name = resource + " plano de papel" };
            quad.GetComponent<MeshFilter>().sharedMesh = mesh; quad.AddComponent<SliceMeshOwner>();
            if (saya)
            {
                mesh.vertices = new[] { new Vector3(-.082f, -1.19f, .018f), new Vector3(.082f, -1.19f, .018f), new Vector3(-.082f, PaperSayaMouth, .018f), new Vector3(.082f, PaperSayaMouth, .018f) };
                mesh.uv = new[] { new Vector2(.414f, .004f), new Vector2(.583f, .004f), new Vector2(.414f, .97f), new Vector2(.583f, .97f) };
            }
            else { paperSwordMesh = mesh; paperBladeBottom = float.NaN; SetPaperBladeReveal(false); }
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return root;
        }
        void SetPaperBladeReveal(bool sheathing)
        {
            if (katanaBladeMaterial != null) katanaBladeMaterial.SetFloat("_BladeBottom", sheathing ? PaperSayaMouth - drawPullProgress * 1.5f : -10);
            if (paperSwordMesh == null) return;
            float bottom = sheathing ? Mathf.Max(-1.15f, PaperSayaMouth - drawPullProgress * 1.5f) : -1.15f;
            if (Mathf.Approximately(bottom, paperBladeBottom)) return;
            paperBladeBottom = bottom;
            float uvBottom = Mathf.Lerp(.006f, .995f, (bottom + 1.15f) / 2.3f);
            paperSwordVertices[0] = new Vector3(-.123f, bottom, 0); paperSwordVertices[1] = new Vector3(.123f, bottom, 0);
            paperSwordVertices[2] = new Vector3(-.123f, 1.15f, 0); paperSwordVertices[3] = new Vector3(.123f, 1.15f, 0);
            paperSwordUV[0] = new Vector2(.393f, uvBottom); paperSwordUV[1] = new Vector2(.607f, uvBottom);
            paperSwordUV[2] = new Vector2(.393f, .995f); paperSwordUV[3] = new Vector2(.607f, .995f);
            paperSwordMesh.vertices = paperSwordVertices; paperSwordMesh.uv = paperSwordUV; paperSwordMesh.RecalculateBounds();
        }
        void BeginKatanaProps()
        {
            sheathedKatana = new GameObject("Ancla de katana 3D").transform; sheathedKatana.SetParent(world, false);
            counterKatana = CreatePaperWeapon("PaperKatana", false); drawSaya = CreatePaperWeapon("PaperSaya", true);
            katanaEquipTime = 0; finalKatanaLoaded = false; katanaBladeMaterial = null; katanaSayaMaterial = null; katanaSpirit = null;
            drawSaya.gameObject.SetActive(false); drawPullTarget = drawPullProgress = 0; drawGripHeld = false;
            katanaCutTime = 0; katanaParryTime = 0; counterKatana.gameObject.SetActive(false);
        }
        void PlayKatanaCounter(Vector3 direction)
        {
            SetPaperBladeReveal(false);
            if (counterKatana != null) counterKatana.localScale = Vector3.one;
            katanaCutDirection = ((Vector2)direction).normalized;
            katanaCutTime = 0.52f;
        }

        void UpdateKatanaProps(float delta)
        {
            katanaParryTime = Mathf.Max(0, katanaParryTime - delta);
            if (drawSaya != null) drawSaya.gameObject.SetActive(bossFinalStage == 1);
            if (bossFinalStage == 1) return;
            if (katanaSpirit != null) katanaSpirit.HidePresentation();
            katanaEquipTime = 0;
            SetPaperBladeReveal(false);
            katanaCutTime = Mathf.Max(0, katanaCutTime - delta);
            if (sheathedKatana != null)
            {
                sheathedKatana.gameObject.SetActive(katanaCutTime <= 0 && bossFinalStage == 1);
                sheathedKatana.position = fighter.position + new Vector3(-0.32f, 0.9f, -0.25f);
                sheathedKatana.rotation = Quaternion.Euler(0, 0, 38);
                sheathedKatana.localScale = fighter.localScale * 0.62f;
            }
            if (counterKatana == null) return;
            if (DuelActive && duelPhase == DuelPhase.Telegraph && duelPrepared)
            {
                counterKatana.gameObject.SetActive(true);
                counterKatana.position = CounterCenter + new Vector3(duelBeat % 2 == 0 ? -.4f : .4f, -.15f, -2);
                counterKatana.rotation = Quaternion.Euler(0, 0, duelBeat % 2 == 0 ? -48 : 48);
                counterKatana.localScale = Vector3.one; return;
            }
            counterKatana.gameObject.SetActive(katanaCutTime > 0);
            if (katanaCutTime <= 0) return;
            float t = 1 - katanaCutTime / 0.52f;
            if (katanaParryTime > 0)
            {
                float age = .34f - katanaParryTime;
                float approach = Mathf.Clamp01(age / .055f);
                float rebound = Mathf.SmoothStep(0, 1, Mathf.Max(0, age - .13f) / .21f);
                counterKatana.position = CounterCenter + Vector3.back * 2 + new Vector3(Mathf.Lerp(.8f, 0, approach) + rebound * .5f, -rebound * .25f, 0);
                counterKatana.rotation = Quaternion.Euler(0, 0, Mathf.Lerp(-80, -42, approach) - rebound * 24);
                return;
            }
            float arc = Mathf.SmoothStep(0, 1, t);
            counterKatana.position = CounterCenter + katanaCutDirection * Mathf.Lerp(-2, 2, arc) + Vector3.back * 1.7f;
            float angle = Mathf.Atan2(katanaCutDirection.y, katanaCutDirection.x) * Mathf.Rad2Deg;
            counterKatana.rotation = Quaternion.Euler(0, 0, angle - 90 + Mathf.Lerp(-55, 55, arc));
        }
    }
}




