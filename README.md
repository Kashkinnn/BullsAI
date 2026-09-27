# BullsAI

A fuzzy logic–driven enemy AI for a top-down/isometric game demo, built in C# WinForms. The project demonstrates a full Mamdani fuzzy inference pipeline (fuzzification → rule evaluation → aggregation → centroid defuzzification) controlling an enemy's aggression, layered with a perception system (vision cone, line-of-sight, memory decay) and a live, interactive 2D/3D simulation.

## What it does

The enemy's behavior — Idle, Fleeing, Alert, or Attacking — is driven by a fuzzy logic controller that takes two inputs, **Health** and **Distance to player**, and outputs a single **Aggressiveness** score (0–100). That score is computed live and visualized in real time alongside the simulation: membership function graphs, the aggregated output curve, a rule-firing table, and a full control-surface heatmap.

On top of the fuzzy core sits a lightweight perception layer: the enemy only reacts to the player's *true* distance when it can actually see them (within a vision cone, with line-of-sight checked against obstacles, or within close proximity). When sight is lost, its perceived distance drifts gradually back toward "far" rather than snapping instantly, so its behavior degrades smoothly instead of flickering.

## Features

- **Mamdani fuzzy inference engine** — 9 rules over Health (Low/Med/High) × Distance (Near/Med/Far), Math.Min for rule strength, Math.Max for aggregation, centroid defuzzification for the crisp output.
- **Live 3D isometric demo** — WASD-controlled player, autonomous enemy AI, draggable characters, dynamic camera (follow player / follow enemy / manual pan), motion trails, bobbing sprite animation.
- **Live 2D obstacle editor** — top-down grid view for placing/removing obstacles (left-click to add, right-click to remove), toggled with a single button; obstacles are shared between both views.
- **Vision cone visualization** — a 120° perception cone rendered with tiered color zones (red/amber/grey) matching the fuzzy distance sets, raycast against obstacles so it visually stops at walls.
- **Manual controls** — sliders for Health and Distance, adjustable player/enemy speed, one-click edge-case test buttons (boundary values) and rule-trigger buttons (jump straight to any of the 9 rules).
- **Real-time fuzzy visualization panel** — membership graphs for both inputs, the aggregated output curve with centroid marker, a live rule-firing table, and a health×distance control-surface heatmap.
- **Random environment generation** — procedurally scatters obstacles across the grid, avoiding the player's and enemy's current tiles.

## Project structure

```
FuzzyEngine.cs           Fuzzy logic core — membership functions, rule base, defuzzification
EnemyAgent.cs             Enemy perception (vision/LoS/memory) and movement/steering
EnvironmentGenerator.cs   Random obstacle placement utility
BullsAI.cs                Form, UI, game loop, rendering (2D + isometric 3D), all custom-drawn panels
```

| File | Responsibility |
|---|---|
| `FuzzyEngine.cs` | Pure fuzzy logic. `Evaluate(health, distance)` runs the full Mamdani pipeline and returns a `FuzzyResult` containing the crisp aggression score, the state label, every membership degree, the full output curve, and every rule's firing strength. |
| `EnemyAgent.cs` | Owns the enemy's position and decision loop. `UpdateBehavior(...)` decides what the enemy can currently perceive and feeds the right distance into `FuzzyEngine`; `Move(...)` turns the resulting state into steering behavior with obstacle avoidance. |
| `EnvironmentGenerator.cs` | One static method that randomly generates a set of obstacle grid coordinates. |
| `BullsAI.cs` | The WinForms `Form`, all UI controls, the central game loop (`GameTimer_Tick`), isometric projection math, vision cone rendering, and the custom `Panel` subclasses used for the graphs and heatmap. |

## Requirements

- .NET Framework (WinForms) — Visual Studio or any compatible IDE
- No external NuGet packages required

## Running it

1. Open the project in Visual Studio.
2. Build and run — the main window (`BullsAI`) launches directly into the control panel and demo view.
3. Click **Set** to randomize the player/enemy positions and start the live simulation, or use the sliders and test buttons to explore the fuzzy logic without moving anything.

## Controls (live demo)

| Input | Action |
|---|---|
| `W` `A` `S` `D` | Move the player |
| Click + drag player/enemy circle | Reposition either character manually |
| Click + drag field (Manual camera mode) | Pan the camera |
| **Set** | Randomize positions and start the simulation |
| **Stop** | Freeze the simulation |
| **Attack (-10 HP)** | Damage the enemy |
| **2D / 3D** toggle | Switch between the obstacle editor and the live isometric view |
| **Clear Blocks** / **Random Env** | Clear or regenerate obstacles |

## Notes

- The fuzzy engine (`FuzzyEngine.cs`) is completely independent of the UI and the perception system — it can be tested or reused on its own with just `(health, distance)` as input.
- Manual slider/button input bypasses the perception layer entirely (`isManualOverride = true`) so the fuzzy logic itself can be tested in isolation from vision/LoS effects.
