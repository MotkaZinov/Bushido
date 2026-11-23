using System.Collections.Generic;
using UnityEngine;
namespace Bushido
{
    // Pinceladas de tinta con mallas de Blender (Tools/vfx_meshes.py): tajo en media luna y anillo de choque.
    // Ambos usan BushidoVfxEnergy en modo tinta; el tajo se revela barriendo de un extremo a otro y el anillo se expande.
    public sealed partial class BushidoPrototype
    {
        sealed class EnergyBurst { public Renderer renderer; public float age, life, size, grow; public Color tint; public bool sweep; }
        readonly List<EnergyBurst> energyBursts = new List<EnergyBurst>();
        Material energyBurstMaterial;
        MaterialPropertyBlock energyBlock;

        void SpawnEnergyBurst(string meshName, Vector3 position, Quaternion rotation, float size, float grow, float life, Color tint, bool sweep)
        {
            if (energyBursts.Count >= 12) return;
            var mesh = Resources.Load<Mesh>("Models/Vfx/" + meshName);
            if (mesh == null) throw new System.InvalidOperationException("Falta la malla de VFX: " + meshName);
            // Checked apart: a script hot reload keeps the material and drops the property block.
            if (energyBurstMaterial == null) energyBurstMaterial = new Material(Resources.Load<Shader>("BushidoVfxEnergy"));
            if (energyBlock == null) energyBlock = new MaterialPropertyBlock();
            var item = new GameObject("Destello " + meshName);
            item.transform.SetParent(world, false);
            item.transform.SetPositionAndRotation(position, rotation);
            item.transform.localScale = Vector3.one * size;
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = energyBurstMaterial;
            renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 4); // the shader places vertices from uv1
            energyBursts.Add(new EnergyBurst { renderer = renderer, life = life, size = size, grow = grow, tint = tint, sweep = sweep });
            UpdateEnergyBursts(0);
        }

        // Crescent along the cut: its head runs in the swing direction.
        void SlashArcBurst(Vector3 centre, Vector3 direction, float radius, Color tint)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90;
            SpawnEnergyBurst("SlashArc", centre, Quaternion.Euler(0, 0, angle), radius, .08f, .32f, tint, true);
        }

        void ShockRingBurst(Vector3 centre, Quaternion facing, float radius, Color tint)
        {
            SpawnEnergyBurst("ShockRing", centre, facing * Quaternion.Euler(0, 0, Random.Range(0f, 360f)), radius * .25f, radius * 1.1f, .38f, tint, false);
        }

        void UpdateEnergyBursts(float delta)
        {
            if (energyBursts.Count > 0 && energyBlock == null) energyBlock = new MaterialPropertyBlock();
            for (int i = energyBursts.Count - 1; i >= 0; i--)
            {
                var burst = energyBursts[i];
                burst.age += delta;
                float t = burst.age / burst.life;
                if (t >= 1 || burst.renderer == null)
                {
                    if (burst.renderer != null) Destroy(burst.renderer.gameObject);
                    energyBursts.RemoveAt(i); continue;
                }
                float ease = 1 - (1 - t) * (1 - t);
                burst.renderer.transform.localScale = Vector3.one * (burst.size + burst.grow * ease);
                var tint = burst.tint; tint.a *= burst.sweep ? 1 : 1 - t;
                energyBlock.SetColor("_Tint", tint);
                // Sweep: the head crosses in the first 40%, the tail follows it out.
                energyBlock.SetFloat("_Reveal", burst.sweep ? Mathf.Clamp01(t / .4f) * 1.1f : 2);
                energyBlock.SetFloat("_Tail", burst.sweep ? Mathf.Clamp01((t - .3f) / .7f) * 1.1f : -1);
                energyBlock.SetFloat("_Sharp", burst.sweep ? 1.6f : 2.5f);
                energyBlock.SetFloat("_Streak", .7f);
                energyBlock.SetFloat("_Radial", 0);
                energyBlock.SetFloat("_White", 1);
                energyBlock.SetFloat("_Ink", 1);
                burst.renderer.SetPropertyBlock(energyBlock);
            }
        }

        void DisposeEnergyBursts()
        {
            foreach (var burst in energyBursts) if (burst.renderer != null) Destroy(burst.renderer.gameObject);
            energyBursts.Clear();
            if (energyBurstMaterial != null) Destroy(energyBurstMaterial);
        }
    }
}
