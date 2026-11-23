"""Convierte SourceAssets/Weapons/katana.glb en la katana y la funda del juego.

Uso:  D:\\Blender\\blender.exe --background --python Tools/katana_import.py
Salida (Assets/Bushido/Resources/Models/Weapons): KatanaTinta.fbx, SayaTinta.fbx, KatanaTintaAtlas.png

El GLB trae tres piezas en fila: la katana desnuda (centro), la katana enfundada y una funda suelta.
Se usa la katana desnuda y la funda de la enfundada, cada una llevada a coordenadas "de juego"
(las que usa BushidoKatana.cs) con el marco de su propio grupo, así la funda encaja con la hoja:
  x = ancho de la hoja (filo hacia -x), y = a lo largo (mango hacia +y), z = hacia la cámara
  punta y=-1.15 · guarda / boca de la funda y=0.43
En el FBX todo viaja en UV (pos = x,y · depth_steel = z,acero · nxy · nz · shade = AO,- · uv = atlas),
porque la importación de Unity refleja un eje. Las texturas de color se juntan en un atlas; los
materiales lisos ocupan una muestra de color en él.
"""
import math
import os

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "..", "SourceAssets", "Weapons", "katana.glb")
OUT = os.path.join(HERE, "..", "Assets", "Bushido", "Resources", "Models", "Weapons")
GUARD_Y, TIP_Y = 0.43, -1.15
BLADE, HANDLE, GOLD = "Material.002", "Material.006", "MY_GOLD_COLOR"
SAYA_MATERIALS = ("Material.004", "Material.007")
ATLAS, SWATCH = 2048, 64

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
depsgraph = bpy.context.evaluated_depsgraph_get()


def world_points(obj):
    return [obj.matrix_world @ v.co for v in obj.data.vertices]


def bounds(obj):
    pts = world_points(obj)
    return Vector([min(p[i] for p in pts) for i in range(3)]), Vector([max(p[i] for p in pts) for i in range(3)])


def material(obj):
    return obj.data.materials[0].name


# ---------------------------------------------------------------- grupos y marcos

groups = {"sword": [], "sheathed": [], "spare": []}
for obj in bpy.data.objects:
    if obj.type != "MESH":
        continue
    low, high = bounds(obj)
    centre = (low.x + high.x) / 2
    groups["spare" if centre < -0.3 else "sword" if centre < 0.45 else "sheathed"].append(obj)


def frame(group):
    """Origen y ejes de juego de un grupo: eje del mango, guarda y dirección del lomo de su hoja."""
    blade = next(o for o in group if material(o) == BLADE)
    handle = next(o for o in group if material(o) == HANDLE)
    tsuba = max((o for o in group if material(o) == GOLD), key=lambda o: len(o.data.polygons))
    h_low, h_high = bounds(handle)
    origin = Vector(((h_low.x + h_high.x) / 2, bounds(tsuba)[1].y, (h_low.z + h_high.z) / 2))
    pts = world_points(blade)
    top = max(p.y for p in pts)
    # Línea central de la hoja por tramos; su sección da el ancho y su flecha, el lado del filo.
    bins = [[] for _ in range(24)]
    for p in pts:
        if p.y > origin.y:
            bins[min(23, int((p.y - origin.y) / (top - origin.y) * 24))].append(p)
    bins = [b for b in bins if b]                                  # la malla no tiene vértices en todos los tramos
    centres = [sum(b, Vector()) / len(b) for b in bins]
    residual = np.array([[p.x - c.x, p.z - c.z] for b, c in zip(bins, centres) for p in b])
    _, vectors = np.linalg.eigh(residual.T @ residual)
    width = Vector((vectors[0, 1], 0, vectors[1, 1])).normalized()
    bulge = centres[len(centres) // 2] - (centres[0] + centres[-1]) / 2   # el arco se abomba hacia el filo
    gx = -width if width.dot(bulge) > 0 else width                 # +x = lomo
    gy = Vector((0, -1, 0))                                        # +y = mango
    return origin, (gx, gy, gx.cross(gy)), top - origin.y


sword_origin, sword_axes, blade_length = frame(groups["sword"])
sheath_origin, sheath_axes, _ = frame(groups["sheathed"])
SCALE = (GUARD_Y - TIP_Y) / blade_length


# ---------------------------------------------------------------- atlas

def base_colour(mat):
    """Imagen conectada al color base, o el color liso del material."""
    shader = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    socket = shader.inputs["Base Color"]
    node = socket.links[0].from_node if socket.is_linked else None
    while node is not None and node.type != "TEX_IMAGE":
        linked = [i for i in node.inputs if i.is_linked]
        node = linked[0].links[0].from_node if linked else None
    return (node.image, None) if node is not None and node.image else (None, tuple(socket.default_value)[:3])


atlas = np.zeros((ATLAS, ATLAS, 4), dtype=np.float32)
atlas[..., 3] = 1
slots, cursor, swatches = {}, 0, 0      # material -> (u0, v0, ancho, alto) en UV del atlas
image_slots = {}
for mat in bpy.data.materials:
    image, colour = base_colour(mat)
    if image is not None:
        if image.name not in image_slots:
            w, h = image.size
            atlas[ATLAS - h:, cursor:cursor + w] = np.array(image.pixels[:], dtype=np.float32).reshape(h, w, 4)
            image_slots[image.name] = (cursor / ATLAS, (ATLAS - h) / ATLAS, w / ATLAS, h / ATLAS)
            cursor += w
        slots[mat.name] = image_slots[image.name]
    else:
        x = swatches * SWATCH
        atlas[:SWATCH, x:x + SWATCH, :3] = [c ** (1 / 2.2) for c in colour]   # lineal -> sRGB
        slots[mat.name] = ((x + SWATCH / 2) / ATLAS, (SWATCH / 2) / ATLAS, 0, 0)
        swatches += 1
assert cursor <= ATLAS, "Las texturas no caben en una fila del atlas"
os.makedirs(OUT, exist_ok=True)
sheet = bpy.data.images.new("KatanaTintaAtlas", ATLAS, ATLAS, alpha=False)
sheet.pixels = atlas.ravel()
sheet.filepath_raw, sheet.file_format = os.path.join(OUT, "KatanaTintaAtlas.png"), "PNG"
sheet.save()


# ---------------------------------------------------------------- mallas

def export(objects, origin, axes, name):
    corners = []    # (posición, normal, uv, acero) por esquina de triángulo, ya en coordenadas de juego
    tiled = 0
    for obj in objects:
        mesh = obj.data
        mesh.calc_loop_triangles()
        turn = obj.matrix_world.to_3x3().inverted().transposed()
        u0, v0, du, dv = slots[material(obj)]
        steel = 1.0 if material(obj) == BLADE else 0.5
        uvs = mesh.uv_layers[0].data
        for tri in mesh.loop_triangles:
            tri_uv = [Vector(uvs[i].uv) for i in tri.loops]
            shift = Vector((math.floor(min(u.x for u in tri_uv)), math.floor(min(u.y for u in tri_uv))))
            tiled += any(u.x - shift.x > 1.001 or u.y - shift.y > 1.001 for u in tri_uv)
            for k in range(3):
                p = (obj.matrix_world @ mesh.vertices[tri.vertices[k]].co - origin) * SCALE
                n = (turn @ Vector(tri.split_normals[k])).normalized()
                uv = tri_uv[k] - shift
                corners.append((Vector((p.dot(axes[0]), p.dot(axes[1]) + GUARD_Y, p.dot(axes[2]))),
                                Vector((n.dot(axes[0]), n.dot(axes[1]), n.dot(axes[2]))),
                                (u0 + min(1.0, uv.x) * du, v0 + min(1.0, uv.y) * dv), steel))
    bm = bmesh.new()
    layers = [bm.loops.layers.uv.new(n) for n in ("pos", "depth_steel", "nxy", "nz", "shade", "uv")]
    verts = [bm.verts.new(c[0]) for c in corners]
    for i in range(0, len(verts), 3):
        try:
            bm.faces.new(verts[i:i + 3])
        except ValueError:      # triángulo degenerado
            pass
    tree = BVHTree.FromBMesh(bm)
    rays = [Vector((math.cos(a) * math.sin(b), math.sin(a) * math.sin(b), math.cos(b)))
            for a, b in ((i * 2.4, math.acos(1 - (i + 0.5) / 12)) for i in range(12))]
    occlusion = {}

    def ao(p, n):
        key = (round(p.x, 4), round(p.y, 4), round(p.z, 4), round(n.x, 1), round(n.y, 1), round(n.z, 1))
        if key not in occlusion:
            basis = n.to_track_quat("Z", "Y")
            occlusion[key] = 1 - sum(tree.ray_cast(p + n * 0.0008, basis @ d, 0.05)[0] is not None for d in rays) / len(rays)
        return occlusion[key]

    for face in bm.faces:
        for loop in face.loops:
            p, n, uv, steel = corners[loop.vert.index]
            loop[layers[0]].uv, loop[layers[1]].uv = (p.x, p.y), (p.z, steel)
            loop[layers[2]].uv, loop[layers[3]].uv = (n.x, n.y), (n.z, 0)
            loop[layers[4]].uv, loop[layers[5]].uv = (ao(p, n), 0.5), uv
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    low = [min(c[0][i] for c in corners) for i in range(3)]
    high = [max(c[0][i] for c in corners) for i in range(3)]
    print("KATANA", name, "tris", len(bm.faces), "teselados", tiled,
          "min", [round(v, 3) for v in low], "max", [round(v, 3) for v in high])
    bm.free()
    out = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(out)
    bpy.ops.object.select_all(action="DESELECT")
    out.select_set(True)
    bpy.context.view_layer.objects.active = out
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True,
                             object_types={"MESH"}, mesh_smooth_type="OFF")


export(groups["sword"], sword_origin, sword_axes, "KatanaTinta")
saya = [o for o in groups["sheathed"] if material(o) in SAYA_MATERIALS or
        (material(o) == GOLD and bounds(o)[0].y > sheath_origin.y + 0.05)]
export(saya, sheath_origin, sheath_axes, "SayaTinta")
