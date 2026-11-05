class_name Tuning
## Every gameplay number in one place. Distances in px, times in seconds.

const TILE := 16

# Diver body (axis-aligned box centered on the diver's position)
const DIVER_SIZE := Vector2(12, 14)

# Swimming
const SWIM_ACCEL := 420.0
const SWIM_MAX := 90.0
const SWIM_DRAG := 3.0          # velocity fraction lost per second with no input
const SINK_SPEED := 28.0        # negative buoyancy: drift down at this speed unless swimming up
const SINK_ACCEL := 60.0        # how fast the drift builds
const SURFACE_JUMP_RANGE := 10.0  # how close to the surface a jump pops you out

# On dry ground
const GRAVITY := 640.0
const WALK_SPEED := 85.0
const JUMP_SPEED := 300.0       # ~4.4 tiles high: clears the 3-row inner floors of drowned towers
const FALL_MAX := 360.0

# Oxygen
const OXYGEN_DRAIN := 1.0       # per second underwater
const EEL_BITE := 10.0
const HURT_INVULN := 1.0
const KNOCKBACK := 160.0
const FAINT_KEEP := 0.5         # share of carried scrap kept after fainting (pipes and gears are rare, never lost)
const GATE_RANGE := 20.0

# Tools
const HIT_COOLDOWN := 0.25
const CRACKED_HP := 2
const RUBBLE_HP := 1
const HARD_HP := 4
const PICKUP_RANGE := 12.0
const PUMP_RANGE := 26.0

# Pump repair cost and drain
const PUMP_COST := {"scrap": 3, "pipe": 1, "gear": 1}
const DRAIN_TIME := 4.0

# Eel
const EEL_SPEED := 40.0
const EEL_SIZE := Vector2(20, 8)

# Crab (M3: only out on drained ground)
const CRAB_SPEED := 22.0
const CRAB_SIZE := Vector2(14, 10)
const CRAB_PINCH := 1           # scrap dropped per pinch
