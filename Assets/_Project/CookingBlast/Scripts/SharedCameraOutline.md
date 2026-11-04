# Shared camera outline (Built-in)

1. Keep the original Spine atlas material on each SkeletonRenderer.
2. Disable SpineSilhouetteOutline on skeletons using this shared effect.
3. Add CameraOutlineTarget to each GameObject containing the skeleton's
   MeshRenderer and MeshFilter (add it to the prefab for spawned animals).
4. Add SharedCameraOutline to the gameplay camera rendering those objects.
5. Assign a material using World/Custom/Screen Space Outline to Outline Style.
6. Start with Resolution Scale 0.5, Outline Width 4–6, Glow Width 0–4.
   Widths are measured in pixels at a reference screen height of 1080.
7. Save the camera/prefab with all three shader references assigned. Reset
   the camera component if these references are empty after importing.

The camera allocates one mask and two half-float seed textures, reused for
all registered targets. Mask drawing scales with submesh count; the distance
passes run once per camera. Targets outside the camera frustum are skipped.
No per-character outline textures or additional cameras are created.

This is a screen-space union: touching characters share an outer contour.
One camera uses one outline style. The mask/distance passes run before
transparent rendering. Each target gets a generated quad at the source's
Sorting Layer and Sorting Order minus one, using the shared distance texture.
The seed field carries target IDs so a quad only draws its own nearest contour.
Other sprites with higher sorting priority can cover the outline normally.
Avoid using the exact same order for unrelated renderers when their overlap
order matters. Generated quads are hidden after each camera render.
The mask does
not test opaque scene depth: this version targets a flat 2D gameplay scene,
not characters occluded by opaque 3D geometry. It supports world-space mesh
skeletons, not SkeletonGraphic UI meshes, SpriteRenderer, XR or split-screen.

Benchmark on the target device. Sharing the field removes per-character
distance passes; it does not guarantee a particular frame rate.
