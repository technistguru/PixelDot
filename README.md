# PixelDot

Godot plugin for making 2D sandbox games like [Terraria](https://en.wikipedia.org/wiki/Terraria) and [Starbound](https://en.wikipedia.org/wiki/Starbound). This is in early development and should not be used for production projects.

[Demo](https://github.com/technistguru/PixelDot_Demo)

## Installation

Requires Godot Mono. [Documentation](https://docs.godotengine.org/en/stable/getting_started/scripting/c_sharp/c_sharp_basics.html) for using c# in Godot.

1. Create `addons/` folder in your project directory.
2. Clone this repository into the `addons/` folder.
3. Open your project in Godot and click on `Build`. You may need to create a new c# script to have this option.
4. Enable this plugin from `Project Settings -> Plugins` menu.

You should now see `BlockMap` in the `Create New Node` menu under `Node2D`.

## Usage

`BlockMap` needs 3 export variables to be set in order to work: `Generator Script`, `Layers`, and `Block Properties`. `Generator Script` is the script that is used to generate new chunks. See `PixelDot/templates/GeneratorTemplate.gd` for how you should format this. `Layers` is an Array of TileSets that will be used for rendering. `Block Properties` is an Array of Dictionaries that define the properties of each block. The first element is reserved for air.

- Add `BlockMap` to your scene.
- Update relevant export variables.
    - `Preview Camera` - The Camera2D node that is used to preview the terrain in the editor.
    - `Generator Script` - The script that is used to generate new chunks.See `PixelDot/templates/GeneratorTemplate.gd` for how you should format this.
    - `Block Size` - Size of each block in pixels.
    - `Chunk Size` - Size of each chunk in blocks.
    - `Padding` - Additional chunks outside of camera.
    - `Enable Lighting` - Whether lighting should be computed.
    - `Colored Lighting` - Whether colored lighting should be computed.
    - `Smooth Lighting` - Whether filter should be enabled in the lighting texture to smooth it.
    - `Lighting Layer` - The layer that is used for lighting computations.
    - `Ambient Light` - Light contribution from the sky.
    - `Layers` - Array of TileSets that will be used for rendering.
    - `Block Properties` - An Array of Dictionaries that define the properties of each block. The first element is reserved for air.
- If you move the `Preview Camera` around you should see the chunks loading and unloading appropriately.
