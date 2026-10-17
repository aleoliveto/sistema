# 'O SISTEMA — Game Design Document
*Version 0.1 — working draft*

> A top-down colony-survival sim about one life inside the Camorra — and the bloodline that inherits it.
> RimWorld's simulation depth, This War of Mine's moral weight, Gomorra's honesty.

---

## 1. Vision & Pillars

**Elevator pitch:** You take control of a small *paranza* (crew) in a fictional Naples district. You feed them, house them, pay them, arm them, and keep them loyal, while running drug squares, collecting *pizzo*, dodging police and fighting rival clans. When your boss dies, gets arrested or turns informant, the run passes to his heir. The city remembers everything.

### Design pillars
1. **The System is a business.** Every choice runs through money, wages and debt. Violence is expensive and bad for business.
2. **Nobody is safe, nobody is loyal.** Every NPC has needs, memories and a breaking point. Betrayal comes out of the systems, not a script.
3. **Survive the life, not just the fight.** Food, sleep, wounds, stress, hiding, prison. The street wears you down.
4. **Naples is a character.** Real mechanics, real dialect, fictional people. Authentic, never documentary.
5. **Show the cost.** Grieving mothers, empty chairs, kids drawn into the trade. The game never glamorizes. Consequences carry the fiction.

### Tone references
- *Gomorra* (Saviano's book, 2006): the economics and the banality of it
- *Gomorra – La Serie* (2014–2021): splits, betrayal, the domestic side of bosses
- *La paranza dei bambini* (Saviano, 2016): the baby-gang era
- *Il camorrista* (Tornatore, 1986): the Cutolo / NCO era
- RimWorld, This War of Mine, Cartel Tycoon, Prison Architect: gameplay models

### Legal and ethical rules (non-negotiable)
- **No real clans, bosses, victims or pentiti by name.** Everything is fictionalized and inspired by documented patterns.
- **Districts are fictional composites** (e.g. "Rione Sette Palazzi" is inspired by the Scampia Vele but is not Scampia).
- **No real business names, journalists or magistrates.**
- Victims are never mere loot. Killing civilians has heavy, lasting systemic weight.

---

## 2. The Real Mechanics → Game Systems Map

| Real practice | Term | Game system |
|---|---|---|
| Monthly salary to affiliates | *mesata* | Wage system; late pay drains loyalty |
| Clan's shared cash fund | *'a bacinella* | Clan treasury, separate from personal cash |
| Support for jailed members' families | *assistenza ai carcerati* | Recurring cost; skipping it breeds pentiti |
| Drug-dealing squares | *piazza di spaccio* | Territory "shops" with shifts, staff and output |
| Lookouts | *vedette / pali* | Job type; early-warning radius vs. raids |
| Square manager | *capopiazza* | Manager role; boosts output, skims money |
| Extortion of businesses | *pizzo* | Recurring income; shop owners have patience/defiance |
| Intimidation drive-by shooting | *stesa* | Action: raises fear and Respect, spikes Heat |
| Splinter groups | *scissione* | Faction-split event driven by unrest |
| Collaborators of justice | *pentiti* | Informant system; anyone can flip under pressure |
| Fugitive life in bunkers | *latitanza* | Survival mode inside hidden bunkers |
| Hard prison regime | *41-bis* | Prison mode: isolation, info delay |
| Coded notes from bosses | *pizzini* | Delayed, lossy command system while hidden or jailed |
| Baby gangs | *paranze* | Late-era chaotic faction; volatile recruits |
| Toxic waste trafficking | *Terra dei Fuochi* | Late-game business: huge profit, health damage to the region |
| Counterfeit goods, construction, rigged tenders | — | Mid/late-game laundering and legitimate fronts |
| Wiretaps and investigations | *intercettazioni* | Heat sub-system; careless talk builds evidence |

---

## 3. Core Structure

### 3.1 Run structure (roguelite dynasty)
- **A run = one boss's life.** Starting position, district layout, clans and era are procedural.
- **Run endings:** killed · life sentence · turned *pentito* · forced exile · peaceful retirement (very rare) · became *capoclan* of the district (a "victory" with a bitter epilogue).
- **Succession:** the next run starts with a chosen heir (son, nephew, widow, trusted lieutenant). It inherits:
  - Family name reputation (feared / honored / cursed if the boss turned pentito)
  - Vendettas and debts
  - Part of the treasury, properties and hidden stashes
  - **NPC memory:** surviving NPCs remember what the last boss did to them
- **Legacy unlocks** (meta-progression, light): new starting scenarios, eras and storyteller modes. These open up new ways to start, not raw stat power.

### 3.2 Eras (scenarios)
| Era | Flavor | Mechanics emphasis |
|---|---|---|
| **1980–83: The Great War** | Two mega-confederations at war; cigarette smuggling | Heavy violence, earthquake reconstruction money |
| **2004–05: The Split** | Narco boom, the Vele, splinter war | Drug squares, scissione events, extreme Heat |
| **2015+: The Kids** | Baby gangs, social media, crypto | Volatile recruits, online exposure, fragmented clans |

### 3.3 Time and loop
- RimWorld-style real-time with pause and 1×/2×/3× speed. Days have phases (morning / afternoon / evening / night).
- **Weekly beat:** Sunday is payday (*mesata*), with collections and reports.
- **Storyteller AI** (RimWorld-style) paces events: *"'O Destino"* (balanced), *"'A Guerra"* (violent), *"'A Pazienza"* (slow-burn economy).

---

## 4. Survival & Colony Layer

Your "colony" is your **crew and their base** (an apartment block, a *basso* street-level home, a farmhouse in the hinterland, or a bunker).

### 4.1 Pawn needs (every crew member)
| Need | Satisfied by | Failure effect |
|---|---|---|
| **Hunger** | Meals (cooked by crew, takeout from local trattorie and bakeries, family-brought food) | Weakness, mood loss |
| **Sleep** | Beds; quality matters | Mistakes on jobs, accidents |
| **Comfort** | Furniture, TV, a decent place | Mood loss |
| **Recreation** | Football on TV, cards (*scopa*, *tressette*), bar, scooter rides | Stress build-up |
| **Family** | Visits, money sent home | Resentment, distraction |
| **Faith** | Altars, church, saint processions (San Gennaro, Madonna dell'Arco) | Superstition stress events |
| **Safety** | Bulletproofing, lookouts, low Heat | Paranoia |
| **Vice** | Cigarettes, alcohol, drugs (risky) | Withdrawal, addiction |

### 4.2 Health
- Body-part injury model (RimWorld-lite): gunshot, stab, beating, chronic illness.
- **You can't go to a hospital without risk.** Gunshot wounds get reported. Options: a bent doctor, a crew medic (skill), or a hospital visit that raises Heat.
- Scars and disabilities are permanent; prison and street life cause long-term illness.

### 4.3 Mood & mental breaks
Mood comes from needs plus *thoughts* ("My brother was killed −20", "Paid on time +5", "Boss humiliated me −15").
Mental breaks take a Neapolitan flavor:
- **Sulking / disappearing** for a day
- **Drunk shooting spree** (Heat spike)
- **Running to the Carabinieri** (pentito risk check)
- **Showing off on social media** (2015+ era: evidence leak)
- **Revenge on own initiative** (starts an unplanned war)

### 4.4 Base building
- Grid-based construction like RimWorld: walls, doors, furniture.
- **Camorra-specific builds:**
  - Reinforced doors, gates, *cancelli* on stairwells
  - Hidden compartments (*nascondigli*) for cash, drugs and guns
  - Cameras and lookout posts
  - **Bunkers:** dig hidden rooms behind walls, under floors, with secret entrances. Discovery chance scales with build quality and Heat.
  - Shrines and family photo walls (mood)
- Resources: money, building materials (bought, stolen, or "donated" by construction firms under pizzo), food, weapons, drugs.

---

## 5. Crime Layer

### 5.1 Crew roles and skills
**Skills:** Shooting · Melee · Driving · Persuasion · Intimidation · Stealth · Business · Medicine · Construction · Cooking · Tech.
**Clan roles (hierarchy is a "job", not a fixed class):**
`Guaglione / Vedetta → Pusher → Cavallo (courier) → Capopiazza → Killer (fuoco) → Luogotenente → Capozona → Capoclan`

### 5.2 Traits (examples)
`Hothead` · `Family man` · `Ambitious` · `Superstitious` · `Silent (omertà)` · `Chatterbox` (wiretap risk) · `Addict` · `Loyal to the old boss` · `Greedy` · `Ice-cold`

### 5.3 Territory: the district map
- Separate **district map** (strategic layer) made of blocks and zones. Each zone has an owner, Heat, Respect and an economic value.
- **Drug squares:** placed in zones, staffed by shifts (lookouts, pushers, a capopiazza). Output depends on location, staff skill and police presence.
- **Pizzo:** each shop has *income*, *defiance* and *connections*. Squeeze too hard and they close, report you or reach out to a rival.
- **Fronts:** buy a bar, pizzeria, car dealership or construction firm to launder money and lower Heat.

### 5.4 Economy
- **Personal cash vs. clan treasury (*bacinella*).** Skimming from the treasury is a temptation and a risk.
- Weekly expenses: wages, family support for jailed members, lawyers, bribes, supplies.
- Income: squares, pizzo, smuggling, contracts, laundering returns.
- Supply chain: buy wholesale from suppliers (outside contacts). Price and reliability vary, and suppliers can be raided too.

### 5.5 Tactical jobs (the "action" in the colony sim)
Real-time with pause, on the local map, using pawn control:
- Collections, ambushes, *stese*, escapes over rooftops, rival square takeovers, defending against raids.
- Scooters (*motorini*) and cars as vehicles; narrow alleys favor scooters.
- Combat is short and lethal. One bullet can end a key pawn. That's why you'd rather negotiate.

---

## 6. Pressure Systems

### 6.1 Heat (police and justice)
- **Zone Heat** (patrols, raids on squares) and **Personal Heat** (warrants on individuals).
- **Evidence:** wiretaps, informants, forensic traces and social media posts build a case file per pawn. When it fills, a **warrant** is issued, and the pawn goes into latitanza or gets arrested.
- **Maxi-blitz** events: coordinated dawn arrests across your whole organization.

### 6.2 Rival clans (faction AI)
- Each clan has: leader personality, treasury, territory, alliances and grudges.
- Diplomacy: truce, alliance, marriage alliance (*matrimonio*), tribute, supply deals, war.
- **Scissione (split):** when a clan's internal unrest grows (unpaid wages, a humiliated lieutenant, a weak boss), it splits. **This can happen to you.**

### 6.3 Pentiti (informants)
- Every arrested or pressured pawn rolls against **Loyalty × Fear × Family-supported × Prison-time**.
- A pentito reveals bunkers, stashes, squares and names, which feeds Heat and evidence.
- Counter-measures (dark choices): move assets, pay families, eliminate the risk, or the "family pressure" option, with heavy moral weight and consequences.
- **The player boss can choose to turn pentito.** It ends the run and curses the bloodline, but it might save your children.

---

## 7. Special Modes

### 7.1 Latitanza (fugitive survival) — the signature mode
Triggered when a warrant is issued for the boss.
- The map shrinks to the **bunker plus a few trusted homes**.
- **Supplies arrive only through couriers.** Food, medicine, news and cigarettes. Every visit adds a leak risk.
- **Cabin fever:** stress builds; recreation is minimal.
- **Command by *pizzini*:** orders go out with a delay of days and can be misread or intercepted.
- Risky choices: visit your newborn child, go to your mother's funeral, change bunkers.
- Exit: the warrant expires, a lawyer gets it annulled, you flee abroad (exile ending), or you're captured, which leads to prison.

### 7.2 Prison (Poggioreale-inspired)
- Prison is a new map, a new hierarchy and new needs.
- Run the clan from inside through lawyers, visits and smuggled phones, until **41-bis isolation** cuts your link.
- Outside, the clan may stay loyal, replace you, or split.
- Exits: trial outcomes (lawyer quality, evidence, witness intimidation), sentence served, death inside.

### 7.3 Family layer
- Wife or husband, mother, children, siblings as semi-controllable NPCs.
- Women historically took over clans when the men were jailed. **The widow or sister can become the heir.**
- Children grow up across runs. What you expose them to shapes their traits.

---

## 8. Narrative & Dialect

- **Dialogue in Neapolitan dialect**, subtitled in English and Italian (more later).
- Emergent storytelling first; light hand-written **event cards** in the style of "a woman comes to ask for her son's debt to be forgiven."
- **Diegetic UI flavor:** wiretap transcripts as evidence logs, newspaper headlines (*"Agguato nel Rione…"*) as event notifications, pizzini as the command screen during latitanza.
- **Dialect quality:** Claude drafts; **a native Neapolitan speaker reviews** before release. Neapolitan spelling varies, so pick one convention and stick to it.

**Sample barks:**
- Paid on time: *"Grazie, Don. 'A famiglia mia v'aringrazia."* — "Thanks, Don. My family thanks you."
- Late wages: *"Cca nun se magna cchiù, Don."* — "We're not eating anymore here, Don."
- Lookout alarm: *"Arrivano 'e guardie! Fuitevénne!"* — "Cops are coming! Run!"
- Paranoia: *"Chillo parla troppo..."* — "That one talks too much..."

---

## 9. Art & Audio Direction

- **Top-down 3/4 view, RimWorld-style readability.** Pawns use separate body and head sprites so apparel and hair layer cheaply.
- **Palette:** sun-bleached tuff yellow, laundry-line colors, concrete grey of the Vele, neon at night.
- **Tilesets:** *bassi*, alleys (*vicoli*), Vele-style housing blocks, hinterland farmhouses, the bunker, prison.
- **Music:** original tracks inspired by neomelodic, tammurriata and dark electronic. Licensed or commissioned. **No copyrighted songs.**
- **Ambient:** scooters, shouting from balconies, church bells, fireworks (which double as an in-world square-delivery signal).

**Art production plan:** placeholders first (simple shapes), then cheap asset packs for tiles, then commissioned character and portrait art once the game is fun.

---

## 10. Technical Architecture (Unity)

- **Engine:** Unity 6 LTS · 2D URP · C#
- **Map:** Unity Tilemap for rendering; a separate **grid data model** as the source of truth.
- **Simulation and presentation are separated:** a deterministic tick-based simulation (`TickManager`, e.g. 60 ticks per game-second at 1×) drives plain C# data. MonoBehaviours only render and handle input. This keeps saves, speed changes and testing easy.
- **Data-driven definitions ("Defs"):** pawns, traits, items, buildings, jobs and events defined in JSON (or ScriptableObjects at first). This enables **modding later**, which is huge for Steam longevity.
- **AI:**
  - Pawn AI: RimWorld-style **Need → WorkGiver → Job → Toils** pipeline with priorities.
  - Pathfinding: custom grid A* with region caching.
  - Faction AI: utility-based decisions on a weekly tick.
  - Storyteller: event scheduler with threat budget.
- **Save/Load:** full simulation-state serialization to JSON (compressed).
- **Localization:** Unity Localization package; string tables for NAP (dialogue), EN and IT.
- **Steam:** Steamworks.NET for achievements, cloud saves, and later Workshop for mods.

### Proposed folder structure
```
Assets/
  _Project/
    Scripts/
      Core/          (TickManager, GameState, SaveSystem, Defs loader)
      World/         (Grid, Tiles, Zones, Rooms, Construction)
      Pawns/         (Pawn, Needs, Skills, Traits, Health, Mood)
      AI/            (Jobs, WorkGivers, Pathfinding)
      Crime/         (Territory, Squares, Pizzo, Economy, Heat, Evidence)
      Factions/      (Clans, Diplomacy, Scissione)
      Story/         (Storyteller, Events, Dynasty)
      UI/
    Defs/            (JSON definitions)
    Art/  Audio/  Scenes/  Localization/
```

---

## 11. Scope & Roadmap

### Milestone 0 — Foundations (≈ 2–4 weeks)
Grid, camera, tick and time controls, pawns walking with A*, select and order, basic building (walls, doors, bed).

### Milestone 1 — Colony survival (≈ 1–2 months)
Needs (hunger, sleep, mood), food sources, job priorities, injuries and simple medicine, save/load.

### Milestone 2 — The Business (≈ 1–2 months)
One district map, one drug square with shifts, pizzo collection, wages, treasury, Heat, police raids, lookouts.

### Milestone 3 — Vertical slice / Steam demo (≈ 2 months)
One rival clan with AI, a tactical ambush, latitanza bunker mode, a first set of event cards, dialect barks, succession to an heir. **→ Steam page + demo for Next Fest.**

### Milestone 4 — Early Access
Prison mode, scissione, pentiti, 2 eras, storyteller choices, family layer, more content.

### Milestone 5 — 1.0 and beyond
Third era, modding / Workshop, more districts and businesses, polish.

> Solo-dev reality check: Milestone 3 is the real goal. A fun, short, replayable slice gets wishlists. Everything else is built on top of proof that the core loop works.

---

## 12. Open Questions
- Working title: *'O Sistema*? Alternatives: *Sistema*, *'A Paranza*, *Rione*, *Latitante*.
- How much direct pawn control in tactical jobs vs. RimWorld-style "draft" mode?
- Is there a "clean ending" path (legit businessman) or is the System inescapable by design?
- Price point and platform (PC first; Steam Deck verified as a goal).
- A native Neapolitan dialect reviewer: who?

---

## 13. Road to Steam

*Added after Milestone 3.* The systems work. Here is the honest list of what separates a working prototype from a game people pay for. Roughly in priority order: the first two phases decide whether the Steam page gets wishlists.

### Phase A — Make it readable (≈ 2–3 months)
The game has to explain itself at a glance, and right now it doesn't.
- **Art direction pass:** 3–4 style frames (a street at noon, a piazza at night, the basso interior, an event card) to lock the look *before* producing assets.
- **Tiles:** tuff and plaster walls with variation, basolato, asphalt, interiors (tiles, furniture), shopfronts, the bunker. Walls need a top-down "height" read like RimWorld/Prison Architect.
- **Characters:** a layered sprite system (body, head, hair, clothes, held weapon), 4 directions, simple walk / lie down / shoot poses. Crew, rivals, police and civilians must be readable by silhouette and colour.
- **Event card illustrations and crew portraits:** the emotional hook of the game.
- **UI rebuild in UI Toolkit:** replace the IMGUI prototype with a consistent visual language: main menu, pause, settings (resolution, audio, key bindings, UI scale), save slots and autosave, tooltips on everything, an inspector panel for shops and squares, and a notification feed styled as newspaper headlines and wiretap transcripts.
- **Onboarding:** a guided first week (feed the crew → open a piazza → first pizzo → first raid), plus contextual hints.

### Phase B — Make it feel (≈ 2 months, overlaps A)
- **Audio:** layered district ambience (scooters, balconies, church bells, fireworks as the piazza's delivery signal), SFX (gunfire, doors, cooking, glass for a scare, fire), UI sounds.
- **Music:** original score inspired by neomelodic, tammurriata and dark electronica. Commissioned and fully licensed.
- **Voice:** short recorded Neapolitan barks (you, or local actors) on top of the subtitles. A huge authenticity win for little cost.
- **VFX and animation:** muzzle flashes, fire and smoke for arson, night lighting with URP 2D lights (street lamps, neon, windows), small animations (scooters, laundry).
- **Content:** 30–40 event cards, traits (hothead, chatterbox, family man…), relationships between crew, more businesses, a pentito storyline, and a second era.

### Phase C — Make it Steam (≈ 1–2 months, starts early)
- **Steamworks:** partner account and app fee (you pay this, with your identity and bank details), Steamworks.NET for achievements and cloud saves, builds for Windows, macOS and Linux, Steam Deck controls and UI scale.
- **Store page:** capsule art, 5+ screenshots, a 60–90 s trailer, a short and long description, tags. Put the "Coming soon" page up **months before launch** to collect wishlists.
- **Mature content survey:** the game depicts violence, drugs and crime; describe it honestly in Steam's content questionnaire.
- **Demo for Steam Next Fest:** the first 3–4 in-game weeks, polished, with a wishlist call-to-action at the end.
- **Playtests:** Steam Playtest or a Discord group from Phase A onwards. Watch new players without helping them.

### Tech and quality, throughout
- Balance tooling: an in-game debug overlay with live economy and heat graphs, plus a headless "simulate 30 days" test to catch runaway economies.
- Performance: path caching and a region system before maps grow.
- Save versioning with migrations (no more "old saves won't load" once players exist).
- Localization tables (EN / IT / NAP subtitles) instead of strings in code.
- Crash and error reporting in builds.

### Legal and ethical checklist
- Every clan, person and business is fictional; a disclaimer at launch.
- Don't use "Gomorra" or other trademarks in the title or marketing.
- License every font, sound and track; keep the receipts.
- Keep pillar 5 (*show the cost*) visible in marketing too: it's what separates the game from glorification.

### Who does what
- **Claude:** systems and UI code, tools, balancing passes, the event-card writing pipeline, store-page copy drafts, build automation.
- **You:** the final say on art and tone, Neapolitan dialogue and voice, hiring and briefing artists and a composer, the Steamworks account and payments, playtest recruitment.
