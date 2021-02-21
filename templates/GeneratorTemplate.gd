tool
extends Node

"""
Generator Script Template for BlockMap

Must have functions following format
_generate<Layer>(int x, int y) -> int blockID
for every layer. Layers is defined in the
layers variable in BlockMap.

This script is attached to a Node and the apporiate
generate functions are called everytime an empty chunk
enters the camera rectangle.
"""

func _ready():
	pass

### Layer 0 ###
func _generate0(x: int, y: int) -> int:
	return 0

### Layer 1 ###
func _generate1(x: int, y: int) -> int:
	return 0
