# Technical Documentation - Parkour Player Controller

This document describes the current implementation of the 3D parkour player
controller. It is written for someone who needs to understand the code structure,
the technical decisions, and the expected Unity setup without reading the whole
script first.

Main script:

- `Assets/PlayerController.cs`

The controller is currently implemented as one prototype-oriented MonoBehaviour.
This keeps iteration fast while the movement feel is still changing. Later, if
the mechanics stabilize, the script can be split into smaller modules such as
input, ground movement, jump logic, wall detection, wall actions, and debug
visualization.

## 1. High Level Design Goal

The movement system is designed for an arcade, fast-paced 3D platformer.

The intended feel is:

- Base movement is a run/jog, not a slow walk.
- Sprint is a faster run.
- The first jump is primarily vertical.
- Horizontal jump distance comes from existing velocity or advanced mechanics.
- Leap, super leap, wall jump, wallrun, wallslide, vertical wallrun, and edge
  climb are the main sources of advanced movement.
- Low-speed control should feel readable and responsive.
- High-speed parkour movement should preserve momentum, but not allow infinite
  speed from normal directional input.

The controller uses a Rigidbody, but it intentionally avoids relying on Unity
physics friction for important movement behavior. Most friction and steering is
implemented manually in code, because that gives more predictable arcade control.

## 2. Required Unity Components

The script requires:

```csharp
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
```

The Rigidbody is used for velocity, collision, gravity, and physics integration.
The CapsuleCollider is the player body collider.

In `Awake()`, the script does:

- Gets the Rigidbody.
- Gets the CapsuleCollider.
- Freezes Rigidbody rotation.
- Optionally applies a frictionless PhysicsMaterial to the capsule.

The Rigidbody should generally have:

- `Use Gravity`: enabled.
- `Is Kinematic`: disabled.
- Rotation constraints handled by the script through `rb.freezeRotation = true`.

## 3. Input System

The script uses Unity's newer Input System through `InputActionReference`.

Serialized input fields:

- `moveAction`
- `sprintAction`
- `jumpAction`
- `climbAction`

The action references are manually assigned in the inspector.

There is no hardcoded keyboard fallback for `E`. The climb/wall/edge action only
comes from `climbAction`.

### Input Event Flow

Input events are subscribed in `OnEnable()`:

```csharp
jumpAction.action.started += OnJumpStarted;
jumpAction.action.canceled += OnJumpCanceled;
climbAction.action.started += OnClimbStarted;
```

And unsubscribed in `OnDisable()`.

`Update()` continuously reads:

- Move vector from `moveAction`.
- Sprint pressed state from `sprintAction`.

Actual physics decisions are made in `FixedUpdate()`.

### Startup Input Grace

The field `inputEnableGraceTime` prevents accidental inputs during the first
moments after pressing Play.

The script sets:

```csharp
inputIgnoredUntil = Time.unscaledTime + inputEnableGraceTime;
```

Then `OnJumpStarted()`, `OnJumpCanceled()`, and `OnClimbStarted()` ignore input
until that time has passed.

This exists because Input System events can sometimes fire immediately when
entering Play Mode if a button is already considered pressed/released.

## 4. Cursor Handling

Fields:

- `hideCursorOnPlay`
- `playCursorLockMode`

When enabled, `ApplyCursorMode()` hides and locks the cursor on play.
`RestoreCursorMode()` restores the previous cursor state on disable.

This is useful for third-person camera testing.

## 5. State Model

The script uses two internal enums.

### MovementState

```csharp
private enum MovementState
{
    Normal,
    Rolling,
    VerticalStep,
    VerticalWallrun,
    Wallslide,
    Wallrun
}
```

This describes what movement mode is currently controlling the Rigidbody.

- `Normal`: normal ground or air movement.
- `Rolling`: short roll after landing from leap/super leap.
- `VerticalStep`: short upward wall action.
- `VerticalWallrun`: longer upward wall action.
- `Wallslide`: air state sliding along a side wall.
- `Wallrun`: horizontal wallrun along a side wall.

### AirAction

```csharp
private enum AirAction
{
    None,
    NormalJump,
    Leap,
    SuperLeap,
    WallJump,
    EdgeImpulse,
    EdgeMomentum
}
```

This remembers what put the player into the air.

It is mainly used by `HandleLanding()` to decide whether landing should start a
roll, preserve jump chain timing, or reset the chain.

For example:

- Landing from `Leap`, `SuperLeap`, or `EdgeMomentum` starts a roll.
- Landing from a normal jump opens the normal chain jump window.

## 6. Mandatory Separated Layer System

The controller enforces separate layer usage for ground, wall, and edge logic.

Serialized masks:

- `groundMask`
- `wallMask`
- `edgeMask`

The script computes exclusive masks:

```csharp
private int GroundOnlyMask => groundMask.value & ~wallMask.value & ~edgeMask.value;
private int WallOnlyMask => wallMask.value & ~groundMask.value & ~edgeMask.value;
private int EdgeOnlyMask => edgeMask.value & ~groundMask.value & ~wallMask.value;
```

This means:

- Ground detection only sees layers that exist only in `Ground Mask`.
- Wall detection only sees layers that exist only in `Wall Mask`.
- Edge detection only sees layers that exist only in `Edge Mask`.

If the same layer is accidentally added to more than one mask, it is ignored by
these exclusive checks.

### Recommended Scene Setup

Use three separate layers:

- `GroundLayer`
- `WallLayer`
- `EdgeLayer`

Recommended collider setup:

- Real floor/platform colliders:
  - Layer: `GroundLayer`
  - Trigger: false
  - Included only in `Ground Mask`

- Wall helper colliders:
  - Layer: `WallLayer`
  - Trigger: usually true
  - Included only in `Wall Mask`

- Edge helper colliders:
  - Layer: `EdgeLayer`
  - Trigger: usually true
  - Included only in `Edge Mask`

This avoids relying on complex 3D mesh collision. The level can use simple
physical colliders for ground and separate helper colliders for parkour.

## 7. Trigger Rules

Ground detection ignores triggers:

```csharp
QueryTriggerInteraction.Ignore
```

Wall and edge detection includes triggers:

```csharp
QueryTriggerInteraction.Collide
```

Reason:

- Ground should be based on real physical support.
- Wall and edge helpers can be non-physical trigger colliders.
- Trigger helpers avoid unwanted collision/friction while still being detectable.

## 8. Physics Material And Friction

The field `useFrictionlessPlayerCollider` allows the script to apply a zero
friction PhysicsMaterial to the player capsule.

If no `playerPhysicsMaterial` is assigned, `ApplyPlayerColliderMaterial()`
generates a runtime material:

```csharp
dynamicFriction = 0f;
staticFriction = 0f;
bounciness = 0f;
frictionCombine = PhysicsMaterialCombine.Minimum;
bounceCombine = PhysicsMaterialCombine.Minimum;
```

Then it assigns:

```csharp
capsule.sharedMaterial = playerPhysicsMaterial;
```

This avoids Unity collider friction killing vertical velocity when brushing
against walls.

Gameplay friction is handled manually through:

- `groundFriction`
- `rollDrag`
- `wallSlideGravityResistance`
- `wallSlideForwardDrag`
- high speed control multipliers

## 9. Main Update Loop

### Update()

Reads non-physics input:

- `moveInput = moveAction.action.ReadValue<Vector2>()`
- `isSprinting = sprintAction.action.IsPressed()`

### FixedUpdate()

Main physics tick:

1. Stores previous grounded state.
2. Calls `ProbeGround()`.
3. Handles landing transitions.
4. Processes queued climb input.
5. Processes super leap press.
6. Processes jump release.
7. Updates current movement state.
8. Updates rotation.
9. Clears queued input flags.

This design keeps Input System callbacks small. They only queue intent. The real
physics decisions happen in `FixedUpdate()`.

## 10. Ground Detection

Function:

```csharp
private void ProbeGround()
```

It builds a ground probe origin from the lower part of the capsule:

```csharp
Vector3 origin = GetBottomSphereCenter() + Vector3.up * groundProbeStartOffset;
```

Then it uses:

```csharp
SphereCastGround(origin, groundProbeRadius, Vector3.down, groundProbeDistance, GroundOnlyMask, out groundHit)
```

`SphereCastGround()` performs a downward spherecast against `GroundOnlyMask`.
It ignores triggers.

Important detail:

`TrySelectClosestWalkableHit()` does not simply accept the closest hit. It loops
through all hits and chooses the closest hit whose normal is walkable:

```csharp
Vector3.Dot(hit.normal, Vector3.up) >= groundMinUpDot
```

This prevents a nearby wall side from invalidating the floor hit.

Finally:

```csharp
isGrounded = hitGround && rb.linearVelocity.y <= groundedVerticalSpeedLimit;
```

This prevents the player from being considered grounded while moving upward too
fast.

## 11. Normal Ground Movement

Function:

```csharp
private void ApplyGroundMovement()
```

Ground movement uses target velocity, not raw additive force.

At normal speed:

```csharp
nextHorizontalVelocity = Vector3.MoveTowards(
    horizontalVelocity,
    moveDirection * maxSpeed,
    acceleration * Time.fixedDeltaTime
);
```

This means the player accelerates toward a readable run/sprint speed.

At speeds above the normal cap, such as after parkour boosts, the script does
not instantly clamp the player down. Instead it uses reduced control:

```csharp
float control = acceleration * highSpeedGroundControlMultiplier * Time.fixedDeltaTime;
nextHorizontalVelocity = Vector3.MoveTowards(horizontalVelocity, moveDirection * currentSpeed, control);
```

If this would accidentally increase speed, it clamps back to the previous
current speed.

This preserves parkour momentum while preventing normal input from generating
free speed.

When there is no movement input, coded friction is applied:

```csharp
rb.AddForce(-horizontalVelocity * groundFriction, ForceMode.Acceleration);
```

## 12. Air Movement

Function:

```csharp
private void ApplyAirMovement()
```

Air movement also avoids raw infinite acceleration.

It computes a controlled speed cap:

```csharp
float maxControlledSpeed = isSprinting
    ? maxSprintSpeed * airSprintSpeedMultiplier
    : maxRunSpeed * airRunSpeedMultiplier;
```

If current horizontal speed is below that cap, it moves toward:

```csharp
moveDirection * maxControlledSpeed
```

using:

```csharp
airAcceleration * airLowSpeedControlMultiplier
```

If current speed is above that cap:

- Opposite input can brake strongly.
- Sideways/same-direction input can steer slowly.
- Same-direction input cannot increase speed further.

This prevents holding forward in air from building infinite velocity.

Relevant fields:

- `airAcceleration`
- `airRunSpeedMultiplier`
- `airSprintSpeedMultiplier`
- `airLowSpeedControlMultiplier`
- `airSideControlMultiplier`
- `airOppositeControlMultiplier`
- `airHighSpeedSteerMultiplier`

Wall jump temporarily reduces air control through:

```csharp
wallJumpControlLockedUntil
wallJumpLockedAirControlMultiplier
```

## 13. Rotation

Function:

```csharp
private void UpdateRotation()
```

If `rotateToMovement` is enabled:

- On ground with movement input, the player rotates toward input direction.
- Otherwise, the player rotates toward horizontal velocity.

Rotation uses:

```csharp
Quaternion.Slerp(...)
```

controlled by `rotationSpeed`.

This makes low-speed movement feel responsive while still letting high-speed
parkour face travel direction.

## 14. Jump Input Design

Jump input is split by phase:

- `OnJumpStarted()` queues a press.
- `OnJumpCanceled()` queues a release and records charge.

The actual jump attempt happens in `FixedUpdate()`.

### Why Release-Based Jump?

The project uses charged jumps. If wall jump triggered on press, then starting a
charged wall jump would be impossible because pressing jump would immediately
fire the wall jump.

So:

- Normal jump: release.
- Charged jump: release.
- Wall jump: release.
- Super leap: press, but only during the special roll timing window.

## 15. Jump Charge

Function:

```csharp
private float GetCharge01()
```

It returns a 0 to 1 charge value:

```csharp
(Time.time - jumpChargeStartedAt) / maxChargeTime
```

The result is clamped with `Mathf.Clamp01`.

The charge currently affects vertical power through:

```csharp
chargedVerticalBonus
```

Normal base jump does not gain horizontal power from charge.

## 16. Ground Jump Flow

Function:

```csharp
private void TryJumpRelease(float charge)
```

Priority:

1. If not grounded and near a wall, execute wall jump.
2. If grounded or rolling, execute ground jump.
3. Otherwise, buffer the jump release for a short time.

Ground jump is routed by:

```csharp
private void ExecuteGroundJump(float charge)
```

It decides whether the jump is:

- normal jump
- chained leap

based on:

```csharp
state == MovementState.Rolling || Time.time <= chainJumpUntil
```

## 17. Normal Jump

Function:

```csharp
private void ExecuteNormalJump(float charge)
```

The normal jump only sets vertical velocity:

```csharp
Vector3 currentHorizontal = Planar(rb.linearVelocity);
Vector3 velocity = currentHorizontal;
velocity.y = normalJumpVerticalSpeed * (1f + chargedVerticalBonus * charge);
rb.linearVelocity = velocity;
```

This means:

- It preserves current horizontal velocity.
- It does not add new horizontal launch.
- It avoids pushing the player into nearby walls.

The resulting `AirAction` is:

```csharp
AirAction.NormalJump
```

## 18. Leap

Function:

```csharp
private void ExecuteLeap(float charge)
```

Leap is the first strong horizontal chained jump.

It chooses a direction using `GetJumpDirection()` and sets:

```csharp
float targetSpeed = Mathf.Max(leapHorizontalSpeed, currentHorizontal.magnitude)
    + leapChargeHorizontalBonus * charge;
```

Then:

```csharp
velocity = direction * targetSpeed;
velocity.y = leapVerticalSpeed * (1f + chargedVerticalBonus * charge);
```

Leap intentionally has strong horizontal force. This is where horizontal jump
power belongs, not in the base jump.

## 19. Roll And Super Leap

Landing from leap-like actions starts a roll:

```csharp
private void BeginRoll()
```

Roll stores:

```csharp
state = MovementState.Rolling;
stateStartedAt = Time.time;
chainJumpUntil = Time.time + rollDuration;
```

During `UpdateRoll()`:

- If the player leaves ground, roll cancels.
- Horizontal velocity is reduced by `rollDrag`.
- After `rollDuration`, the roll ends.

Super leap is checked by:

```csharp
private bool TrySuperLeap()
```

It only works when:

```csharp
state == MovementState.Rolling
IsInSuperLeapWindow()
```

The super leap timing window is:

```csharp
elapsed >= rollDuration - superLeapWindow && elapsed <= rollDuration
```

Super leap is press-only. It does not trigger from jump release.

Speed bonus:

```csharp
superLeapFirstSpeedBonus * Mathf.Pow(0.5f, superLeapChainCount)
```

Then clamped to at least:

```csharp
superLeapMinimumSpeedBonus
```

This creates diminishing returns:

- 50%
- 25%
- 12.5%
- 6.25%
- then minimum value

## 20. Parkour Action Input

Function:

```csharp
private void TryParkourAction()
```

This is called when `climbAction` is pressed.

Priority:

1. Edge climb, if valid.
2. Horizontal wallrun, if airborne and valid.
3. Vertical wall action, if facing a wall.

This ordering makes edge helpers take priority over wall actions.

## 21. Wall Detection

Wall detection uses `WallOnlyMask`.

The main wall probe functions are:

- `TryGetFrontWall(out WallContact wall)`
- `TryGetSideWall(out WallContact wall)`
- `TryKeepFrontWall(out WallContact wall)`
- `TryKeepSideWall(out WallContact wall)`
- `ProbeSideWall(int side)`

All use spherecasts and include triggers:

```csharp
QueryTriggerInteraction.Collide
```

Wall helper colliders can therefore be trigger boxes.

Wall hits must pass:

```csharp
IsWallSurface(hit.normal)
```

`IsWallSurface()` checks:

```csharp
Mathf.Abs(Vector3.Dot(normal.normalized, Vector3.up)) <= wallSurfaceMaxUpDot
```

This rejects floor-like surfaces and accepts mostly vertical surfaces.

## 22. Wall Jump

Function:

```csharp
private void ExecuteWallJump(Vector3 wallNormal, float charge)
```

Wall jump is release-based.

It computes:

- `away`: planar wall normal.
- `inputDirection`: player input projected onto wall plane.
- `tangentInfluence`: limited directional influence from input.
- `awayVelocity`: away-from-wall jump force.
- `carriedVelocity`: retained tangential momentum.

Then:

```csharp
velocity.y = wallJumpVerticalSpeed * (1f + chargedVerticalBonus * charge);
```

Wall jump uses:

- `wallJumpAwaySpeed`
- `wallJumpVerticalSpeed`
- `wallJumpChargeHorizontalBonus`
- `wallJumpInputInfluence`
- `wallJumpMomentumRetention`

After wall jump, air control is briefly reduced through:

```csharp
wallJumpControlLockedUntil
```

There is no same-wall cooldown. Same-wall climbing is controlled by physics
values, wall jump direction, gravity, and reduced control.

## 23. Vertical Step And Vertical Wallrun

Function:

```csharp
private bool TryStartVerticalWallAction()
```

It requires:

- Front wall found by `TryGetFrontWall()`.
- Player intent/facing direction sufficiently toward the wall.

It then checks horizontal speed:

```csharp
Planar(rb.linearVelocity).magnitude >= verticalWallrunMinSpeed
```

If true:

- Starts `MovementState.VerticalWallrun`.

Otherwise:

- Starts `MovementState.VerticalStep`.

### Vertical Step

Updated by:

```csharp
private void UpdateVerticalStep()
```

It uses:

```csharp
GetVerticalWallVelocity(verticalStepUpSpeed)
```

for `verticalStepDuration`.

### Vertical Wallrun

Updated by:

```csharp
private void UpdateVerticalWallrun()
```

It keeps checking the front wall.

If the wall disappears before the run ends, it applies a top impulse:

```csharp
rb.linearVelocity += Vector3.up * verticalWallrunTopImpulse
    + overWall * (verticalWallrunTopImpulse * 0.45f);
```

This is meant to help the player reach the top if the helper collider ends
before the wallrun duration finishes.

## 24. Wallslide

Auto-start check:

```csharp
private void TryAutoStartWallslide()
```

Wallslide requires:

- Not grounded.
- Normal movement state.
- Side wall found.
- `CanWallSlideOn(wall)` returns true.

`CanWallSlideOn()` requires:

- Horizontal speed above `wallSlideMinSpeed`.
- Input toward the wall or forward input.

Wallslide update:

```csharp
private void UpdateWallslide()
```

It:

- Adds upward counter-gravity using `wallSlideGravityResistance`.
- Adds stick force toward wall using `wallSlideStickAcceleration`.
- Limits falling speed to `wallSlideTerminalFallSpeed`.
- Reduces along-wall horizontal speed using `wallSlideForwardDrag`.

This is the "manual/optional wall friction" mechanic.

## 25. Horizontal Wallrun

Start function:

```csharp
private bool TryStartWallrun()
```

Requirements:

- Side wall exists.
- Horizontal speed is at least `wallRunMinStartSpeed`.
- Movement is not too perpendicular into the wall.
- `CanWallSlideOn(wall)` is true.

It computes wallrun direction by projecting current velocity onto the wall plane:

```csharp
Vector3 runDirection = Vector3.ProjectOnPlane(horizontalVelocity, wall.normal).normalized;
```

It stores:

```csharp
activeWallNormal
activeWallRunDirection
activeWallRunSpeed
```

Update function:

```csharp
private void UpdateWallrun()
```

Wallrun lasts:

```csharp
wallRunRiseTime + wallRunStraightTime
```

Current default total is 1.5 seconds.

During rise time:

```csharp
verticalSpeed = wallRunRiseSpeed
```

After the wallrun time ends, it transitions into:

```csharp
MovementState.Wallslide
```

Jumping before this transition avoids the wallslide speed loss because the
wallrun itself does not reduce `activeWallRunSpeed`.

## 26. Edge Climb

Function:

```csharp
private bool TryFindEdge(out EdgeContact edge)
```

The current implementation does not detect mesh edges.

It detects manual edge helper colliders on `EdgeOnlyMask`.

Probe:

```csharp
SphereCastFiltered(
    origin,
    edgeProbeRadius,
    forward,
    edgeProbeDistance,
    EdgeOnlyMask,
    QueryTriggerInteraction.Collide,
    out RaycastHit edgeHit
)
```

The edge helper collider transform defines climb direction:

```csharp
Vector3 climbForward = Planar(edgeHit.collider.transform.forward);
```

If `edgeHelpersAreOneWay` is false, the script can flip the climb direction
based on approach direction.

The edge contact stores:

- `topPoint`: helper collider bounds center.
- `forward`: climb-over direction.
- `wallNormal`: opposite of climb direction.

### Edge Climb Execution

Function:

```csharp
private bool TryStartEdgeClimb()
```

There are two outcomes:

### Momentum Edge Climb

Used when:

```csharp
state == MovementState.VerticalWallrun
|| rb.linearVelocity.y >= edgeMomentumVerticalSpeed
```

It converts vertical speed into forward speed:

```csharp
horizontal.magnitude + Mathf.Max(0f, rb.linearVelocity.y) * edgeVerticalToForwardMultiplier
```

Minimum speed:

```csharp
edgeMomentumMinForwardSpeed
```

Then it snaps near the edge helper and applies:

```csharp
rb.linearVelocity = edge.forward * convertedSpeed + Vector3.up * edgeMomentumUpSpeed;
```

### Impulse Edge Climb

Used otherwise.

It applies:

```csharp
forwardVelocity + Vector3.up * edgeImpulseUpSpeed
```

Forward velocity depends on whether the player is holding toward the edge.

## 27. Direction Helpers

### GetMoveDirection()

Converts `moveInput` into world-space movement.

If `movementReference` is assigned, movement is camera-relative:

```csharp
Transform reference = movementReference != null ? movementReference : transform;
```

It uses planar `forward` and `right` from the reference transform.

### GetIntentDirection()

Used for wall/edge detection.

Priority:

1. Move input direction.
2. Current horizontal velocity.
3. Player forward.

### GetJumpDirection()

Used by leap and super leap.

Priority:

1. Move input direction.
2. Current horizontal velocity.
3. Player forward.

## 28. Probe Helpers

### SphereCastFiltered()

Generic spherecast wrapper.

It calls:

```csharp
Physics.SphereCastNonAlloc(...)
```

Then selects the closest valid hit with `TrySelectClosestHit()`.

It ignores hits from:

- null colliders
- the player transform
- children of the player transform

### SphereCastGround()

Ground-specific spherecast wrapper.

It calls:

```csharp
Physics.SphereCastNonAlloc(..., QueryTriggerInteraction.Ignore)
```

Then selects the closest walkable hit with `TrySelectClosestWalkableHit()`.

### RaycastFiltered()

Currently kept as a general helper, although edge detection now uses manual edge
helper spherecasts rather than top-surface raycasts.

## 29. Gizmos

Gizmos are drawn in:

```csharp
private void OnDrawGizmosSelected()
```

They only show when the player object is selected and `showParkourGizmos` is
enabled.

Gizmo colors:

- Green: ground spherecast.
- Yellow: front wall spherecast.
- Cyan: left/right side wall spherecasts.
- Magenta: edge helper spherecast.
- Red: active wall normal while playing.

The gizmos help tune:

- `groundProbeRadius`
- `groundProbeDistance`
- `wallProbeRadius`
- `wallProbeDistance`
- `wallProbeHeightOffset`
- `edgeProbeRadius`
- `edgeProbeDistance`
- `edgeProbeHeightOffset`

## 30. Inspector Tuning Guidelines

### Ground Feel

Tune:

- `runAcceleration`
- `sprintAcceleration`
- `maxRunSpeed`
- `maxSprintSpeed`
- `groundFriction`
- `highSpeedGroundControlMultiplier`

If normal movement feels sluggish, increase `runAcceleration`.

If high-speed parkour is too controllable on ground, lower
`highSpeedGroundControlMultiplier`.

### Air Feel

Tune:

- `airAcceleration`
- `airLowSpeedControlMultiplier`
- `airSideControlMultiplier`
- `airOppositeControlMultiplier`
- `airHighSpeedSteerMultiplier`

If air feels too weak at low speed, increase `airLowSpeedControlMultiplier`.

If high-speed parkour can be redirected too easily, lower
`airHighSpeedSteerMultiplier`.

### Jump Feel

Tune:

- `normalJumpVerticalSpeed`
- `chargedVerticalBonus`
- `maxChargeTime`
- `leapVerticalSpeed`
- `leapHorizontalSpeed`
- `leapChargeHorizontalBonus`

Base jump is intentionally vertical. Do not add base horizontal jump power unless
the design changes.

### Super Leap Feel

Tune:

- `rollDuration`
- `superLeapWindow`
- `rollDrag`
- `superLeapFirstSpeedBonus`
- `superLeapMinimumSpeedBonus`

If super leap timing is too hard, increase `superLeapWindow`.

### Wall Feel

Tune:

- `wallJumpAwaySpeed`
- `wallJumpVerticalSpeed`
- `wallJumpChargeHorizontalBonus`
- `wallJumpInputInfluence`
- `wallJumpMomentumRetention`
- `wallProbeRadius`
- `wallProbeDistance`
- `wallProbeHeightOffset`

If wall jumps climb the same wall too easily, reduce vertical speed or increase
away speed so returning to the same wall costs height.

### Edge Feel

Tune:

- `edgeProbeRadius`
- `edgeProbeDistance`
- `edgeProbeHeightOffset`
- `edgeMomentumVerticalSpeed`
- `edgeVerticalToForwardMultiplier`
- `edgeMomentumMinForwardSpeed`
- `edgeImpulseUpSpeed`
- `edgeImpulseForwardSpeed`
- `edgeSnapUp`
- `edgeSnapForward`

Edge helper placement matters more than raw values. Put helpers exactly where
the move should be allowed.

## 31. Known Architectural Notes

The controller is still a prototype controller. It intentionally prioritizes:

- fast iteration
- inspector tuning
- explicit helper colliders
- predictable arcade behavior

It currently does not include:

- animation state integration
- camera controller implementation
- stamina/cooldowns
- complete same-wall prevention
- automated ledge topology detection
- multiplayer/network determinism

The important current decision is that the level designer controls parkour
validity through explicit helper layers, not through arbitrary mesh geometry.

## 32. Setup Checklist

For a new test object:

1. Create the visible mesh.
2. Add a solid ground/platform collider on `GroundLayer` if the player can stand
   on it.
3. Add separate wall helper trigger colliders on `WallLayer` where wall actions
   are allowed.
4. Add separate edge helper trigger colliders on `EdgeLayer` where edge climbs
   are allowed.
5. Assign masks on the player:
   - `Ground Mask`: only `GroundLayer`
   - `Wall Mask`: only `WallLayer`
   - `Edge Mask`: only `EdgeLayer`
6. Select the player in Scene view and enable parkour gizmos.
7. Tune probe radius/distance until the gizmos overlap helper colliders at the
   intended moments.

