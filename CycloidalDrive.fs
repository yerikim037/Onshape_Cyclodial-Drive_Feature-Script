FeatureScript 2399;
import(path : "onshape/std/geometry.fs", version : "2399.0");

// Cycloidal Drive Sketch Generator for Onshape
// MIT License
//
// This feature draws the 2D sketch of a cycloidal disc drive on any face you pick.
// It generates the disc profile, ring pins, centre hole, and output holes — all
// from just a handful of physical parameters.
//
// The disc profile is based on the contracted epitrochoid formula:
//
//   ψ = atan2( sin((1−N)·t),  R/(E·N) − cos((1−N)·t) )
//   X =  R·cos(t) − Rr·cos(t+ψ) − E·cos(N·t)
//   Y = −R·sin(t) + Rr·sin(t+ψ) + E·sin(N·t)
//
// This is the same formula used in the SolidWorks Education blog by Omar Younis,
// StepByStep Robotics, and most hobbyist cycloidal drive designs online.
// t sweeps from 0 to 2π to trace the full disc profile.
//
// Quick parameter guide:
//   N  = number of ring pins (your reduction ratio is N−1)
//   E  = eccentricity — how far off-centre the disc sits from the input shaft
//   R  = pitch radius — the radius of the circle the ring pin centres sit on
//   Rr = ring pin radius
//   K1 = E×N/R — keep this between 0.4 and 0.6 for a good tooth shape
//
// After you click OK, open the notices panel (the speech bubble at the bottom)
// to see K1, the modified radii, and the average pressure angle.


annotation { "Feature Type Name" : "Cycloidal Drive" }
export const cycloidalDrive = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        // Which face to draw on
        annotation { "Name" : "Sketch plane",
                     "Filter" : EntityType.FACE,
                     "MaxNumberOfPicks" : 1 }
        definition.sketchPlane is Query;

        // N = total number of ring pins. Your reduction ratio = N - 1.
        // For example N=24 gives a 23:1 reduction.
        annotation { "Name" : "Number of ring pins N (reduction ratio + 1)",
                     "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
        isInteger(definition.ringPinCount, { (unitless) : [3, 24, 99999] } as IntegerBoundSpec);

        // E = how far the disc centre is offset from the input shaft centre.
        // This is the offset of your eccentric bearing.
        // Rule of thumb: keep K1 = E×N/R between 0.4 and 0.6.
        annotation { "Name" : "Eccentricity E",
                     "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
        isLength(definition.eccentricAmount, { (millimeter) : [0.01, 1.0, 9999.0] } as LengthBoundSpec);

        // Rr = the radius of each ring pin cylinder.
        annotation { "Name" : "Ring pin radius Rr",
                     "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
        isLength(definition.ringPinRadius, { (millimeter) : [0.1, 5.0, 9999.0] } as LengthBoundSpec);

        // R = radius of the circle that the ring pin centres sit on.
        // This is half your ring gear pitch diameter.
        annotation { "Name" : "Ring pin pitch radius R",
                     "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
        isLength(definition.ringPinPitchRadius, { (millimeter) : [1.0, 30.0, 99999.0] } as LengthBoundSpec);

        // More points = smoother curve but slower regeneration.
        // Use 10 while designing, bump to 100 for the final version.
        annotation { "Name" : "Curve points per tooth (higher = smoother)",
                     "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
        isInteger(definition.plotPointsPerTooth, { (unitless) : [3, 10, 500] } as IntegerBoundSpec);

        // Dr = total clearance gap between disc and pins.
        // Set to 0 for a theoretical perfect fit.
        // For FDM printing use 0.15–0.2 mm. For resin use 0.05–0.1 mm.
        // The script splits this correctly between the pin radius and pitch radius
        // using the K1-based formula — you don't need to calculate it manually.
        annotation { "Name" : "Total clearance Dr (0 = no clearance)",
                     "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
        isLength(definition.totalClearance, { (millimeter) : [0.0, 0.2, 999.0] } as LengthBoundSpec);

        // Toggle each element on or off
        annotation { "Name" : "Draw ring pins",
                     "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
        definition.drawRingPins is boolean;

        annotation { "Name" : "Draw centre hole",
                     "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
        definition.drawCentreHole is boolean;

        if (definition.drawCentreHole)
        {
            // This is the bore for your eccentric bearing.
            annotation { "Name" : "Centre hole diameter",
                         "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
            isLength(definition.centreHoleDiameter, { (millimeter) : [0.1, 16.0, 9999.0] } as LengthBoundSpec);
        }

        annotation { "Name" : "Draw output holes",
                     "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
        definition.drawOutputHoles is boolean;

        if (definition.drawOutputHoles)
        {
            annotation { "Name" : "Number of output holes",
                         "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
            isInteger(definition.outputHoleCount, { (unitless) : [2, 6, 9999] } as IntegerBoundSpec);

            // Enter your output shaft pin diameter here.
            // The holes in the disc will be drawn as pin + 2×E automatically,
            // because the disc wobbles by E as it spins so the holes need to be
            // that much bigger to let the pins pass through freely.
            annotation { "Name" : "Output pin diameter (holes drawn at pin + 2*E)",
                         "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
            isLength(definition.outputPinDiameter, { (millimeter) : [0.1, 6.0, 9999.0] } as LengthBoundSpec);

            // Radius of the circle the output hole centres sit on.
            // Must fit inside the disc without clashing with the centre hole.
            annotation { "Name" : "Output hole pitch radius",
                         "UIHint" : UIHint.REMEMBER_PREVIOUS_VALUE }
            isLength(definition.outputHolePitchRadius, { (millimeter) : [1.0, 19.0, 9999.0] } as LengthBoundSpec);
        }
    }

    {
        // Pull the plane geometry from the selected face so we can sketch on it
        const skPlane = evPlane(context, { "face" : definition.sketchPlane });

        // Grab all parameters. Lengths stay as ValueWithUnits (they carry metre units).
        // Pure numbers like N, K1, and ratios need to be plain numbers so trig works.
        const N  = definition.ringPinCount as number;
        const Zc = N - 1.0;   // number of disc lobes = reduction ratio

        const R  = definition.ringPinPitchRadius;
        const Rr = definition.ringPinRadius;
        const E  = definition.eccentricAmount;
        const Dr = definition.totalClearance;

        // K1 is dimensionless (length / length) but FeatureScript still tags it
        // with units internally, so we divide by meter first to get a plain number.
        const K1 = (E / meter) * N / (R / meter);

        // K1 must be strictly less than 1 or the tooth profile gets cusps and
        // folds back on itself, which is physically impossible to manufacture.
        if (K1 >= 1.0)
        {
            throw regenError("K1 = E*N/R must be less than 1. Try reducing E or increasing R.",
                             ["eccentricAmount", "ringPinPitchRadius"]);
        }

        // Apply clearance modifications using the K1-based formula.
        // This splits the total clearance Dr between the pitch radius and pin radius
        // in the correct proportion so the tooth shape stays geometrically valid.
        // If Dr is zero (or K1 is very close to zero) we skip this and use raw values.
        const sqrtK = sqrt(1.0 - K1 * K1);
        const denom = 1.0 - sqrtK;

        var R_mod  = R;
        var Rr_mod = Rr;
        if (denom > 1e-10)
        {
            R_mod  = R  + (-Dr * sqrtK / denom);   // pitch radius gets smaller
            Rr_mod = Rr + ( Dr         / denom);   // pin radius gets bigger
        }

        // Calculate the average pressure angle numerically.
        // This tells you how efficiently the pins transmit torque to the disc.
        // Lower is better — aim for under 35 degrees.
        const twoPi  = 6.28318530717959;
        const invK1  = 1.0 / K1;
        var sumAlpha = 0.0;
        for (var j = 0; j < 360; j += 1)
        {
            const t_j    = ((j as number) / 360.0 * twoPi) * radian;
            const alpha  = atan2(sin((1.0 - N) * t_j), invK1 - cos((1.0 - N) * t_j));
            sumAlpha     = sumAlpha + abs(alpha / radian);
        }
        const avgPressureAngle = sumAlpha / 360.0 * (180.0 / twoPi);

        // Print a summary to the notices panel so you can check your design
        println("── Cycloidal Drive ──");
        println("Reduction ratio  = " ~ Zc ~ ":1");
        println("K1               = " ~ K1 ~ "  (ideal: 0.4 – 0.6)");
        println("R_mod            = " ~ (R_mod  / millimeter) ~ " mm");
        println("Rr_mod           = " ~ (Rr_mod / millimeter) ~ " mm");
        println("Avg pressure angle ≈ " ~ avgPressureAngle ~ " deg  (lower is better)");

        // Create the sketch on the selected plane
        var sk = newSketchOnPlane(context, id + "sk", { "sketchPlane" : skPlane });

        // --- DISC PROFILE ---
        // We sample the parametric formula at N×plotPointsPerTooth evenly spaced
        // angles and fit a spline through the resulting points.
        // The disc is shifted by E along X so it sits in its correct assembled
        // position — offset from the ring gear centre by the eccentricity.
        const totalPoints = (definition.plotPointsPerTooth as number) * Zc;
        var pts is array = [];

        for (var i = 0; i < totalPoints; i += 1)
        {
            const t           = ((i as number) / totalPoints * twoPi) * radian;
            const psi         = atan2(sin((1.0 - N) * t), invK1 - cos((1.0 - N) * t));
            const X           =  R_mod * cos(t) - Rr_mod * cos(t + psi) - E * cos(N * t);
            const Y           = -R_mod * sin(t) + Rr_mod * sin(t + psi) + E * sin(N * t);

            pts = append(pts, vector(X + E, Y));   // +E shifts disc to assembled position
        }

        pts = append(pts, pts[0]);   // close the curve back to the start
        skFitSpline(sk, "profile", { "points" : pts });

        // --- RING PINS ---
        // These are centred at the origin because the ring gear is fixed to the housing.
        // We use the raw (unmodified) R and Rr here, not the clearance-adjusted values,
        // because the ring pins are physical cylinders — you don't shrink them in the sketch.
        if (definition.drawRingPins)
        {
            for (var i = 0; i < definition.ringPinCount; i += 1)
            {
                const th = ((i as number) / N * twoPi) * radian;
                skCircle(sk, "pin" ~ toString(i), {
                    "center" : vector(R * cos(th), R * sin(th)),
                    "radius" : Rr
                });
            }
        }

        // --- CENTRE HOLE ---
        // This is the bore for the eccentric bearing, so it sits at the disc centre
        // which is offset from the origin by E.
        if (definition.drawCentreHole)
        {
            skCircle(sk, "centreHole", {
                "center" : vector(E, 0.0 * meter),
                "radius" : definition.centreHoleDiameter / 2.0
            });
        }

        // --- OUTPUT HOLES ---
        // The holes are larger than the output pins by 2×E on the diameter (E on the radius)
        // because as the disc wobbles, each hole centre traces a circle of radius E
        // around the output pin — so the hole needs that extra room.
        // The hole centres are also shifted by E along X to move with the disc.
        if (definition.drawOutputHoles)
        {
            const holeRadius = (definition.outputPinDiameter / 2.0) + E;
            const pitchR     = definition.outputHolePitchRadius;
            const numHoles   = definition.outputHoleCount as number;

            for (var i = 0; i < definition.outputHoleCount; i += 1)
            {
                const th = ((i as number) / numHoles * twoPi) * radian;
                skCircle(sk, "outHole" ~ toString(i), {
                    "center" : vector(pitchR * cos(th) + E, pitchR * sin(th)),
                    "radius" : holeRadius
                });
            }
        }

        skSolve(sk);
    });
