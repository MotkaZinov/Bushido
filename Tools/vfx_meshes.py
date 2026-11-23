"""Genera las mallas de VFX de Bushido (destello de la guarda).

Los cortes, la estela y los fragmentos de pantalla se construyen en tiempo de ejecución
(ScreenSlicer.cs y BushidoJudgmentCut.cs), porque dependen de por dónde pasa la katana.

Uso:  blender --background --python Tools/vfx_meshes.py
Salida: Assets/Bushido/Resources/Models/Vfx/*.fbx

Los shaders reconstruyen la posición desde los UV, así el resultado no depende
de la conversión de ejes del FBX:
  uv0 = UV del efecto      uv1 = posición XY      uv2 = centro del fragmento
  uv3 = dos valores aleatorios por fragmento
"""
import math
import os

import bmesh
import bpy

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Bushido", "Resources", "Models", "Vfx")
LAYERS = ("uv0", "pos", "pivot", "rnd")


def new_bm():
    bm = bmesh.new()
    return bm, [bm.loops.layers.uv.new(name) for name in LAYERS]


def tag(face, layers, uv_of, pivot=(0, 0), rnd=(0, 0)):
    for loop in face.loops:
        x, y = loop.vert.co.x, loop.vert.co.y
        loop[layers[0]].uv = uv_of(x, y)
        loop[layers[1]].uv = (x, y)
        loop[layers[2]].uv = pivot
        loop[layers[3]].uv = rnd


def export(bm, name):
    bmesh.ops.triangulate(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True,
                             object_types={"MESH"}, mesh_smooth_type="OFF", bake_space_transform=True)
    print("VFX", name, "tris", len(mesh.polygons))


def flare():
    """Estrella de cuatro puntas: u = 0 en el centro, 1 en las puntas."""
    bm, layers = new_bm()
    center = bm.verts.new((0, 0, 0))
    ring = [bm.verts.new((math.cos(i * math.pi / 4) * r, math.sin(i * math.pi / 4) * r, 0))
            for i, r in ((i, 1.0 if i % 2 == 0 else 0.09) for i in range(8))]
    for a, b in zip(ring, ring[1:] + ring[:1]):
        face = bm.faces.new((center, a, b))
        for loop in face.loops:
            loop[layers[0]].uv = (0.0 if loop.vert is center else 1.0, 0.5)
            loop[layers[1]].uv = (loop.vert.co.x, loop.vert.co.y)
    export(bm, "Flare")


def strip(name, point, rows=(0.0, 0.5, 1.0), segments=64):
    """Banda u x v: point(u, v) da la posición; uv0 = (u, v) para BushidoVfxEnergy (u = cabeza del barrido)."""
    bm, layers = new_bm()
    uv = {}
    grid = []
    for i in range(segments + 1):
        grid.append([bm.verts.new((*point(i / segments, v), 0)) for v in rows])
        uv.update({vert: (i / segments, v) for vert, v in zip(grid[-1], rows)})
    for i in range(segments):
        for j in range(len(rows) - 1):
            face = bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
            tag(face, layers, lambda x, y: (0, 0))  # posición; el uv0 real va a continuación
            for loop in face.loops:
                loop[layers[0]].uv = uv[loop.vert]
    export(bm, name)


def slash_arc(u, v):
    """Media luna de 150°: gruesa en el centro, afilada en los extremos; radio exterior 1."""
    angle = math.radians(75 - 150 * u)
    radius = 1 - 0.22 * math.sin(math.pi * u) ** 0.8 * (1 - v)
    return math.cos(angle) * radius, math.sin(angle) * radius


def shock_ring(u, v):
    """Anillo de radio 1 con el borde exterior dentado, como un eclipse que se rompe."""
    angle = u * 2 * math.pi
    jag = 0.035 * math.sin(u * 2 * math.pi * 23) + 0.02 * math.sin(u * 2 * math.pi * 61)
    radius = 1 + (v - 0.5) * 0.12 + jag * v
    return math.cos(angle) * radius, math.sin(angle) * radius


os.makedirs(OUT, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
flare()
strip("SlashArc", slash_arc)
strip("ShockRing", shock_ring, segments=192)
