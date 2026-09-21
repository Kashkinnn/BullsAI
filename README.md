# BullsAI - A Fuzzy Logic RPG Enemy AI

A Windows Forms demo project implementing an RPG enemy's behavior using a **Mamdani fuzzy inference system**. The enemy's aggressiveness is computed from two inputs — its own health and its distance to the player — and drives both a manual-control test panel and a live, movable WASD demo.

## Overview

Instead of hard-coded `if/else` thresholds, the enemy's behavior is governed by fuzzy logic: linguistic concepts like "healthy," "hurt," "near," and "far" are modeled as overlapping membership functions rather than sharp cutoffs. This produces smoother, more human-like behavioral transitions than a crisp rule table would.

**Inputs:**
| Variable | Range | Meaning |
|---|---|---|
| Health | 0–100 | Enemy's current HP, as a percentage |
| Distance | 0–51 | Distance to the player in meters (51 represents "beyond 50m") |

**Output:**
| Variable | Range | Meaning |
|---|---|---|
| Aggro | 0–100 | A continuous aggression score, mapped to one of four states |

**Output states:** `Fleeing` → `Idle` → `Alert` → `Attacking`, determined by thresholding the defuzzified Aggro score.

## Project Structure

```
EnemAI/
├── BullsAI.cs        # Main Form: UI (sliders, status card), live WASD demo, rendering
└── FuzzyEngine.cs     # Static fuzzy inference engine (fuzzification → rules → defuzzification)
```

`FuzzyEngine.Evaluate(health, distance)` is decoupled from the UI — it takes two doubles and returns a `FuzzyResult { Aggressiveness, State }` struct, so it can be tested or reused independently of the Form.

## How It Works

### 1. Fuzzification
Health and Distance are each converted into three linguistic membership values using triangular membership functions:

| Variable | Low/Near | Medium | High/Far |
|---|---|---|---|
| Health | (1, 1, 40) | (25, 50, 80) | (60, 100, 100) |
| Distance | (0, 0, 20) | (10, 25, 40) | (30, 50, 50) |

### 2. Rule Base
9 rules cover every combination of Health × Distance:

| Health \ Distance | Near | Medium | Far |
|---|---|---|---|
| **Low** | Fleeing | Fleeing | Idle |
| **Medium** | Attacking | Alert | Idle |
| **High** | Attacking | Alert | Idle |

Each rule uses `Math.Min` for the fuzzy AND between its two conditions.

### 3. Aggregation & Defuzzification
Rules sharing the same output category are combined with `Math.Max` (fuzzy OR), clipped against that category's output membership function, then aggregated across all four categories with `Math.Max`. The result is defuzzified using the **centroid (center of gravity)** method, computed numerically over the output range in steps of 1.0.

### 4. Output Thresholds
| Aggro | State |
|---|---|
| < 15 | Fleeing |
| 15 – 44.9 | Idle |
| 45 – 74.9 | Alert |
| ≥ 75 | Attacking |

One crisp exception exists outside the fuzzy engine: `Health ≤ 0` immediately returns a `DEAD` state, since death is a discrete condition, not a fuzzy one.

## Running the Project

**Requirements:** .NET Framework / .NET (Windows Forms support), Visual Studio or `dotnet build`.

1. Open the project in Visual Studio (or run `dotnet run` from the project directory).
2. The main window shows:
   - **Enemy Status** — live icon and aggression readout.
   - **Manual Controls** — sliders to directly set Health and Distance and observe the resulting state.
   - **Live Demo** — a small field where you can move the player (WASD) and watch the enemy react in real time.

**Live Demo controls:**
| Key / Button | Action |
|---|---|
| `W` `A` `S` `D` | Move the player |
| Start | Begin the live simulation |
| Stop | Pause the simulation |
| Attack (−10 HP) | Damage the enemy to test health-driven behavior changes |
| Reset | Randomize positions and restore full health |

## Known Limitation: Defuzzification Straddling

Because centroid defuzzification averages over the *entire* combined output shape rather than picking a single winning category, the crisp Aggro value can occasionally land inside a numeric band whose corresponding rule was never actually active — for example, briefly reading "Alert" while transitioning from Attacking to Fleeing at close range, even though no Alert rule can fire at that distance. This is a known, well-documented characteristic of centroid defuzzification with well-separated, multi-modal outputs, not a defect in the rule base. See the accompanying technical documentation for a full analysis, measured frequency, and possible mitigations (Mean-of-Maximum defuzzification or explicit tie-breaking rules).

## Credits

Triangular membership function implementation adapted from course material (Intelligent Systems).
