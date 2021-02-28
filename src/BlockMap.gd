tool
extends Node2D

signal finished_setup
signal generated_chunk(chunk_pos)

# List of required keys for each block's properties.
const BLOCK_PROPERTIES := ["Name", "Solid", "LightAbsorb", "Emit", "EmitStrength"]
# Default properties for new block.
const TEMPLATE_PROPERTIES := {"Name" : "BlockName", "Solid" : true, "LightAbsorb" : Color(0.7,0.7,0.7,1), "Emit" : Color(), "EmitStrength" : 1.4}
# Default properteis for air.
const AIR_PROPERTIES := {"Name" : "Air", "Solid" : false, "LightAbsorb" : Color(0.85,0.85,0.85,1), "Emit" : Color(), "EmitStrength" : 1.4}


"""
Visible rect can't be accessed by tool scripts in editor
so a camera is used to preview the terrain. The active
camera is used at runtime regardless of this
variable's value.
"""
export var preview_camera: NodePath setget set_preview_cam
var preview_cam: Camera2D

# Script used for generating new chunks.
export(Script) var generator_script: Script = generator_template setget set_gen_script

# Size of each block in pixels.
export var block_size := Vector2(16, 16) setget set_block_size
# Size of each chunk in blocks.
export var chunk_size := Vector2(16, 16) setget set_chunk_size
# Additional chunks outside of camera.
export var padding := Vector2(1, 1) setget set_padding

# Array of dictionarys containg properties of each block.
# Index 0 is reserved for air.
export var block_properties: Array = [AIR_PROPERTIES] setget set_block_properties


var block_data_script: Script = preload("res://addons/PixelDot/src/BlockData.cs")
var generator_template: Script = preload("res://addons/PixelDot/templates/GeneratorTemplate.gd")


var ready := false


## --Node Structure-- ##

var data_node: Node
var generator_node: Node

func _setup():
	if data_node:
		data_node.queue_free()
	if generator_node:
		generator_node.queue_free()
	
	generator_node = Node.new()
	if generator_script:
		generator_node.set_script(generator_script)

	add_child(generator_node)
	
	data_node = Node.new()
	data_node.set_script(block_data_script)
	add_child(data_node)
	
	emit_signal("finished_setup")


## --Data-- ##

func set_block(x: int, y: int, layer: int, value: int) -> void:
	data_node.set_block(x, y, layer, value)

func get_block(x: int, y: int, layer: int) -> int:
	return data_node.get_block(x, y, layer);


## --Utility-- ##

func get_render_rect() -> Rect2:
	return data_node.get_render_rect()

func BlockPos2ChunkPos(x: int, y: int) -> Vector2:
	return Vector2(floor(x/chunk_size.x), floor(y/chunk_size.y))


## --Overrides-- ##

func _ready():
	_setup()
	ready = true

	preview_cam = get_node(preview_camera)
	data_node.preview_cam = preview_cam


## --Setters-- ##

func set_preview_cam(new):
	### Makes Sure NodePath is valid and points to Camera2D
	preview_camera = new

	if not ready:  # Node must be ready to set its property.
		yield(self, "ready")
	
	if !preview_camera:
		return
	
	if get_node(preview_camera) is Camera2D:
		preview_cam = get_node(preview_camera)
		data_node.preview_cam = preview_cam
	else:
		preview_cam = null
		data_node.preview_cam = null
		preview_camera = NodePath()

func set_block_size(new):
	block_size = new

	if not ready:  # Node must be ready to set its property.
		yield(self, "ready")
	
	data_node.block_size = block_size

func set_chunk_size(new):
	chunk_size = new

	if not ready:  # Node must be ready to set its property.
		yield(self, "ready")
	
	data_node.chunk_size = chunk_size
	data_node.reset()

func set_padding(new):
	padding = new

	if not ready:  # Node must be ready to set its property.
		yield(self, "ready")
	
	data_node.padding = padding

func set_block_properties(new):
	### Enforces Correct Formatting
	block_properties = new
	if block_properties.size() == 0:
		block_properties.append(AIR_PROPERTIES)
		return
	
	if not ready:
		return;

	var new_block_prop := []

	var i := 0
	for prop in block_properties:
		if not typeof(prop) == TYPE_DICTIONARY:  # Make sure properties are Dictionaries.
			if i == 0:
				new_block_prop.append(AIR_PROPERTIES)
			else:
				new_block_prop.append(TEMPLATE_PROPERTIES)
		
		else:  # Property is Dictionary.
			new_block_prop.append(prop)
		
		for required_key in BLOCK_PROPERTIES:  # Make sure properties has required keys.
			if not new_block_prop[i].has(required_key):
				new_block_prop[i][required_key] = TEMPLATE_PROPERTIES[required_key]
		
		i += 1
	
	block_properties = new_block_prop
	
	if not ready:
		return
	
	for child in get_children():
		if child.has_method("ComputeLighting"):
			child.BlockProp = block_properties

func set_gen_script(new):
	if new:
		generator_script = new
	else:
		generator_script = generator_template
	
	if ready:
		generator_node.set_script(generator_script)


### --Signals-- ###
func generated_chunk(chunk_pos: Vector2):
	emit_signal("generated_chunk", chunk_pos)


# Used other nodes to identify it.
func BlockMap():
	pass
