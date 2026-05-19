# Philips Azurion 7 C20 with FlexArm — Joint & Movement Specification
## For Unity URDF Pose Randomization

> **Purpose:** This document specifies every kinematic degree of freedom of the Azurion 7 FlexArm ceiling-mounted C-arm system, with concrete ranges and types, so that a Claude Code instance can implement plausible random pose generation in Unity from a URDF model.

---

## 1. System Overview & Kinematic Architecture

The Azurion 7 C20 with FlexArm is a **ceiling-mounted monoplane** interventional X-ray system. It operates on **8 coordinated axes** controlled by a single "Axsys" kinematic engine. The system consists of two independent kinematic sub-chains:

1. **The FlexArm + Gantry chain** (ceiling → C-arm end-effector): 6 axes
2. **The Patient Table chain** (floor-mounted, independent): 2–5 axes depending on options

The isocenter (the point in space the C-arm rotates around) sits at **106.5 cm above the floor**.

### Physical Dimensions (for collision/boundary checking)
- Room ceiling height: **2700 mm (preferred 2900 mm)**, minimum 2300 mm
- C-arm depth (throat): **90 cm** (distance from isocenter to the back of the C-arm)
- Gantry front-view width: **~900 mm**
- Gantry front-view height: **~1687 mm** (from ceiling rail to bottom of tube housing)
- Focal spot to isocenter: **81 cm**
- Source-to-Image Distance (SID): **89.5–119.5 cm** (adjustable, prismatic)

---

## 2. FlexArm + Gantry Kinematic Chain (Ceiling to C-arm)

The chain starts at the ceiling rail and ends at the flat detector/X-ray tube. Reading from ceiling downward:

### Joint 1: Ceiling Rail Longitudinal Travel (Prismatic)
- **Type:** `prismatic` (linear translation along ceiling rail, parallel to table long axis)
- **Axis:** Along the patient table's longitudinal axis (let's call this **Y** in Unity world space if Y is head-to-toe)
- **Range:** Depends on rail configuration:
  - Standard: **285 cm** total travel
  - Extended: **455 cm** total travel  
  - Maximum: **635 cm** total travel
- **Recommended for randomization:** Use **±142.5 cm** from center (285 cm config) or pick whichever matches your URDF rail length. Express as `[-1.425, +1.425]` meters.
- **Speed:** up to 15 cm/s
- **Snap positions:** Park, Cardio, Neuro, Lower Peripheral (you can ignore these for randomization, they're just presets)

### Joint 2: FlexArm Rotation (Revolute — vertical axis)
- **Type:** `revolute` (rotation about a **vertical axis** descending from the ceiling carriage)
- **Axis:** Vertical (Z-up or Y-up depending on your Unity convention)
- **Range:** **270°** total, with snap positions at 135°, 90°, 0°, −90°, −135°
- **Recommended for randomization:** `[-135°, +135°]` centered on the 0° (head-end) position. In radians: `[-2.356, +2.356]`
- **Effect:** This swings the entire gantry arm around the table — the main mechanism that allows access from the head, left side, and right side. This is the dominant "which side of the table the C-arm is on" joint.
- **Transversal displacement effect:** The FlexArm rotation creates an effective transversal (lateral) movement range of **236 cm** (92.9 inches). This is NOT a separate prismatic joint — it's an effect of the revolute joint swinging the arm at a radius.

### Joint 3: L-arm Rotation / Propeller (Revolute — longitudinal horizontal axis)
- **Type:** `revolute` (rotation about a **horizontal axis** parallel to the ceiling rail / patient long axis)
- **Axis:** Horizontal, along the longitudinal (head-to-toe) direction
- **Range:** **180°** total, with snap positions at +90°, 0°, −90°
- **Recommended for randomization:** `[-90°, +90°]` → in radians `[-1.571, +1.571]`
- **Effect:** This "propeller" rotation flips the C-arm plane. At 0° the C-arm hangs straight down (AP view). At ±90° the C-arm is oriented laterally. This is what allows switching between the C-arm opening facing left vs. right vs. down.
- **Note:** On the standard ceiling model (non-FlexArm), this same joint is called the "L-arm rotation" and has 180° with snaps at 90°, 0°, −90°.

### Joint 4: C-arm Rotation / RAO-LAO (Revolute)
- **Type:** `revolute` (rotation of the C-arc within its plane, producing Left/Right Anterior Oblique views)
- **Axis:** Perpendicular to the C-arm plane, roughly the cranio-caudal axis when in head position
- **Range (position-dependent):**
  - **In head-end position:** 120° LAO + 185° RAO = **total ~305°** → `[-120°, +185°]`
  - **In side position:** 90° LAO + 90° RAO = **total 180°** → `[-90°, +90°]`
- **Recommended for randomization:** Use the head-end range as the superset: `[-120°, +185°]` but **clamp based on Joint 2 position**. If Joint 2 places the gantry in a side position (|FlexArm angle| > ~45°), clamp to `[-90°, +90°]`.
- **In radians (head-end):** `[-2.094, +3.228]`
- **Speed:** up to 25°/s normal, up to 55°/s for rotational angiography

### Joint 5: C-arm Angulation / Cranial-Caudal (Revolute)
- **Type:** `revolute` (tilts the C-arm to produce cranial/caudal angulation)
- **Axis:** Perpendicular to the rotation axis, roughly the lateral axis
- **Range (position-dependent):**
  - **In head-end position:** 90° cranial + 90° caudal = **180° total** → `[-90°, +90°]`
  - **In side position:** 185° cranial + 120° caudal = **305° total** → `[-120°, +185°]`
- **Recommended for randomization:** Use `[-90°, +90°]` as conservative default. If Joint 2 indicates side position, can extend to `[-120°, +185°]`.
- **In radians (conservative):** `[-1.571, +1.571]`
- **Speed:** up to 25°/s

### Joint 6: Source-Image Distance / SID (Prismatic)
- **Type:** `prismatic` (linear extension/retraction of the detector relative to the tube along the C-arm's beam axis)
- **Axis:** Along the X-ray beam axis (radial from isocenter)
- **Range:** **89.5 cm to 119.5 cm** SID → that's a **30 cm** travel range
- **Recommended for randomization:** `[0.895, 1.195]` meters as absolute SID, or as prismatic offset: `[0.0, 0.30]` meters from minimum position
- **Effect:** Moves the flat detector closer/farther from the X-ray tube, changing magnification

### Joint 7 (optional): Flat Detector Rotation (Revolute — about beam axis)
- **Type:** `revolute` (rotates the flat detector between portrait and landscape orientation)
- **Axis:** Along the X-ray beam axis
- **Range:** **90°** (portrait ↔ landscape switch, completes in ~3 seconds)
- **Recommended for randomization:** Discrete: `{0°, 90°}` or continuous `[0°, 90°]`
- **Note:** This may or may not be a separate URDF joint. If your URDF has it, randomize discretely between 0° and 90°.

### Joint 8 (derived/virtual): Image Beam Auto-Rotation
- **NOT a physical joint to randomize.** The system's kinematic engine automatically rotates the collimation/image to keep the image "heads-up" aligned with the patient regardless of gantry position. This is handled in software, not a separate mechanical axis. Your URDF likely doesn't have this as a joint.

---

## 3. Patient Table Kinematic Chain (Independent from Gantry)

The table is floor-mounted and moves independently. It has its own joints:

### Table Joint A: Table Height (Prismatic — vertical)
- **Type:** `prismatic`
- **Axis:** Vertical (up/down)
- **Range:** **74 cm to 102 cm** from floor → **28 cm travel**
- **Recommended for randomization:** `[0.74, 1.02]` meters absolute height
- **Speed:** 3 cm/s

### Table Joint B: Tabletop Longitudinal Float (Prismatic)
- **Type:** `prismatic` (tabletop slides head-to-foot relative to the table base)
- **Axis:** Longitudinal (along patient head-to-toe axis)
- **Range:** **120 cm** total travel → `[-0.60, +0.60]` meters from center
- **Tabletop length:** 319 cm

### Table Joint C: Tabletop Lateral Float (Prismatic)
- **Type:** `prismatic` (tabletop slides left-right)
- **Axis:** Lateral (perpendicular to patient long axis, horizontal)
- **Range:** **36 cm** total travel → `[-0.18, +0.18]` meters from center

### Table Joint D: Tabletop Pivot (Revolute — optional)
- **Type:** `revolute` (rotates entire tabletop about a vertical axis near one end)
- **Axis:** Vertical
- **Range:** **−90° / +180°** (or **−180° / +90°** depending on configuration)
- **Recommended for randomization:** If present in URDF, use `[-90°, +180°]` → `[-1.571, +3.142]` rad
- **Note:** This is an **optional** feature. If your URDF doesn't have it, skip.

### Table Joint E: Tabletop Tilt (Revolute — optional)
- **Type:** `revolute` (tilts the tabletop about the longitudinal axis, Trendelenburg/reverse Trendelenburg)
- **Axis:** Longitudinal horizontal
- **Range:** **±17°** isocentric → `[-0.297, +0.297]` rad
- **Note:** Optional. Raises table height by +4 cm min when active.

### Table Joint F: Tabletop Cradle (Revolute — optional)
- **Type:** `revolute` (lateral tilt/roll of the tabletop)
- **Axis:** Longitudinal horizontal (same axis as tilt but independent cradle mechanism)
- **Range:** **±15°** → `[-0.262, +0.262]` rad
- **Note:** Optional. Only present if Tilt & Cradle option is installed.

---

## 4. Summary Table for Implementation

| # | Joint Name | Type | Axis Description | Min | Max | Unit | Required? |
|---|-----------|------|-----------------|-----|-----|------|-----------|
| 1 | ceiling_rail_longitudinal | prismatic | Along table long axis (Y) | -1.425 | +1.425 | m | Yes |
| 2 | flexarm_rotation | revolute | Vertical axis from ceiling | -135 | +135 | deg | Yes |
| 3 | larm_propeller | revolute | Horizontal, along longitudinal | -90 | +90 | deg | Yes |
| 4 | carm_rotation_rao_lao | revolute | C-arm arc rotation (LAO/RAO) | -120 | +185 | deg | Yes |
| 5 | carm_angulation_cran_caud | revolute | C-arm tilt (Cranial/Caudal) | -90 | +90 | deg | Yes |
| 6 | sid_extension | prismatic | Along X-ray beam axis | 0.895 | 1.195 | m | Yes |
| 7 | detector_rotation | revolute | About beam axis (portrait/landscape) | 0 | 90 | deg | Optional |
| A | table_height | prismatic | Vertical | 0.74 | 1.02 | m | Yes |
| B | tabletop_longitudinal | prismatic | Along table long axis | -0.60 | +0.60 | m | Yes |
| C | tabletop_lateral | prismatic | Lateral horizontal | -0.18 | +0.18 | m | Yes |
| D | tabletop_pivot | revolute | Vertical axis | -90 | +180 | deg | Optional |
| E | tabletop_tilt | revolute | Longitudinal horizontal | -17 | +17 | deg | Optional |
| F | tabletop_cradle | revolute | Longitudinal horizontal | -15 | +15 | deg | Optional |

---

## 5. Constraints & Rules for Plausible Pose Randomization

These constraints ensure the randomly generated poses are physically plausible and the robot doesn't self-collide, break apart, or clip through the room.

### 5.1 Position-Dependent Range Coupling (CRITICAL)

The C-arm rotation and angulation ranges **depend on the FlexArm rotation position**:

```
IF abs(flexarm_rotation) <= 30°:    # "head-end position"
    carm_rotation_rao_lao  ∈ [-120°, +185°]
    carm_angulation         ∈ [-90°, +90°]
ELSE:                                # "side position"  
    carm_rotation_rao_lao  ∈ [-90°, +90°]
    carm_angulation         ∈ [-120°, +185°]
```

This is because the rotation and angulation axes effectively swap roles as the FlexArm swings the gantry from head-end to side. The wider range always applies to the axis that is closer to horizontal.

### 5.2 Room Boundary Constraints

- The C-arm must not penetrate the **floor** (z = 0). Given isocenter height ~106.5 cm and C-arm depth ~90 cm, extreme angulations at low table heights could cause floor clipping. After generating a pose, verify:
  ```
  lowest_point_of_carm_z > 0.0  (floor)
  highest_point_of_carm_z < ceiling_height  (typically 2.9 m)
  ```
- The room is typically **~6m × 6m** minimum. Ensure the C-arm's swept volume stays within room bounds.

### 5.3 Table-Gantry Collision Avoidance

- The system has an "intelligent Collision Prevention" (iCP) system that maintains **≥2 cm clearance** between the X-ray tube housing and the tabletop.
- In randomization, after computing FK (forward kinematics), check that the distance between the X-ray tube link and the tabletop surface is > 2 cm.
- The flat detector (top of C-arm) has a "BodyGuard" capacitive collision sensor — it should never overlap with the patient or table either.

### 5.4 FlexArm Radius and Lateral Extent

- The FlexArm creates a **transversal sweep of 236 cm** (±118 cm from table centerline). This means at extreme FlexArm rotation angles, the gantry is displaced ~1.18 m laterally from the table center.
- The **off-center imaging range** is 118 cm on either side of the table.
- Ensure the gantry base doesn't exit the room when at extreme FlexArm rotation + longitudinal rail positions.

### 5.5 Rotational Angiography Mode (Special — typically NOT randomized)

For 3D rotational scans the C-arm spins up to **240° at 55°/s** in head position (40°/s in side position on FlexArm). This is a dynamic sweep, not a static pose — **do not use these as static limits**. The static positioning limits above are what you want.

### 5.6 Table Independence

The table and gantry are **physically independent** kinematic chains. You can randomize them independently, but must check for inter-chain collisions afterward.

### 5.7 Recommended Randomization Strategy

```python
def generate_random_pose():
    # 1. Randomize table first (it's simpler)
    table_height       = uniform(0.74, 1.02)        # meters
    table_longitudinal = uniform(-0.60, 0.60)        # meters
    table_lateral      = uniform(-0.18, 0.18)        # meters
    table_pivot        = uniform(-90, 180) if HAS_PIVOT else 0  # degrees
    table_tilt         = uniform(-17, 17)  if HAS_TILT  else 0  # degrees
    table_cradle       = uniform(-15, 15)  if HAS_CRADLE else 0 # degrees
    
    # 2. Randomize gantry — order matters for coupling
    rail_longitudinal  = uniform(-1.425, 1.425)      # meters
    flexarm_rotation   = uniform(-135, 135)           # degrees
    larm_propeller     = uniform(-90, 90)             # degrees
    
    # 3. Apply position-dependent coupling
    if abs(flexarm_rotation) <= 30:  # head-end
        carm_rotation  = uniform(-120, 185)           # degrees
        carm_angulation= uniform(-90, 90)             # degrees
    else:                            # side
        carm_rotation  = uniform(-90, 90)             # degrees
        carm_angulation= uniform(-120, 185)           # degrees
    
    sid = uniform(0.895, 1.195)                       # meters
    detector_rot = choice([0, 90]) if HAS_DET_ROT else 0  # degrees
    
    # 4. Compute forward kinematics → check collisions
    # 5. If collision detected → resample (rejection sampling)
    #    OR clamp offending joints
    
    return pose
```

### 5.8 Conservative Ranges (if you want guaranteed no-collision without FK checks)

If implementing full collision detection is too complex initially, use these **conservative** ranges that virtually guarantee no self-intersection:

| Joint | Conservative Min | Conservative Max |
|-------|-----------------|-----------------|
| ceiling_rail_longitudinal | -1.0 m | +1.0 m |
| flexarm_rotation | -90° | +90° |
| larm_propeller | -45° | +45° |
| carm_rotation | -90° | +90° |
| carm_angulation | -45° | +45° |
| sid_extension | 0.95 m | 1.15 m |
| table_height | 0.78 m | 0.98 m |
| tabletop_longitudinal | -0.40 m | +0.40 m |
| tabletop_lateral | -0.12 m | +0.12 m |
| tabletop_tilt | -10° | +10° |
| tabletop_cradle | -10° | +10° |

---

## 6. URDF Joint Name Mapping Guidance

Your URDF file may use different joint names. Here is how to identify which URDF joint maps to which specification joint:

1. **Look at the joint axis and parent/child links.** The topmost joint (attached to world/ceiling) with a translational axis along the table is Joint 1 (rail).
2. **The first revolute joint below the rail** with a vertical axis is Joint 2 (FlexArm rotation).
3. **The next revolute** with a horizontal longitudinal axis is Joint 3 (L-arm/propeller).
4. **Two revolute joints in sequence** near the C-arc are Joint 4 (rotation/RAO-LAO) and Joint 5 (angulation/cran-caud). Distinguish them by their axes — one produces LAO/RAO views, the other produces cranial/caudal.
5. **A prismatic joint along the beam axis** is Joint 6 (SID).
6. **Table joints** will be on a separate kinematic chain rooted at the floor.

---

## 7. Sources

- Philips Azurion 7 C20/F20 with FlexArm Specifications PDF (4522 991 40841, Jan 2019)
- Philips Azurion 7 C12 Specifications PDF (4522 991 56151, Jun 2020)
- Philips Azurion Release 1.2 Instructions for Use (4523 001 01901)
- Philips product pages: usa.philips.com/healthcare/product/HCNCVD207
- Philips press release, January 17, 2019 (FlexArm launch announcement)
