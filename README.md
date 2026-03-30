# Cycloidal Drive for Onshape

A custom Onshape feature that draws a cycloidal drive sketch.

## Cycloidal drive

A type of gearbox that gives you a very high reduction ratio in a compact design. It's popular in robotics because it has virtually zero backlash and handles shock loads really well. 


## Parameters

| Parameter | What it means |
|---|---|
| **N** (number of ring pins) | Your reduction ratio is N−1. So N=24 gives you 23:1. |
| **E** (eccentricity) | How far off-centre the disc sits. This is the offset of your eccentric bearing. |
| **Rr** (ring pin radius) | The radius of each ring pin cylinder. |
| **R** (ring pin pitch radius) | The radius of the circle the ring pin centres sit on. Half your pitch diameter. |
| **Curve points per tooth** | Higher = smoother curve. Use 10 while designing, 100 for the final version. |
| **Total clearance Dr** | Gap between disc and pins. Set 0 for a perfect theoretical fit. For FDM printing use 0.15–0.2 mm. |
| **Centre hole diameter** | The bore for your eccentric bearing. |
| **Output pin diameter** | The diameter of your output shaft pins. The holes in the disc are drawn automatically as pin + 2×E. |
| **Output hole pitch radius** | The radius of the circle the output holes sit on. |

---

## The most important number — K1

```
K1 = E × N / R
```

K1 controls the shape of the teeth. You want it between **0.4 and 0.6**.

- Too high (above 0.7) and the tooth tips become sharp and pointed — hard to print and prone to breaking
- Too low (below 0.3) and the teeth are too shallow to transmit much torque
- The script will stop and warn you if K1 is 1.0 or above because the curve becomes mathematically impossible at that point

The notices panel shows you K1 every time you run the feature so you can check it instantly.

---

## Recommended starting values

These work well for a compact 23:1 drive around 80mm diameter:

| Parameter | Value |
|---|---|
| N | 24 |
| E | 1.0 mm |
| Rr | 2.5 mm |
| R | 38 mm |
| Clearance (FDM) | 0.15 mm |
| K1 result | 0.63 |

---

## Tips

- **For 3D printing** — use 0.15 mm clearance for FDM, 0.05–0.1 mm for resin
- **For stacking multiple discs** — use 2 discs at 180° apart or 3 discs at 120° apart to reduce vibration. 3 discs gives the smoothest output
- **Disc thickness** — roughly 0.3–0.5× your pitch radius R per disc. For the example above that's 11–19 mm per disc
- **Output hole size** — the script calculates this for you (pin diameter + 2×E), but make sure your output pin pitch radius leaves enough material between the holes and the centre bore

---

## License

MIT — use it however you want, just keep the copyright notice if you share it.
