# Dog Roster & Names

*Issues: [#63](https://github.com/derekwinters/lucas-doggiehood/issues/63) (starting roster), [#67](https://github.com/derekwinters/lucas-doggiehood/issues/67) (name pool), [#68](https://github.com/derekwinters/lucas-doggiehood/issues/68) (easter eggs)*

All three specs below are **decided but not yet implemented** as data in the project.

## Starting roster (the 4 houses)

Breed pool used for the starting cast: German Shepherd, Golden Retriever, Labrador, Beagle, Chihuahua, French Bulldog, Puggle, Frenchton. Personality types: see [Dog Behavior](behavior.md).

| House | Dog | Breed | Personality |
|---|---|---|---|
| 1 — Parent + puppy | Zeus | German Shepherd | Brave |
| 1 — Parent + puppy | Nala (puppy) | German Shepherd | Excited |
| 2 — Parent + puppy | Bailey | Golden Retriever | Adventurous/Exploring |
| 2 — Parent + puppy | Sunny (puppy) | Golden Retriever | Excited |
| 3 — Single dog | Pepper | Chihuahua | Grumpy |
| 4 — Multi-dog (3) | Duke | Labrador | Brave |
| 4 — Multi-dog (3) | Scout | Beagle | Adventurous/Exploring |
| 4 — Multi-dog (3) | Waffles | Frenchton | Shy |

French Bulldog and Puggle are unused in the starting roster — reserved for the first expansion zone (v0.4, see [Neighborhood Expansion](../expansion.md)).

## General name pool (for dogs that move in later)

As new houses fill up over time (v0.4 expansion), new dogs need names. The game randomly picks an unused name from a curated pool, avoiding duplicates with dogs currently in the neighborhood.

How move-in dogs are assembled (households, the reserved-breeds-first rule, count-weighted breed selection, personalities, and the easter-egg chance) is specified in [Neighborhood Expansion — Move-in system](../expansion.md#move-in-system) *(decisions 2026-07-14, Derek, on [#54](https://github.com/derekwinters/lucas-doggiehood/issues/54))*. In short: after the reserved French Bulldog and Puggle arrive, breeds are chosen with weights inversely proportional to each breed's current count in the neighborhood.

**Classic**: Buddy, Max, Charlie, Rocky, Cooper, Bear, Tucker, Jack, Toby, Milo, Oliver, Leo, Winston, Baxter, Bentley, Gus, Murphy, Finn, Otis, Chase, Rusty, Sam, Boomer, Ollie, Louie, Bruno, Diesel, Sarge, Ranger, Frank, Gordon, Reggie, Wally

**Playful/food-themed**: Biscuit, Pretzel, Peanut, Nugget, Ziggy, Mochi, Taco, Noodle, Pickles, Beans, Cricket, Marshmallow, Biscotti, Nutmeg, Cinnamon, Pumpkin, Buttercup, Petunia, Juniper

**Classic (softer)**: Bella, Luna, Daisy, Coco, Molly, Sadie, Ruby, Rosie, Lucy, Zoe, Maggie, Penny, Roxy, Willow, Ginger, Honey

68 names total. `Hank` and `Stella` are deliberately excluded from this pool — see easter eggs below. *(The count was originally misstated as 66; corrected to match the enumerated list — see [#67](https://github.com/derekwinters/lucas-doggiehood/issues/67).)*

## Easter egg dogs

A set of names reserved for family/friends Lucas knows. Unlike the general pool, these names always pair with one specific fixed breed (and coat color, where noted) whenever selected — they are never assigned randomly to a different breed, and are excluded from the general name pool above.

Each expansion move-in has a **5% chance** its household head is drawn from this table instead of the general pool (the breed roll is skipped — the fixed breed/coat applies). Once used, a name is **permanently removed from the reserve**, so each easter-egg dog appears exactly once. *(Decision 2026-07-14, Derek, on [#54](https://github.com/derekwinters/lucas-doggiehood/issues/54).)*

| Name | Breed | Coat |
|---|---|---|
| Rex | German Shepherd | Black |
| Arnie | Golden Retriever | Light |
| Hank | Golden Retriever | Dark |
| Stella | Chihuahua | — |
| Muffin | Puggle | — |
| Akon | Puggle | — |
| Brody | Golden Retriever | — |

## Build checklist

- [ ] The 8 starting dogs exist as data (name, breed, personality, house assignment) matching the roster table above
- [ ] General name pool (68 names) exists as data, excluding `Hank` and `Stella`
- [ ] Name-selection logic picks a random unused name from the pool when a new dog is needed, with no duplicates among currently-active dogs
- [ ] Easter-egg name table exists and always resolves to its fixed breed/coat when that name comes up, bypassing random breed assignment
