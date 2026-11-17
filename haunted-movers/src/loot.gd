extends RigidBody3D
## One piece of scrap. On the floor the host simulates it and clients follow the synced
## transform. While a mover carries it (`holder` = their peer id) it is hidden and
## collisionless: the carrier's hand shows a copy of its model instead.

const Models = preload("res://src/models.gd")

var kind := ""
var label := ""
## Payout when it's on the truck at departure. Rolled each day by the host; synced.
var value := 0
var weight := 1.0
## Takes both hands: while it's the active item you can't pick anything else up or switch.
var two_handed := false
var size := Vector3.ONE
var color := Color.WHITE
## Out of today's mansion: hidden, collisionless, parked under the map. Host sets; synced.
var in_play := false:
	set(v):
		in_play = v
		_refresh()
## Peer id carrying it, 0 = lying somewhere. Host sets; synced.
var holder := 0:
	set(v):
		holder = v
		_refresh()


## Builds collision, mesh and network sync from one entry of tuning.json "scrap".
func setup(d: Dictionary, index: int) -> void:
	kind = d.name
	name = "%s%d" % [kind, index]
	label = d.label
	weight = d.weight
	two_handed = d.get("two_handed", false)
	color = Color(d.color)
	mass = maxf(weight, 0.5)
	size = Models.SCRAP_SIZE.get(kind, Vector3.ONE * 0.3)
	position = Vector3(0, -60, 0)
	freeze_mode = RigidBody3D.FREEZE_MODE_KINEMATIC
	var col := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	shape.size = size
	col.shape = shape
	add_child(col)
	add_child(Models.scrap(kind, color))
	var cfg := SceneReplicationConfig.new()
	for p in [".:position", ".:rotation", ".:in_play", ".:holder", ".:value"]:
		cfg.add_property(NodePath(p))
	var sync := MultiplayerSynchronizer.new()
	sync.name = "Sync"  # fixed name: auto names (@...@N) differ between peers
	sync.replication_config = cfg
	add_child(sync)
	_refresh()


## On the floor and part of today: the only state in which it's seen, touched and simulated.
func lying() -> bool:
	return in_play and holder == 0


func _refresh() -> void:
	var on := lying()
	visible = on
	collision_layer = 1 if on else 0
	collision_mask = 1 if on else 0
	if is_inside_tree() and multiplayer.is_server() and multiplayer.multiplayer_peer != null:
		freeze = not on


## Host: put it down at `at` (floor point), still.
func place(at: Vector3) -> void:
	holder = 0
	rotation = Vector3(0, rotation.y, 0)
	linear_velocity = Vector3.ZERO
	angular_velocity = Vector3.ZERO
	global_position = at + Vector3.UP * (size.y / 2.0 + 0.02)
