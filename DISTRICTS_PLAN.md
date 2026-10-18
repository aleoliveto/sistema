# Travelling between districts — plan

Today the whole game is one 200×150 map (one `Game`, one `Map`, one save file). The idea: that map becomes **one district of Naples**, and other districts are separate maps you travel to. Nothing below is built yet; this is for you to decide on.

## The recommendation in one paragraph

Keep **one district live at a time**: full simulation, people walking, everything you see today. Every other district runs as a **ledger**: a few numbers per rione that tick once per game hour (clan strength, piazza takings, heat, your income there). You travel by riding to a marked **exit at the map edge** (a road out, or the port). A short "on the road" card follows, and the new district loads. Same clock, same speed, so time never jumps or drags. Your crew, money, stock and phone travel with you. Each district's map, buildings, businesses and piazze are saved in its own file.

## Districts to start with

| District | Feel | What's there that isn't at home |
|---|---|---|
| **Quartieri** (today's map) | Vicoli, bassi, small piazze | Home, Totonno, Nanninella, the clan next door |
| **Il Porto** | Docks, warehouses, wide roads, the lungomare | Smuggling (cigarette cases, product by boat), the Marenaro, police checkpoints |
| **Chiaia / Vomero-style "Alta"** | Palazzi, boutiques, cafés, wide streets, fewer vicoli | Rich customers (higher prices, lower demand), the jeweller and the car dealer, laundering fronts, heavier policing, no piazze allowed in the open (dealing by delivery only) |

Two new districts are enough to prove it. More can come later from the same generator with different settings.

## Decisions and trade-offs

### 1. How districts you're not in keep living

| Option | Pros | Cons |
|---|---|---|
| **A. Ledger tick (recommended)** | Cheap, so the game speed never changes. Easy to save. Easy to balance. | Off-screen events are reported, not seen ("Your piazza at the port was hit last night"). |
| B. Full sim of every district at once | Everything really happens | 3× the CPU and memory, time slows on big maps, and much harder to debug |
| C. Frozen while you're away | Simplest | Feels dead: no income, no threats, nothing to come back to |

Under A, each off-screen rione keeps: owner (you, a clan or nobody), piazza takings per day, heat, clan soldiers, your guards there, and pending threats. Every game hour it rolls the same odds the live sim uses: sales, assaults on undefended piazze, raids by heat, clan expansion. When you arrive, the ledger is turned back into people. Pushers appear on their corners and clan squads that are mid-assault appear on the street.

### 2. How you travel

| Option | Pros | Cons |
|---|---|---|
| **A. Ride to an exit at the map edge, then a short travel card (recommended)** | Fits the scooter, works with the minimap, and makes route choice meaningful (a police checkpoint on the coast road) | One more "loading" moment |
| B. A fast-travel button on the big map | Quickest | Breaks immersion, and the scooter loses a purpose |
| C. One giant seamless city | No loading at all | Huge maps, slow generation, and the problems of option 1B |

Travel takes game time: 20–40 minutes depending on the scooter, with a chance of an event on the way (a stop and search, a clan tail). Crew you choose come with you; the rest stay and work.

### 3. What belongs to a district, and what's global

| Global (travels with you, one save) | Per district (its own save file) |
|---|---|
| The family: crew, money, stock and storage, scooters, respect, evidence, rank | Map: terrain, buildings, furniture, decor, rotations |
| Phone, newspaper, favours, informants | Properties, shops, businesses and pizzo |
| The clock and the weather | Rioni, heat, your piazze and the clans' piazze |
| Clans: name, boss, intel, hostility, soldiers. A clan can hold rioni in more than one district. | Contacts who live there (Nanninella at home, the Marenaro at the port) |
| Contracts (each one notes which district its steps are in) | Fog of war (what you've explored) |

Street NPCs (passers-by, customers, police on patrol) aren't saved today and won't be: each district respawns them.

### 4. Save format

- `save1/` becomes a **folder**:
  - `family.json` holds everything global.
  - `district_quartieri.json`, `district_porto.json` and so on each hold one map plus its ledger.
- Districts you haven't visited yet aren't generated until you first go there. Each has its own seed in `family.json`, so a district stays the same once made.
- **Old saves:** `save1.json` loads as the Quartieri district plus a fresh `family.json`. Version goes from 10 to 11.
- Size: about 250 KB per district, which is fine.

### 5. What changes in the code (rough)

1. **Split `Game`** into `Family` (global) and `District` (map, territory, properties, businesses, squares, street life). This is the biggest piece: about 20 files touch `game.Map`, `game.Territory` or `game.Org.Squares` today.
2. **`DistrictLedger`** gives the hourly off-screen tick, plus the conversions from live district to ledger when you leave and from ledger back to live when you arrive.
3. **Generator settings per district:** `MapGenerator` gets a profile (vicoli density, road widths, block sizes, which shops, the port's docks, Alta's wide streets).
4. **Exits** are road cells at the map edge marked as gates, with a map icon and an "E: ride to the port" prompt, plus the travel card and its events.
5. **Save and load** as above.
6. **UI:** a district name in the top bar; the big map shows the city's districts with the ledger state of each; the Famiglia Overview gains income and threats per district.

## Phases (each one playable and committed)

1. **Split and save:** `Family` and `District` with the new save folder. One district, no visible change. *This is the risky one, so it goes first and alone.*
2. **The port:** a second generator profile, exits, travel with its card, and the ledger for the district you're not in.
3. **The ledger feels alive:** off-screen piazza takings, assaults and raids reported on the phone and in the paper, and clans spanning districts.
4. **Alta:** the rich district, with delivery-only dealing, the dealer and jeweller moved there, and laundering fronts.

Rough size: phase 1 is the biggest. Phases 2–4 are each similar to the clans change we just did.

## Questions for you

1. **Ledger vs. frozen:** happy with districts living as numbers while you're away (option 1A)?
2. **Travel:** ride to the map edge (2A), or do you also want a fast-travel button for later in the game?
3. **Which second district first:** the **port** (smuggling, docks) or **Alta** (money, laundering)?
4. **Home district map size:** keep 200×150, or shrink it a little now that there will be more districts (faster to generate and load)?
