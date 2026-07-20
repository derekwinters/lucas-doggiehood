# Future Ideas

Ideas that have been discussed and recorded, but are explicitly **out of scope** for now — kept here so they aren't lost or accidentally re-litigated, not because any of this should be built next.

## Dog fears + weather/day-night environmental behavior

*[#87](https://github.com/derekwinters/lucas-doggiehood/issues/87)*

Post-v1.0 concept: a new personality/bio trait category, **Fears** (e.g. fear of the dark, fear of thunder/storms), alongside the existing personality types (see [Dog Behavior](dogs/behavior.md)). This would require environmental systems that don't exist yet — a day/night cycle and weather (rain/thunder) — which directly conflicts with the current v1.0 decision for [static daytime lighting with no weather system](world/world.md#lighting-time).

Concept: a dog afraid of the dark goes inside when night falls; most dogs (regardless of fear trait) go inside when there's thunder.

Implementing this would require revisiting the static-lighting decision first.
