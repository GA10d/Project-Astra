"""Apply the v2 cabin material pass to an already-open editable .blend.

Run Blender with the source .blend and --python this script. No geometry changes.
Selected textures are packed into the .blend for portable editable delivery.
"""
import bpy
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TEXTURES = ROOT / "UnityProject" / "Assets" / "Astra" / "Textures"
CC0 = TEXTURES / "CC0"
IMAGES = {}


def image_asset(filename, non_color=False):
    path = TEXTURES / filename
    if not path.is_file():
        raise FileNotFoundError(f"Required cabin material asset is missing: {path}")
    key = (str(path), non_color)
    if key not in IMAGES:
        image = bpy.data.images.load(str(path), check_existing=True)
        image.colorspace_settings.name = "Non-Color" if non_color else "sRGB"
        image.pack()
        IMAGES[key] = image
    return IMAGES[key]


def box_texture(nodes, links, image, position_socket, scale, label):
    mapping = nodes.new("ShaderNodeVectorMath")
    mapping.operation = "SCALE"
    mapping.inputs[3].default_value = scale
    mapping.label = f"{scale:g} repeats / metre"
    links.new(position_socket, mapping.inputs[0])
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.extension = "REPEAT"
    tex.interpolation = "Linear"
    tex.projection = "BOX"
    tex.projection_blend = 0.16
    tex.label = label
    links.new(mapping.outputs[0], tex.inputs["Vector"])
    return tex


def update_material(name, color, metallic, roughness, detail_scale, bump_strength,
                    texture_strength=0.0):
    mat = bpy.data.materials.get(name)
    if mat is None:
        print(f"SKIP: material {name} not found")
        return
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (650, 100)
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    principled.location = (360, 100)
    principled.inputs["Base Color"].default_value = (*color, 1)
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    geometry = nodes.new("ShaderNodeNewGeometry")
    geometry.location = (-1000, 100)
    geometry.label = "Shared physical texture scale; editable geometry unchanged"

    if texture_strength > 0:
        tex = box_texture(nodes, links, image_asset("NavalPaint_Albedo.png"),
                          geometry.outputs["Position"], 1.0, "Original naval enamel color")
        tex.location = (-560, 350)
        mix = nodes.new("ShaderNodeMixRGB")
        mix.blend_type = "MIX"
        mix.inputs[0].default_value = texture_strength
        mix.inputs[1].default_value = (*color, 1)
        links.new(tex.outputs["Color"], mix.inputs[2])
        links.new(mix.outputs[0], principled.inputs["Base Color"])
        mix.location = (40, 350)

    rough = box_texture(nodes, links,
        image_asset("CC0/PaintedMetal012_2K-PNG_Roughness.png", True),
        geometry.outputs["Position"], detail_scale, "ambientCG CC0 micro-roughness")
    rough.location = (-560, -10)
    rough_mix = nodes.new("ShaderNodeMixRGB")
    rough_mix.blend_type = "MIX"
    rough_mix.inputs[0].default_value = 0.32
    rough_mix.inputs[1].default_value = (roughness, roughness, roughness, 1)
    links.new(rough.outputs["Color"], rough_mix.inputs[2])
    links.new(rough_mix.outputs[0], principled.inputs["Roughness"])
    rough_mix.location = (40, 60)

    # Height-based bump supports Blender's box projection without UV tangent seams.
    # This is a micro-surface layer, not a displacement of the authored geometry.
    height = box_texture(nodes, links,
        image_asset("CC0/PaintedMetal012_2K-PNG_Displacement.png", True),
        geometry.outputs["Position"], detail_scale, "ambientCG CC0 paint micro-relief")
    height.location = (-560, -350)
    bump = nodes.new("ShaderNodeBump")
    bump.location = (50, -260)
    bump.inputs["Strength"].default_value = bump_strength
    bump.inputs["Distance"].default_value = 0.0016
    links.new(height.outputs["Color"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], principled.inputs["Normal"])
    mat.diffuse_color = (*color, 1)
    mat["astra_material_revision"] = "v2 world-box naval enamel and CC0 micro-detail"
    print(f"UPDATED MATERIAL: {name}")


update_material("HullPaint", (0.19, 0.26, 0.23), 0.32, 0.68, 3.4, 0.24, 0.82)
update_material("DarkSteel", (0.052, 0.065, 0.062), 0.72, 0.55, 4.0, 0.17)
update_material("Brass", (0.42, 0.30, 0.13), 0.75, 0.40, 4.8, 0.12)
update_material("RedPaint", (0.34, 0.047, 0.028), 0.32, 0.62, 3.4, 0.18)
if not bpy.data.filepath:
    raise RuntimeError("Open Source/Astra_Cabin.blend before running this material-only update")
bpy.context.scene["astra_material_revision"] = "v2 naval enamel; geometry and pivots unchanged"
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
print(f"MATERIAL PASS SAVED: {bpy.data.filepath}; packed images: {len(IMAGES)}")
