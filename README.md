# ![logo](./assets/Logo_Outline.png)
An easy way to separate combined meshes in Unity.

## Installation
1. Download this repo as a zip
2. Copy the `Decombiner` folder into your Unity project

## Usage
1. Open the Decombiner window with `[Toolbar]/Window/Decombiner`
2. Set the options
3. Press the Decombine button

## Options
- Export Directory `[String]`
  - Where the exported sub-meshes are stored
  - Meshes are formatted as `<ExportDirectory>/<CombinedMeshName>/sub_<index>.asset`.
- Reset Pivot `[Bool]`
  - Centers the pivot of decombined meshes, if disabled the meshes have a pivot set to 0,0,0.
  - `[WARNING]`: Pivoting meshes sometimes causes major position offsets. 
- Reset Scale `[Bool]`
  - Resets the scale of the decombined renderer which fixes position offset issues, though can mess up other things.
  - `[WARNING]`: Disabling this option can lead to position offsets in the final decombined mesh.
- Fix Colliders `[Bool, Requires Reset Scale to be Enabled]`
  - Moves colliders on the root object to a child object with a corrected scale value to fix issues from Reset Scale.
  - `[WARNING]`: Disabling this option can lead to incorrectly sized colliders in the final decombined mesh if they exist.
- Fix Children `[Bool, Requires Reset Scale to be Enabled]`
  - Corrects an object's children when resetting the parent's scale.
  - `[WARNING]`: Disabling this option can lead to incorrectly sized children.
- Set Meshes Static `[Bool]`
  - Sets the final decombined meshes to static.
- Combined Renderer Action `[Enum]`
  - What happens to the original combined mesh renderer when the decombined mesh is finalized.
  - `Ignore (stay enabled)`, `Disable`, `Destroy`
- Log Actions `[Bool]`
  - Logs information between decombining actions.

## Credits
- [Submesh Separation Code](https://answers.unity.com/questions/1213025/separating-submeshes-into-unique-meshes.html)
- [Pivot Updating Code](https://web.archive.org/web/20190727035826/http://wiki.unity3d.com/index.php?title=SetPivot)