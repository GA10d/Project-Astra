"""Upgrade existing editable cabin needles without rebuilding the cabin.

Run:
  F:\\Blender\\blender.exe -b Source\\Astra_Cabin.blend --python Source\\upgrade_gauge_pivots.py

The original four pointers are already separate editable Blender meshes, but
were included in static FBX batches. Re-centre them on their original spindles,
retain their geometry and v2 packed materials, and make a fresh compact FBX.
One-time v2 backups are saved beside the source before the first upgrade.
"""
import bpy
import json
import math
import re
import shutil
from pathlib import Path
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Source"
MODEL_DIR = ROOT / "UnityProject" / "Assets" / "Astra" / "Models"
BLEND_PATH = SOURCE / "Astra_Cabin.blend"
FBX_PATH = MODEL_DIR / "Astra_Cabin.fbx"
MANIFEST_PATH = SOURCE / "model_manifest.json"


def unity(p):
    return [round(-p.x, 5), round(p.z, 5), round(-p.y, 5)]


def world_position(wall, u, h, d):
    a = {"A": 0, "B": math.pi / 2, "C": math.pi, "D": -math.pi / 2}[wall]
    y = -1.6 + d
    return Vector((u * math.cos(a) - y * math.sin(a), u * math.sin(a) + y * math.cos(a), h))


def material_signature():
    # No node/image mutation is needed to upgrade the pivots.
    return {
        "materials": [(m.name, len(m.node_tree.nodes) if m.node_tree else 0,
                       len(m.node_tree.links) if m.node_tree else 0) for m in bpy.data.materials],
        "packed_images": [(i.name, i.packed_file.size) for i in bpy.data.images if i.packed_file],
    }


if not bpy.data.filepath or Path(bpy.data.filepath).resolve() != BLEND_PATH.resolve():
    raise RuntimeError("Open the intended Source/Astra_Cabin.blend before running this upgrade")
if bpy.data.collections.get("EXPORT_BATCHED"):
    raise RuntimeError("Expected the editable source, not a scene containing temporary export batches")

manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8-sig"))
material_before = material_signature()
definitions = [
    ("A", "O2", -1.18, 1.88, .26, [0, 0, 1]),
    ("A", "PWR", 1.18, 1.89, .26, [0, 0, 1]),
    ("B", "TEMP", 1.065, 1.04, .318, [-1, 0, 0]),
    ("D", "PSI", .99, .89, .295, [1, 0, 0]),
]
needles = []
claimed = set()
for wall, label, u, h, d, axis in definitions:
    name = "GAUGE_" + wall + "_" + label
    expected = world_position(wall, u, h, d + .052)
    obj = bpy.data.objects.get(name)
    if obj is None:
        choices = [o for o in bpy.data.objects if o.type == "MESH"
                   and re.fullmatch(r"Gauge needle(?:\.\d+)?", o.name)
                   and o.get("astra_wall") == wall and o.name not in claimed]
        if not choices:
            raise RuntimeError("Missing editable needle for " + name)
        obj = min(choices, key=lambda o: ((o.matrix_world @ o.data.vertices[0].co) - expected).length)
        # The first six vertices form the original cylinder's spindle ring.
        spindle = sum((obj.matrix_world @ v.co for v in obj.data.vertices[:6]), Vector()) / 6
        if (spindle - expected).length > .005:
            raise RuntimeError("Unexpected pointer location for " + name)
    else:
        spindle = obj.matrix_world.translation.copy()
    if obj.parent:
        raise RuntimeError("A gauge pointer unexpectedly has a parent: " + obj.name)
    claimed.add(obj.name)
    needles.append((obj, name, wall, label, spindle, axis))

# Recoverable one-time backups; no existing backup is overwritten.
for original, backup in [(BLEND_PATH, SOURCE / "Astra_Cabin_before_gauges.blend"),
                         (FBX_PATH, SOURCE / "Astra_Cabin_before_gauges.fbx"),
                         (MANIFEST_PATH, SOURCE / "model_manifest_before_gauges.json")]:
    if original.is_file() and not backup.exists():
        shutil.copy2(original, backup)

gauges = []
max_error = 0.0
for obj, name, wall, label, spindle, axis in needles:
    old_world_vertices = [obj.matrix_world @ v.co for v in obj.data.vertices]
    target_matrix = Matrix.Translation(spindle)
    # Bake only this needle's previous transform into its mesh, then translate
    # into spindle-local coordinates. Every point remains in the same place.
    obj.data.transform(target_matrix.inverted() @ obj.matrix_world)
    obj.matrix_world = target_matrix
    obj.name = name
    obj["astra_keep_name"] = True
    obj["astra_gauge"] = label
    obj["astra_pivot"] = list(spindle)
    obj["astra_dynamic"] = ""
    bpy.context.view_layer.update()
    for previous, vertex in zip(old_world_vertices, obj.data.vertices):
        max_error = max(max_error, (previous - obj.matrix_world @ vertex.co).length)
    gauges.append({"name": name, "wall": wall, "label": label,
                   "blender_position": list(spindle), "unity_expected_position": unity(spindle),
                   "unity_world_axis": axis, "maximum_swing_degrees": 2.4})
if max_error > 0.000002:
    raise RuntimeError("Pointer geometry moved during pivot upgrade: " + str(max_error))
if material_signature() != material_before:
    raise RuntimeError("Material/image preservation check failed")

bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))

# Same export strategy/settings as build_cabin.py. Evaluate copies, then batch
# static pieces by wall/material. Never join meshes in the editable source.
objects = [o for o in bpy.context.scene.objects
           if o.type in {"MESH", "CURVE", "FONT"} and "astra_wall" in o]
export_collection = bpy.data.collections.new("EXPORT_BATCHED")
bpy.context.scene.collection.children.link(export_collection)
deps = bpy.context.evaluated_depsgraph_get()
export_objs = []
export_dynamic = {}
for item in manifest["interactions"]:
    src = bpy.data.objects[item["name"]]
    ob = bpy.data.objects.new(item["name"] + "__EXPORT", None)
    export_collection.objects.link(ob)
    ob.matrix_world = src.matrix_world.copy()
    export_dynamic[item["name"]] = ob
    export_objs.append(ob)

groups = {}
for src in objects:
    if src.get("astra_preview_only", False):
        continue
    evaluated = src.evaluated_get(deps)
    data = bpy.data.meshes.new_from_object(evaluated, preserve_all_data_layers=True, depsgraph=deps)
    if not data or not data.vertices:
        continue
    data.transform(src.matrix_world)
    data.update()
    if not data.uv_layers:
        uv = data.uv_layers.new(name="UVMap")
        for poly in data.polygons:
            major = max(range(3), key=lambda i: abs(poly.normal[i]))
            axes = [i for i in range(3) if i != major]
            for li in poly.loop_indices:
                co = data.vertices[data.loops[li].vertex_index].co
                if src.name == "SCREEN_Main":
                    uv.data[li].uv = (1 - (co.x + .825) / 1.65, (co.z - 1.092) / .876)
                else:
                    uv.data[li].uv = (co[axes[0]], co[axes[1]])
    dyn = src.get("astra_dynamic", "")
    if src.get("astra_keep_name", False):
        key = ("NAMED", src.name)
    elif dyn:
        key = ("DYNAMIC", dyn, src.get("astra_material", ""))
    else:
        key = ("STATIC", src.get("astra_wall", ""), src.get("astra_material", ""))
    ob = bpy.data.objects.new("__".join(key), data)
    export_collection.objects.link(ob)
    groups.setdefault(key, []).append(ob)

for key, batch in groups.items():
    bpy.ops.object.select_all(action="DESELECT")
    for ob in batch:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = batch[0]
    if len(batch) > 1:
        bpy.ops.object.join()
    ob = batch[0]
    if key[0] == "NAMED":
        ob.name = key[1] + "__EXPORT"
        if key[1] == "PRINT_Head":
            center = world_position("B", .08, .855, .30)
            ob.data.transform(Matrix.Translation(-center))
            ob.location = center
            export_dynamic["PRINT_Head"] = ob
        elif key[1].startswith("GAUGE_"):
            center = Vector(bpy.data.objects[key[1]]["astra_pivot"])
            ob.data.transform(Matrix.Translation(-center))
            ob.location = center
    elif key[0] == "DYNAMIC":
        ob.name = key[1] + "_" + key[2] + "__EXPORT"
    else:
        ob.name = "STATIC_" + key[1] + "_" + key[2]
    export_objs.append(ob)

for ob in export_objs:
    for dynamic_name, parent in export_dynamic.items():
        if (ob.name.startswith(dynamic_name + "_") and ob != parent
                and ob.type == "MESH" and ob.name != dynamic_name + "__EXPORT"):
            ob.parent = parent
            ob.matrix_parent_inverse = parent.matrix_world.inverted()

for ob in export_objs:
    if ob.name.endswith("__EXPORT"):
        intended = ob.name[:-8]
        existing = bpy.data.objects.get(intended)
        if existing:
            existing.name = intended + "__SOURCE"
        ob.name = intended
bpy.ops.object.select_all(action="DESELECT")
for ob in export_objs:
    ob.select_set(True)
bpy.context.view_layer.objects.active = export_objs[0]
bpy.ops.export_scene.fbx(filepath=str(FBX_PATH), use_selection=True,
    object_types={"MESH", "EMPTY"}, axis_forward="-Z", axis_up="Y",
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", global_scale=1.0,
    use_mesh_modifiers=True, use_triangles=True, mesh_smooth_type="FACE",
    bake_anim=False, add_leaf_bones=False, bake_space_transform=True,
    path_mode="AUTO", use_custom_props=True)

triangles = sum(sum(len(p.vertices) - 2 for p in o.data.polygons)
                for o in export_objs if o.type == "MESH")
if triangles != manifest["export_triangle_count"]:
    raise RuntimeError("Geometry triangle count unexpectedly changed: " + str(triangles))
manifest["gauges"] = gauges
manifest["export_object_count"] = len(export_objs)
manifest["export_triangle_count"] = triangles
note = "GAUGE_* meshes have independent spindle origins and no interaction/collision; rotate about their manifest world axes."
if note not in manifest["notes"]:
    manifest["notes"].append(note)
for path in [MANIFEST_PATH, MODEL_DIR / "model_manifest.json"]:
    path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
report = {"gauges": gauges, "max_world_vertex_error_metres": max_error,
          "packed_images_preserved": material_before["packed_images"],
          "material_nodes_unchanged": True, "export_objects": len(export_objs),
          "export_triangles": triangles}
(SOURCE / "gauge_upgrade_report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print("GAUGE UPGRADE COMPLETE " + json.dumps(report), flush=True)
