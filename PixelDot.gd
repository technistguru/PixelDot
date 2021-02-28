tool
extends EditorPlugin

var block_map_script := preload("src/BlockMap.gd")
var block_map_icon := preload("icons/BlockMap.png")

var block_layer_script := preload("src/BlockLayer.cs")
var block_layer_icon := preload("icons/BlockLayer.png")

var block_fluid_script := preload("res://addons/PixelDot/src/BlockFluid.cs")
var block_fluid_icon := preload("icons/BlockFluid.png")

var block_lighting_script := preload("src/Lighting.cs")
var block_lighting_icon := preload("icons/BlockLighting.png")

var custom_types := [
	["BlockMap", "Node2D", block_map_script, block_map_icon],
	["BlockLayer", "TileMap", block_layer_script, block_layer_icon],
	["BlockFluid", "Node2D", block_fluid_script, block_fluid_icon],
	["CPU-BlockLighting", "Sprite", block_lighting_script, block_lighting_icon],
]

func _enter_tree():
	for type in custom_types:
		add_custom_type(type[0], type[1], type[2], type[3])

func _exit_tree():
	for type in custom_types:
		remove_custom_type(type[0])
