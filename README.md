# Void Stunts (iOS) · v0.10

Unity 6.3 LTS · URP 17 (Forward+) · Metal · built in the cloud and shipped through TestFlight. Setup is in **SETUP.md**.

Everything is generated in code at load time: levels, car, UI and synth audio. The scene is empty, and `Game.Boot` builds the rest.

## Levels (true scale: car 4.4 m · lanes 3.5 m · storeys 3.4 m)
- **CANOPY**: 1.4 km overgrown city with 12×12 blocks, towers up to 180 m, street trees 10–18 m, a 14 m viaduct loop with four on-ramps, a garage spiral leading to a ten-roof skybridge run, the Drop spur, Kowloon terraces, a skate plaza, a lantern market, a screen tower and a 44 m tree.
- **MESA**: 2.6 km desert heightfield with terraced canyons, a 1.2 km airstrip, a dam gorge with a spillway jump, a motel, a radio dish, a fallen tower, power lines and dune bowls.
- **PROVING**: 1.1 km test ground with a banked oval, a 60 m ski jump, a mega-ramp over a canal, a bowl field, a hangar and a monolith slalom.

## Surfaces without visible tiling
Hex-tile sampling (Mikkelsen 2022), world-space macro variation and grime, and procedural facades (windows per bay and storey from mesh params). No texture repeats in a grid.

## Layout
```
Assets/Scripts/Car        VS-02 (frozen design), raycast vehicle, tyre tracks
Assets/Scripts/World      MeshKit (chunked merged meshes + colliders), Vegetation (GPU instanced, LOD), levels
Assets/Scripts/Rendering  materials, atmosphere presets, Looks (post styles + momentum ladder)
Assets/Scripts/Gameplay   touch input, scoring, synth audio, fx
Assets/Scripts/UI         code-built uGUI: menu, sheets, HUD, results
Assets/Editor             BuildScript (configures URP + player + scene, exports Xcode), import rules
Assets/Plugins/iOS        Taptic Engine bridge
Tools                     texture and icon generators (Python)
.github/workflows         ios-testflight.yml, web-preview.yml (free Safari build), compile-check.yml
```

## Quality tiers (Settings sheet)
PERFORMANCE / BALANCED / FIDELITY adjust render scale, shadow distance, SSAO, MSAA and vegetation distance and LOD. There's an optional 120 Hz mode for ProMotion screens. The baseline is iPhone 13 Pro / Pro Max (A15).
