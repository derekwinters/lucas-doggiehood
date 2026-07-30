# Map Builder

An interactive tool for designing and visualising the Doggiehood neighborhood. It opens on the current authored map, renders it top-down (roads, curved bends, cul-de-sac bulbs, houses), and checks every piece against the [tile connection rules](../specs/world/tile-catalog.md) as you edit — a red **!** marks any tile whose road meets a neighbour's wall.

This is a **design/authoring aid**, not in-game UI. It's the fast way for Derek & Lucas to draw and expand the map together; the authored layout it produces feeds the map-definition system ([#359](https://github.com/derekwinters/lucas-doggiehood/issues/359)).

<p><a href="map-builder.html" target="_blank" rel="noopener"><strong>▶ Open the Map Builder full screen ↗</strong></a></p>

<iframe src="map-builder.html" title="Doggiehood Map Builder" loading="lazy"
        style="width:100%; height:820px; border:1px solid var(--md-default-fg-color--lightest); border-radius:10px;"></iframe>

## How to change the map

- **On the page:** pick a road piece, click a glowing empty square to place it, or use **Erase**. The map re-validates live. Toggle houses, coordinates, and tile codes in the sidebar.
- **To save your work:** open the **Map data (JSON)** drawer at the bottom and press **Copy**, then paste the JSON wherever you need it (a GitHub issue, or the data file below).
- **To change the default map** everyone sees here: edit **`docs/tools/map-data.json`** — the page loads it on open, falling back to the map baked into the tool if the file is missing. Expanding the neighborhood later is just editing that file (or drawing it in the tool and copying the JSON back).

## Format

A map is `{ "name": ..., "tiles": [ { "x", "y", "type" } ] }`. `x` is east/west, `y` is north/south, origin `FourWay` at `(0,0)` — the same convention as the [Tile Catalog](../specs/world/tile-catalog.md). `type` is a catalog tile name (e.g. `CulDeSacSouth`); the tool also accepts a `code` (the 5-char connectivity token) in place of `type`.
