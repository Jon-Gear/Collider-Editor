# Collider Editor

A Unity Editor tool for building **fast, accurate colliders by picking mesh vertices**. Select the part of a mesh you care about in the Scene view, choose a collider type, see a live preview, and create it in one click. Stack several to build compound colliders.

<p align='center'>
  <img src='https://jon-gear.github.io/assets/images/internship-2/features/selection_drag_rectangle_add_remove_vertices.gif' width='720' alt='Selecting vertices in the Scene view'>
</p>

Developed during my tools-programming internship at [Flashgroup XR](https://flashgroupxr.com/) by [Zhangir Nurmukhambetov](https://jon-gear.github.io/).

> 📖 **Read the case study:** [how and why I built the Collider Editor](https://jon-gear.github.io/work/internship-2-short/), covering the design process, user feedback from artists, and the technical challenges behind selection, previews and collider generation.

## Why use it

Unity's built-in "Edit Collider" handles only fit-by-eye on simple shapes, and a single Mesh Collider is often too expensive or too inaccurate. Collider Editor lets you:

- Fit primitives to *exactly the vertices you select*, instead of the whole mesh's bounds
- Preview every collider before creating it
- See the physics cost of each collider at a glance (colour-coded from fastest to slowest)
- Split concave meshes into several convex hulls automatically with [CoACD](https://github.com/SarahWeiii/CoACD)

## Features

- **Vertex selection in the Scene view**: click, box-select, add/remove, select a whole connected piece, hide or isolate vertices, optional through-select for back-side vertices
- **Six collider types**: Sphere, Capsule, Box, Cylinder, Mesh From Selected Vertices (convex hull), and Convex Decomposition (CoACD)
- **Several fit methods** for spheres and capsules, plus oriented (rotated) boxes and capsules and a selectable axis for capsules and cylinders
- **Live Scene-view preview** with optional wireframe and performance colours (blue = fastest, red = slowest)
- **Generated Colliders panel**: lists every collider on the object, lets you ping or delete them, and shows them in a performance view
- **Collider settings**: Is Trigger and Physic Material applied to newly created colliders
- **Undo support** for selection changes, hiding/isolating vertices, collider creation and deletion
- Works on scene objects and inside **Prefab Mode**; generated mesh assets are saved next to the scene or prefab and cleaned up when you delete the collider

<p align='center'>
  <img src='https://jon-gear.github.io/assets/images/internship-2/features/main_ui.png' alt='The Collider Editor window'>
</p>

## Selecting vertices

Selection works like a lightweight mesh-editing mode, directly on the original model. The mesh asset is never modified.

<table>
<tr>
<td align='center' valign='top'><img src='https://jon-gear.github.io/assets/images/internship-2/features/selection_drag_rectangle_add_remove_vertices.gif' width='420' alt='Drag-select, add (Shift) and remove (Ctrl/Cmd)'><br><sub>Drag-select, add (Shift) and remove (Ctrl/Cmd)</sub></td>
<td align='center' valign='top'><img src='https://jon-gear.github.io/assets/images/internship-2/features/selection_through_select.gif' width='420' alt='Through Select: pick front and back vertices'><br><sub>Through Select: pick front and back vertices</sub></td>
</tr>
<tr>
<td align='center' valign='top'><img src='https://jon-gear.github.io/assets/images/internship-2/features/selection_hull_select.gif' width='420' alt='Double-click to select a connected piece'><br><sub>Double-click to select a connected piece</sub></td>
<td align='center' valign='top'><img src='https://jon-gear.github.io/assets/images/internship-2/features/selection_hide_vertices.gif' width='420' alt='Hide noisy vertices'><br><sub>Hide noisy vertices</sub></td>
</tr>
<tr>
<td align='center' valign='top'><img src='https://jon-gear.github.io/assets/images/internship-2/features/selection_isolate_vertices.gif' width='420' alt='Isolate the region you want'><br><sub>Isolate the region you want</sub></td>
<td></td>
</tr>
</table>

## Previewing

See what you are about to create before you commit to it, and review existing colliders afterwards.

<table>
<tr>
<td align='center' valign='top'><img src='https://jon-gear.github.io/assets/images/internship-2/features/preview_wireframe.png' width='420' alt='Editable mesh overlay'><br><sub>Editable mesh overlay</sub></td>
<td align='center' valign='top'><img src='https://jon-gear.github.io/assets/images/internship-2/features/preview_collider.png' width='420' alt='Live collider preview'><br><sub>Live collider preview</sub></td>
</tr>
<tr>
<td align='center' valign='top'><img src='https://jon-gear.github.io/assets/images/internship-2/features/preview_collider_wireframe.png' width='420' alt='Wireframe preview'><br><sub>Wireframe preview</sub></td>
<td align='center' valign='top'><img src='https://jon-gear.github.io/assets/images/internship-2/features/preview_performance_cost_sphere.png' width='420' alt='Performance colour: sphere (fastest)'><br><sub>Performance colour: sphere (fastest)</sub></td>
</tr>
<tr>
<td align='center' valign='top'><img src='https://jon-gear.github.io/assets/images/internship-2/features/preview_performance_cost_mesh_from_vertices.png' width='420' alt='Performance colour: mesh collider (slowest)'><br><sub>Performance colour: mesh collider (slowest)</sub></td>
<td align='center' valign='top'><img src='https://jon-gear.github.io/assets/images/internship-2/features/preview_existing_colliders.png' width='420' alt='Inspect existing colliders'><br><sub>Inspect existing colliders</sub></td>
</tr>
<tr>
<td align='center' valign='top'><img src='https://jon-gear.github.io/assets/images/internship-2/features/preview_existing_colliders_wireframe.png' width='420' alt='Existing colliders, wireframe'><br><sub>Existing colliders, wireframe</sub></td>
<td></td>
</tr>
</table>

## Creating colliders

<table>
<tr>
<td align='center' valign='top'><img src='https://jon-gear.github.io/assets/images/internship-2/features/colliders_box.gif' width='420' alt='Box (axis-aligned or rotated)'><br><sub>Box (axis-aligned or rotated)</sub></td>
<td align='center' valign='top'><img src='https://jon-gear.github.io/assets/images/internship-2/features/colliders_sphere.gif' width='420' alt='Sphere'><br><sub>Sphere</sub></td>
</tr>
<tr>
<td align='center' valign='top'><img src='https://jon-gear.github.io/assets/images/internship-2/features/colliders_capsule.gif' width='420' alt='Capsule'><br><sub>Capsule</sub></td>
<td align='center' valign='top'><img src='https://jon-gear.github.io/assets/images/internship-2/features/colliders_cylinder.gif' width='420' alt='Cylinder'><br><sub>Cylinder</sub></td>
</tr>
<tr>
<td align='center' valign='top'><img src='https://jon-gear.github.io/assets/images/internship-2/features/colliders_mesh_from_vertices.gif' width='420' alt='Mesh From Selected Vertices'><br><sub>Mesh From Selected Vertices</sub></td>
<td align='center' valign='top'><img src='https://jon-gear.github.io/assets/images/internship-2/features/colliders_coacd.gif' width='420' alt='Convex Decomposition (CoACD)'><br><sub>Convex Decomposition (CoACD)</sub></td>
</tr>
</table>

## Requirements

- Unity **6000.4.3f1** (the version this project was built and tested on)
- 3D physics (`BoxCollider`, `SphereCollider`, `CapsuleCollider`, `MeshCollider`)
- Convex Decomposition uses a native CoACD library. Binaries are included for **Windows** (`.dll`), **macOS** (`.dylib`) and **Linux** (`.so`)

> This repository tracks large binaries with **Git LFS**. Install [Git LFS](https://git-lfs.com/) before cloning, otherwise the CoACD libraries and icons will be placeholder files.

## Installation

**Try it first:** clone the repo and open it as a Unity project. It includes a ready-made [demo scene](#demo-scene).

**Add it to your own project:** copy this folder into your project's `Assets` folder:

```
Assets/Apollo Utilities/ColliderEditor
```

Then open the tool from **Tools > Apollo Utilities > Collider Editor**.

## Quick start

1. Open **Tools > Apollo Utilities > Collider Editor**.
2. Select a scene object that has a `MeshFilter` and click **Create Object Collider** (or **Edit Object Collider** if it already has colliders).
3. In the Scene view, select the vertices you want the collider to cover.
4. Pick a collider type in the window. A preview appears in the Scene view.
5. Click **Create Collider** (or press `Ctrl/Cmd + Enter`) and confirm the dialog.
6. Repeat with other selections to build a compound collider. Press `Esc` or **Stop Editing Collider** when you are done.

## Collider types

| Type | Creates | Cost | Good for |
|---|---|---|---|
| **Sphere** | `SphereCollider` | Fastest | Round props, pickups, rocks |
| **Capsule** | `CapsuleCollider` | Fast | Limbs, poles, handles, elongated shapes |
| **Box** | `BoxCollider` (optionally rotated) | Medium | Crates, walls, steps, most hard-surface props |
| **Cylinder** | Convex `MeshCollider` (3 to 64 sides) | Slowest | Barrels, columns, wheels |
| **Mesh From Selected Vertices** | Convex `MeshCollider` (convex hull) | Slowest | Irregular but convex shapes |
| **Convex Decomposition** | Several convex `MeshCollider`s via CoACD | Slowest | Concave shapes: rings, arches, furniture, terrain chunks |

Convex Decomposition has no live preview. The exact split is only known after CoACD runs, and it runs in the background when you press Create. Advanced CoACD settings (threshold, max hulls, resolution, MCTS parameters, seed) are in the panel.

## Controls

| Action | Input |
|---|---|
| Toggle one vertex | Click |
| Select many vertices | Drag a rectangle |
| Add to selection | `Shift` + click/drag |
| Remove from selection | `Ctrl/Cmd` + click/drag |
| Select a whole connected piece | Double-click |
| Select all visible vertices | `Ctrl/Cmd + A` |
| Hide selected vertices | `H` |
| Isolate selected vertices | `Shift + H` |
| Choose collider type | `1` Sphere, `2` Capsule, `3` Box, `4` Cylinder, `5` Mesh From Selected Vertices, `6` Convex Decomposition |
| Choose a type and create it immediately | `Ctrl/Cmd + 1` to `6` |
| Create collider (with confirmation) | `Ctrl/Cmd + Enter` |
| Undo | `Ctrl/Cmd + Z` |
| Finish editing | `Esc` |
| Orbit / pan / zoom | Normal Scene view controls (`Alt` + mouse) |

## Demo scene

`Assets/Apollo Utilities/ColliderEditor/Demo/ColliderEditorDemo.unity`

The scene contains six objects with **no colliders**, each suited to a different collider type:

| Object | Try |
|---|---|
| 1 - Stairs | Boxes on groups of steps, or one Mesh From Selected Vertices |
| 2 - Ring | Convex Decomposition (a single convex collider would fill the hole) |
| 3 - Table | A box for the top and one per leg, or Convex Decomposition |
| 4 - Pillar | Capsule, comparing the Capsule Fit methods |
| 5 - Barrel | Cylinder, adjusting the side count |
| 6 - Boulder | Sphere, comparing the Sphere Fit methods |

Press **Play** before you start and the yellow test balls fall straight through everything. Create colliders, press Play again, and the balls land on them. Step-by-step notes are also in the scene, on the `READ ME - Collider Editor Demo` object.

## Known limitations

- Cylinders and convex hulls are convex `MeshCollider`s, so they cost more than a box, sphere or capsule. Mesh From Selected Vertices needs a valid 3D point cloud; flat or very small selections can fail.
- Convex Decomposition has no live preview, depends on a native plugin, and runs one background task at a time. Cancelling is cooperative: it may not stop until the current native call returns.
- Performance colours are a simplified guide. Real physics cost also depends on scale, Rigidbody setup, layers and runtime use. Existing-collider previews cover Box, Sphere, Capsule and Mesh colliders.
- Selection scans the mesh's vertices, so very dense meshes can feel slower. Hiding vertices removes whole triangles from the preview (the mesh asset itself is untouched).
- Generated-mesh cleanup only deletes assets inside its own generated-mesh folders, so assets you move elsewhere are not cleaned up automatically.

## Project layout

```
Assets/Apollo Utilities/ColliderEditor/
├── Editor/     Window, panels, controllers, Scene-view previews   (ColliderEditor.Editor)
├── Runtime/    Collider fitting, convex hull, CoACD bindings,
│               asset persistence, shared data                      (ColliderEditor.Runtime)
│   └── CoACD/  Native libraries (Windows / macOS / Linux)
├── Icons/      Collider type icons
└── Demo/       Demo scene, meshes and materials
```

## Credits

Convex decomposition uses **CoACD**: Xinyue Wei, Minghua Liu, Zhan Ling, Hao Su, *"Approximate Convex Decomposition for 3D Meshes with Collision-Aware Concavity and Tree Search"*, SIGGRAPH 2022. See the [CoACD repository](https://github.com/SarahWeiii/CoACD) for its license terms.

## Contact

[Case study](https://jon-gear.github.io/work/internship-2-short/) · [Portfolio](https://jon-gear.github.io/) · [LinkedIn](https://linkedin.com/in/jon-gear) · [Email](mailto:zhangir.dev@gmail.com)
