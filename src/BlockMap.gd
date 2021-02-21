tool
extends Node2D


# List of required keys for each block's properties.
const BLOCK_PROPERTIES := ["Name", "Solid", "LightFalloff", "Emit"]
# Default properties for new block.
const TEMPLATE_PROPERTIES := {"Name" : "BlockName", "Solid" : true, "LightFalloff" : 0.7, "Emit" : Color()}
# Default properteis for air.
const AIR_PROPERTIES := {"Name" : "Air", "Solid" : false, "LightFalloff" : 0.85, "Emit" : Color()}


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

### ---Light-- ###
export var enable_lighting := true setget set_enable_lighting
export var colored_lighting := true setget set_col_light
export var smooth_lighting := true setget set_smooth_light
export var lighting_layer := 0 setget set_lighting_layer
export(Color, RGB) var ambient_light := Color.white

### ---TileMap--- ###
export var layers: Array = [TileSet.new()] setget set_layers
# Array of dictionarys containg properties of each block.
# Index 0 is reserved for air.
export var block_properties: Array = [AIR_PROPERTIES] setget set_block_properties

### ---Code and Shader Files--- ###
var block_layer_script: Script = preload("res://addons/PixelDot/src/BlockLayer.cs")
var block_data_script: Script = preload("res://addons/PixelDot/src/BlockData.cs")
var lighting_script: Script = preload("res://addons/PixelDot/src/Lighting.cs")
var lighting_shader: Shader = preload("res://addons/PixelDot/shaders/lighting.shader")
var generator_template: Script = preload("res://addons/PixelDot/templates/GeneratorTemplate.gd")


var ready := false


## --Node Structure-- ##

var layer_nodes := []
var lighting_node: Sprite
var data_node: Node
var generator_node: Node

func _setup():
	for node in layer_nodes:
		node.queue_free()
	layer_nodes = []
	if lighting_node:
		lighting_node.queue_free()
	if data_node:
		data_node.queue_free()
	if generator_node:
		generator_node.queue_free()
	
	for layer in layers:
		var node = TileMap.new()
		node.tile_set = layer
		node.cell_size = block_size
		node.set_script(block_layer_script)
		add_child(node)
		layer_nodes.append(node)
	
	if enable_lighting:
		lighting_node = Sprite.new()
		lighting_node.set_script(lighting_script)
		lighting_node.material = ShaderMaterial.new()
		lighting_node.material.shader = lighting_shader
		add_child(lighting_node)
	
	data_node = Node.new()
	data_node.set_script(block_data_script)
	add_child(data_node)
	
	generator_node = Node.new()
	if generator_script:
		generator_node.set_script(generator_script)
	add_child(generator_node)


## --Lighting-- ##

func update_lighting():
	var render_rect := get_render_rect()
	
	lighting_node._update_data(render_rect, block_size, lighting_layer, ambient_light)


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
	ready = true
	preview_cam = get_node(preview_camera)
	data_node.preview_cam = preview_cam

func _process(_delta):
	if enable_lighting:
		lighting_node._update_data(get_render_rect(), block_size, lighting_layer, ambient_light)


## --Setters-- ##

func set_preview_cam(new):
	### Makes Sure NodePath is valid and points to Camera2D
	if !ready:
		preview_camera = new
		return
	
	preview_camera = new
	if !preview_camera:
		return
	
	if get_node(preview_camera) is Camera2D:
		preview_cam = get_node(preview_camera)
		data_node.preview_cam = preview_cam
	else:
		preview_cam = null
		data_node.preview_cam = null
		preview_camera = NodePath()

func set_enable_lighting(new):
	enable_lighting = new
	if enable_lighting:
		_setup()
	else:
		lighting_node.queue_free()

func set_col_light(new):
	colored_lighting = new
	if not ready:  # Node must be ready to set its property.
		yield(self, "ready")
	lighting_node.COLORED = new

func set_smooth_light(new):
	smooth_lighting = new
	if not ready:  # Node must be ready to set its property.
		yield(self, "ready")
	lighting_node.FILTER = new

func set_block_size(new):
	block_size = new
	if not ready:  # Node must be ready to set its property.
		yield(self, "ready")
	data_node.set_block_size(new)

func set_chunk_size(new):
	chunk_size = new
	if not ready:  # Node must be ready to set its property.
		yield(self, "ready")
	data_node.set_chunk_size(new)

func set_padding(new):
	padding = new
	if not ready:  # Node must be ready to set its property.
		yield(self, "ready")
	data_node.set_padding(new)

func set_lighting_layer(new):
	### Enforces valid layer id
	lighting_layer = new
	if not ready: return
	lighting_layer = clamp(lighting_layer, 0, layers.size()-1)

func set_layers(new):
	layers = new
	
	if layers.size() == 0:
		layers.append(TileSet.new())
		return
	
	var new_layers := []
	var i := 0
	for layer in layers:
		if layer is TileSet:
			new_layers.append(layer)
		else:
			new_layers.append(TileSet.new())
	
	layers = new_layers
	_setup()  # Tilesets must be recreated.
	data_node.set_layers(layers.size())

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
	lighting_node.BlockProp = block_properties

func set_gen_script(new):
	if new:
		generator_script = new
	else:
		generator_script = generator_template
