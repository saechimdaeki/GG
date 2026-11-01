class_name Tuning
## Every gameplay number in one place. Distances in px, times in seconds.

const TILE := 16

# Diver body (axis-aligned box centered on the diver's position)
const DIVER_SIZE := Vector2(12, 14)

# Swimming
const SWIM_ACCEL := 420.0
const SWIM_MAX := 90.0
const SWIM_DRAG := 3.0          # velocity fraction lost per second with no input
const SWIM_SINK := 12.0         # slow sink while idle in water
const SURFACE_JUMP_RANGE := 10.0  # how close to the surface a jump pops you out

# On dry ground
const GRAVITY := 640.0
const WALK_SPEED := 85.0
const JUMP_SPEED := 270.0       # ~3.5 tiles high
const FALL_MAX := 360.0

# Oxygen
const OXYGEN_MAX := 60.0
const OXYGEN_DRAIN := 1.0       # per second underwater
const EEL_BITE := 10.0
const HURT_INVULN := 1.0
const KNOCKBACK := 160.0
const FAINT_KEEP := 0.5         # share of carried materials kept after fainting

# Tools
const HIT_COOLDOWN := 0.25
const CRACKED_HP := 2
const RUBBLE_HP := 1
const PICKUP_RANGE := 12.0
const PUMP_RANGE := 26.0

# Pump repair cost and drain
const PUMP_COST := {"scrap": 3, "pipe": 1, "gear": 1}
const DRAIN_TIME := 4.0

# Eel
const EEL_SPEED := 40.0
const EEL_SIZE := Vector2(20, 8)
