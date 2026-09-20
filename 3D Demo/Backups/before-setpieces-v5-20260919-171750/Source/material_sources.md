# Cabin material upgrade / 舱室材质来源

## Included assets

- **NavalPaint_Albedo.png**: original generated naval-enamel color asset. The primary task's `material_generation.md` records the prompt, reference, and tool provenance. It is a surface texture, not an environment photograph or painted window view.
- **PaintedMetal012** by ambientCG: <https://ambientcg.com/view?id=PaintedMetal012>.
  - Downloaded the official 2K PNG package: <https://ambientcg.com/get?file=PaintedMetal012_2K-PNG.zip>.
  - Asset license: **CC0 1.0 Universal**, including redistributable raw maps. Official license statement: <https://docs.ambientcg.com/license/>.
  - Included in `UnityProject/Assets/Astra/Textures/CC0`: Color, NormalGL, Roughness, and Displacement. Each image is 2048 × 2048 pixels. Displacement is used by the editable Blender material; the game shader uses normal and roughness.
  - Official JPEG and PNG source archives were downloaded to staging for verification. Only the selected actual maps are required to rebuild the demo. Reference preview / extra interchange formats are not game dependencies.

No third-party paid assets, Tripo credits, historical operating-system binaries, or unlicensed photographed interiors were needed for this material pass.

## Shader design

`Astra/WornMetal` uses world-space triplanar color and detail mapping. The texture scale is in repeats per metre, so the small modeled plates, pipes, instruments, and bulkheads do not stretch their textures according to arbitrary imported UV islands.

The generated color and ambientCG micro-surface maps are separate artistic layers. They are **not** represented as a registered photogrammetric PBR scan of the same real surface. Small repeated detail supplies paint microstructure; the custom color asset supplies the gray-green enamel / staining composition.

The original `_Color`, `_RustColor`, `_Wear`, `_Scale`, `_Metallic`, and `_Glossiness` controls remain compatible. Additional sparse procedural chips are deliberately low contrast and small scale. The old metre-wide round rust islands were removed.

## Recommended initial material settings

| Material | Color blend | Color repeats/m | Detail repeats/m | Detail strength | Normal depth | Extra wear |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| HullPaint | 0.82 | 1.0 | 3.4 | 0.70 | 0.24 | 0.16 |
| DarkSteel | 0 | 1.0 | 4.0 | 0.70 | 0.17 | 0.22 |
| Brass | 0 | 1.0 | 4.8 | 0.45 | 0.12 | 0.12 |
| RedPaint | 0 | 1.0 | 3.4 | 0.55 | 0.18 | 0.12 |
| Rubber / Canvas / Paper | 0 | 1.0 | 3.4 | 0 | 0 | as authored |

`_MainTex` is sRGB color; `_BumpMap` must be imported as a Unity normal map; `_RoughnessMap` must have sRGB disabled. Use mipmaps, repeat wrapping, and anisotropic filtering. `_TextureStrength` blends the image's original color with the material tint rather than multiplying both and making the hull excessively dark.

## Editable Blender material refresh

After generating the naval color texture, run:

```powershell
& 'F:\Blender\blender.exe' -b '.\Source\Astra_Cabin.blend' --python '.\Source\apply_materials.py'
```

The script changes material nodes only, preserves all mesh objects / named pivots, packs its selected texture images into the `.blend`, and saves that same `.blend`. Make a version backup before running it if the original material graph is needed. It does not re-export FBX because Unity supplies its own materials and the geometry is unchanged.
