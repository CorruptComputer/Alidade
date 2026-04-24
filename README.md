# Alidade [![.NET Build & Test](https://github.com/CorruptComputer/Alidade/actions/workflows/build-and-test.yml/badge.svg?branch=develop)](https://github.com/CorruptComputer/Alidade/actions/workflows/build-and-test.yml)

Alidade is a browser-based [OpenStreetMap](https://www.openstreetmap.org) editor targeting experienced contributors. In terms of scope and complexity, it is intended to sit somewhere between iD and JOSM.

## Key characteristics
- **Blazor WebAssembly** — all application logic runs in C# compiled to WASM; no JavaScript framework
- **MapLibre GL JS** — WebGL-accelerated map rendering; OSM edits are pushed as GeoJSON diffs through a thin JS interop boundary
- **Offline-capable** — full edit sessions are possible without connectivity; sync on reconnect
- **Future desktop target** — architecture keeps an Electron packaging path open

## Contributions

Since Alidade is in early development and is not yet ready for general use, I am not accepting public pull requests until the project stabilizes.
