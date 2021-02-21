tool
extends EditorPlugin

var block_map_script := preload("res://addons/PixelDot/src/BlockMap.gd")
var block_map_icon := preload("res://addons/PixelDot/icons/BlockMap.png")

var custom_types := [
	["BlockMap", "Node2D", block_map_script, block_map_icon],
]

func _enter_tree():
	for type in custom_types:
		add_custom_type(type[0], type[1], type[2], type[3])

func _exit_tree():
	for type in custom_types:
		remove_custom_type(type[0])
