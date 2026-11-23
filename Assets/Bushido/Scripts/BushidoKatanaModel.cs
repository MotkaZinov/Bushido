using System.Collections.Generic;
using UnityEngine;
namespace Bushido
{
    // Katana y funda 3D, convertidas desde SourceAssets/Weapons/katana.glb (Tools/katana_import.py)
    // ya en coordenadas de la espada: punta y=-1.15, guarda / boca de la funda y=0.43.
    public sealed partial class BushidoPrototype
    {
        // Each call returns a new mesh: SliceMeshOwner destroys it with its object.
        static Mesh LoadWeaponMesh(string name)
        {
            var source = Resources.Load<Mesh>("Models/Weapons/" + name);
            if (source == null) throw new System.InvalidOperationException("Falta el modelo 3D: " + name);
            // The FBX carries everything in UVs, because Unity mirrors an axis on import:
            // position xy · z and steel flag · normal xy · normal z · baked occlusion · atlas uv.
            var channels = new List<Vector2>[6];
            for (int i = 0; i < channels.Length; i++)
            {
                channels[i] = new List<Vector2>(); source.GetUVs(i, channels[i]);
                if (channels[i].Count != source.vertexCount) throw new System.InvalidOperationException("El modelo " + name + " no trae los UV de Blender");
            }
            int count = source.vertexCount;
            var vertices = new Vector3[count]; var normals = new Vector3[count]; var colors = new Color[count];
            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(channels[0][i].x, channels[0][i].y, channels[1][i].x);
                normals[i] = new Vector3(channels[2][i].x, channels[2][i].y, channels[3][i].x);
                colors[i] = new Color(1, 1, 1, channels[1][i].y); // alpha 1 = blade steel, the only part that carries energy
            }
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices; mesh.normals = normals; mesh.colors = colors;
            mesh.SetUVs(0, channels[4]); mesh.SetUVs(1, channels[5]); mesh.triangles = source.triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        // The blade is an arc, and leans a little in z: it only fits its saya if it slides along that arc.
        // Centre and radius of the blade's centre line and its lean per unit of length, measured on KatanaTinta;
        // measure them again if the model is replaced.
        static readonly Vector3 BladeArcCentre = new Vector3(9.834f, .317f, 0);
        const float BladeArcRadius = 9.833f, BladeLean = -.048f;

        // Places the katana drawn out of its saya by `slide` (in the weapon's own units; 0 = fully sheathed).
        static void SheathSlide(Transform saya, Transform katana, float slide)
        {
            Quaternion turn = Quaternion.Euler(0, 0, -slide / BladeArcRadius * Mathf.Rad2Deg);
            katana.SetPositionAndRotation(saya.TransformPoint(BladeArcCentre - turn * BladeArcCentre + Vector3.forward * (BladeLean * slide)), saya.rotation * turn);
            katana.localScale = saya.localScale;
        }

        static Texture2D WeaponAtlas()
        {
            var atlas = Resources.Load<Texture2D>("Models/Weapons/KatanaTintaAtlas");
            if (atlas == null) throw new System.InvalidOperationException("Falta el atlas de la katana");
            return atlas;
        }
    }
}
