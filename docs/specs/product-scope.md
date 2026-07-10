# Product Scope & Constraints

*Epic: [#17](https://github.com/derekwinters/lucas-doggiehood/issues/17)*

These are standing product decisions, not tied to any single build milestone.

## Monetization

No ads, no in-app purchases, no store monetization plans. This is a free personal project. ([#41](https://github.com/derekwinters/lucas-doggiehood/issues/41))

## Connectivity

Fully offline. No account system, no backend. Progress is saved locally on the device only. ([#42](https://github.com/derekwinters/lucas-doggiehood/issues/42))

## Audience

All-ages/family. The game aims to be cozy and simple enough for a family to play together, without being designed around any one specific age bracket. ([#43](https://github.com/derekwinters/lucas-doggiehood/issues/43))

## Build checklist

- [ ] No ad SDK, IAP SDK, or monetization code paths exist anywhere in the project
- [ ] No network calls exist anywhere in the project — the app functions with no connectivity at all
- [ ] Save data is local-only (device storage), with no account/login flow
