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

### 7.2b Poggioreale as a playable map (planned)
Today, prison is a screen: a pizzino a day, a colloquio on Saturdays, the lawyer. The plan is a real map:
- **The jail as a district:** cell blocks, the yard (*l'aria*), the canteen, the chapel, the visiting room, the infirmary. You walk it like the rione.
- **People:** cellmates, other clans' men (some at war with you, some open to a pact), the old bosses who run the wings, guards who can be bought, and *pentiti* kept apart.
- **Missions inside:** carry a message between wings, settle a debt in the yard, protect a new boy, get a phone smuggled in, find out who is talking to the magistrate.
- **New friends and connections:** whoever you help inside can become a contact, a supplier or a recruit when you're both out. Enemies made inside follow you out too.
- **The outside goes on:** pizzini and colloqui stay, now as things you do in person (the visiting room, the guard who carries notes).
- **Art needed:** cell, bunk, toilet, yard wall and fence, canteen tables, chapel pews, visiting-room glass, guards' uniforms, inmates' clothes.

### 7.1b Latitanza abroad (planned, later)
When the warrant can't be beaten at home, run: a flight to **South America** (an invented port city, never a real cartel). A new district with its own rules:
- **A new start with nothing:** no turf, no name, a little money wired by the family, a language you barely speak.
- **The link home:** orders to the family by burner and courier, with long delays; the family may hold, split or replace you.
- **Local people:** a fixer, a landlord, a bar, local crews who want to know who you are; the risk of extradition if you're careless.
- **Ways back:** the warrant lapses, a lawyer gets it annulled, or you're caught and flown home to Poggioreale.
- **Art needed:** a whole new set: colonial streets and houses, a port, tropical plants, a different style of furniture (rattan, ceiling fans, hammocks), local cars and scooters, people and police.

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

### Next up (as of October 2026)
- Districts phase 3 and beyond (see DISTRICTS_PLAN.md).
- Poggioreale as a playable map (7.2b): after districts, since it's built as one.
- Walk, sit, sleep and ride animation from new art sheets (ART_PROMPTS.md, sheet 12 onward).
- Latitanza abroad (7.1b): after Poggioreale.
- Story mode and sandbox settings (section 14): the beat engine and Act 1 first.

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

---

## 14. Story Mode and Sandbox

*Design draft, October 2026. Nothing here is built yet. Every Neapolitan line is a **draft** for native review (Story/Dialect.cs rules: short, common, 28 characters at most). All names are invented placeholders.*

At new game the player picks one of two modes. Both run the **same simulation**: same map, same economy, same police and clans. Story mode puts a hand-written spine on top of it. Sandbox is today's free play, with settings.

### 14.1 Principles
- **The story rides the sim, it doesn't replace it.** A beat is a goal the player meets with systems that already exist (a lavoretto, a piazza, an operation card, a colloquio, a trial). No custom levels, no cutscene engine.
- **Gated by progress, never the clock.** A beat opens when a condition is true (rank, respect, a piazza held, a clan at war, a warrant). The player can play the sim for as long as they like between beats. A beat that has waited a long time can nudge (a phone call, a visit), but it never fires by date alone.
- **Scenes are short.** A person walks up to the player (Events' walk-up, or a phone call if he can't be reached), a few lines, two or three choices. The same card format as today's event cards, with a portrait.
- **The cast is small and recurring.** Six people the player knows from the first hour. Strangers come from the sim.
- **Choices are remembered and come back.** Each scene sets a flag or a number on a cast member (trust, debt, grudge). Later beats read them; the endings read them all.
- **Emergent beats the script.** If the sim kills a cast member early (a stray bullet, a blitz), the story bends instead of breaking: every beat names a fallback (who stands in, or the beat is skipped and its flag set as if failed).
- **Proposed: story choices save at once** (ironman-lite for the story flags only), so one choice can't be save-scummed. See open questions.

### 14.2 The cast
Six people, each with an arc across the acts. Names are placeholders.

| Who | At the start | Arc | Where they can end |
|---|---|---|---|
| **Mamma** (Carmela) | Runs the basso, irons his shirts, pretends not to know. | Silence → complicity (keeps the money, hides a gun) → the cost (a search of her home, a son in the cemetery or in jail). Her trust is the game's moral thermometer. | Proud and broken at your side; leaves the rione to her sister in the province; at your funeral; testifying to keep your sister safe. |
| **The sister** (Assunta) | Seventeen, studying, wants out of the rione. | Wants nothing to do with it → is dragged in (a boyfriend in a rival clan, or the family's money pays her school) → her choice: leave, or take over when you can't. Ties into 7.3 (women ran clans when the men were inside). | Gone north with a degree and no phone number; the heir who runs the family; under protection with you. |
| **Don Saverio 'o Malommo** (exists) | Holds the corner for the people above him. First boss, first wages. | Teacher → obstacle (his corner is the only way up) → what you do with him decides the tone of Act 2: dead, paid to retire, or kept on as an adviser who knows where the bodies are. His fall at the midpoint is the cue for the succession war. | Killed by you, killed by the clan above for letting you rise, retired by the sea, or the first pentito who names you. |
| **The rival heir** (Gennarino, called *'o Principe*) | Son of the boss Saverio answers to. Your age; you played football together in the courtyard. | Friendly contempt → rivalry over the same corners → he inherits his father's clan in the succession war. The war is personal from both sides. | Dead by your hand or his own men's; your ally by a marriage (LifeCards); a pentito who sinks you both; in Poggioreale in the cell next to yours. |
| **The cop** (Ispettore Ciro Iovine) | Grew up two doors down. His mother still talks to yours. | Warns you as a friend → builds the file (evidence, microphones) → offers the deal. He is the face of Heat: the blitz, the warrant and the trial all come with his name on them. | Promoted on your arrest; transferred after you threaten his family (a heavy flag); your handler if you turn; dead, which brings the whole State down on the rione. |
| **The parish priest** (Don Mimì) | Runs the oratorio where you played as a kid; asks money for the festa (exists: PriestFesta card). | Takes your money for the church → refuses it after a killing → the one door out that isn't a cell or a coffin (he knows people in the anti-racket association and in a community up north). | Celebrates your funeral; marries your sister; walks you to the carabinieri; is threatened and leaves. |
| **The childhood friend** (Totore) | Your first guaglione (the first friend who calls, Ranks). Shape set by the Background: from the vicolo, from the Vele walkways, or met in Nisida. | Brother → your right hand, capo of the second piazza → under pressure (arrested, his family unpaid, Informants rumours) → pentito, dead, or the one who stays loyal to the end. Whether he flips depends on the sim's real numbers (loyalty, assistenza paid, prison time), not a script. | Pentito; killed by the rival; killed on your order; your heir; the last man standing at the end. |

### 14.3 Act 1 — *'O guaglione* (rank: Guaglione)
**Theme:** the street is an apprenticeship and it already costs something.

| Beat | Trigger | What the player does | Systems used | Choices → consequences |
|---|---|---|---|---|
| 1.1 Mamma's basso | New game | Wake up in the basso; Mamma asks for money for the rent. Find work. | Objectives, Contacts (Totonno), Contracts | None: sets the first goal (€ for the rent by Sunday). |
| 1.2 The first lavoretto | First job done | Totore turns up; he's in if you are. | Contracts, Ranks (first friend) | Take him on (he's crew) / tell him to stay out (he joins Saverio's corner instead: an ally inside the corner, later a problem). |
| 1.3 Eyes on the corner | Respect ≥ ~5 | Don Saverio offers lookout nights. | Contracts (Saverio's corner shift), Police | Work for him (trust +) / refuse (he remembers; 1.6 is harder). |
| 1.4 Ciro on the step | First police stop, or Heat above a low mark | Ciro walks up, off duty. *"Te cunosco 'a piccerillo."* (draft: "I've known you since you were small.") | Events walk-up, Heat | Listen (Ciro trust +, a hint about patrols later) / insult him (he opens a file early: evidence +) / say nothing. |
| 1.5 The first arrest | The sim produces one (a pursuit, a search), or a scripted shipment job at the port that goes wrong if it hasn't happened by respect ~12 | A night in the cell, or Totore taken instead. | Police, Pursuit, Justice (lawyer), Hospital | Who pays the lawyer: Saverio (debt to him), Mamma's savings (her trust −, she learns everything), or nobody (Totore does the time and comes out different). |
| 1.6 The corner (Act climax) | Respect enough for Paranza, and the money or the men | Take Saverio's corner. | Organization.Saverio (kill / buy out / weekly cut), RivalClan war if shots fired | **Kill him**: the clan above takes note (Act 2 starts at war with the heir's clan, fear +, Mamma learns at the market). **Buy him out**: slow, clean, he leaves owing you nothing. **Keep him as adviser** (new: the weekly cut plus he stays in the rione): cheaper Act 2 tips, but he knows everything (a pentito risk at the midpoint). |

**Act 1 ends** when the corner is yours and the rank is Paranza. Mamma's first real scene: she finds the money under the bed. *"Figlio mio, che hê fatto?"* (draft: "My son, what have you done?")

### 14.4 Act 2 — *'A paranza* (ranks: Paranza → Clan)
**Theme:** you're the one paying wages now, and everyone below you is someone's son.

| Beat | Trigger | What the player does | Systems used | Choices → consequences |
|---|---|---|---|---|
| 2.1 The heir's visit | Act 2 start | Gennarino comes to the piazza with two men: his father wants a cut, or the corner back. | RivalClan (pact), Events | Pay (a pact; peace but money out every Sunday) / refuse (stese on your piazza, OperationCards opens against them) / ask for time (a deadline). |
| 2.2 The second piazza | Rank Clan in reach | Open a second piazza; it needs a capo paranza. | Capi, Staffing | Make Totore capo (his loyalty +, but he's exposed) / someone else (Totore sulks; Character may turn him ambitious). |
| 2.3 Assunta's school | First Sunday with takings above a mark | Assunta's fees are due. | Money, Famiglia | Pay with the family's money (she stays, resentful, and is tied to you) / she works and pays herself (trust + but she's out of reach later) / she asks to leave for the north (the first "out" thread). |
| 2.4 The pizzo street | Rank Clan | Pizzo on the shops of your turf; one shopkeeper asks you to hold his child at the font. | Rackets, LifeCards (comparaggio), Pressure (anti-racket) | Squeeze hard (money; the anti-racket association forms sooner; Don Mimì refuses your festa money) / squeeze soft (less money, the rione protects you from Ciro's questions). |
| 2.5 The microphones | Informants detects a bug, or evidence ≥ ~40 | Ciro's file gets thick; rumour that someone talks. | Informants, Justice | Trust Totore (nothing happens yet) / test him with a false stash (Informants traps) / move everything. Sets the flag the pentito question reads in Act 3. |
| 2.6 **Midpoint: Saverio falls** | Clan rank and 2.1 resolved | Saverio (or, if he's already dead, his widow at your door) is found shot by the sea. The heir's father is arrested the same week in a blitz: the clan above is headless. | RivalClan (beheaded, successor), Newspaper front page, Justice (a blitz on *them*) | Go to the funeral (respect +, Heat +, Ciro sees who stands where) / stay away (the rione reads it as guilt). The heir now leads his clan and blames you. |

**Act 2 ends** when the heir takes over his father's clan. La Sirena runs it: "*Il Principe eredita il rione.*" (Italian headline, draft).

### 14.5 Act 3 — *'O sistema* (ranks: Clan → 'O Sistema)
**Theme:** the war you wanted, and what it eats.

| Beat | Trigger | What the player does | Systems used | Choices → consequences |
|---|---|---|---|---|
| 3.1 Succession war | Act 3 start | The heir's clan and yours fight for Saverio's old rioni; other clans pick sides. | RivalClan (stese, agguati, piazza takeovers), OperationCards, Response | Ally with a third clan by marriage (LifeCards: peace with them, a crew member married off; Assunta can be the one offered: her trust collapses if you agree) / tribute / go it alone. |
| 3.2 The baby paranza | Mid-war, crew short | Kids from the vicoli want guns. | LifeCards (baby paranza) | Arm them (cheap soldiers; a kid dies and his mother comes to the door, MotherPlea) / send them home (Don Mimì trust +). |
| 3.3 Totore inside | Totore arrested (blitz, a hit gone wrong), or scripted at the first blitz if he's still free | Totore in Poggioreale. His family needs the assistenza. | Justice, JailCards (colloquio), Money (assistenza ai carcerati) | Pay his family every week / don't / pay and send a message through the colloquio. His flip roll uses the real loyalty and assistenza numbers: the story only makes sure the question is asked. |
| 3.4 **The pentito question** | Totore's roll, or Ciro's offer to the *player* after a warrant | Either Totore turns (you learn from La Sirena), or Ciro sits across from you: *"Pienzace."* (draft: "Think about it.") | Informants, Leadership (warrant), Justice | If Totore turned: hide (latitanza) / reach him inside (a hit in Poggioreale: the darkest beat, Mamma's trust and Don Mimì gone) / let it go to trial. If Ciro offers: refuse / accept (→ Pentito ending path). |
| 3.5 Poggioreale or the bunker | Warrant issued | Latitanza or arrest. | Leadership (bunker, pizzini), JailCards; 7.2b map and 7.1b abroad when built | Run the family from inside / from the bunker; Assunta or a capo holds the outside (succession). |
| 3.6 The heir falls, or you do | The war decides: their boss dead or arrested, or yours | Last scene with Gennarino, wherever it happens (a hit, a cell, a funeral). *"Simme cresciute 'nzieme."* (draft: "We grew up together.") | OperationCards (hit on their boss), RivalClan beheading | Kill him / spare him (he becomes a pentito, or a debt) / make peace (one clan of two families). Leads into the endings. |

### 14.6 Act 4 — Endings
No ending is a win screen. Each closes on an epilogue card per cast member (where they are a year later), then La Sirena's last front page.

| Ending | What leads there | Epilogue tone |
|---|---|---|
| **'O Capo** (boss of the System) | Rank 'O Sistema, the heir's clan beaten or joined, free at the end of Act 3. | You won. Mamma doesn't come to dinner. Assunta isn't in the photos. A kid on your old corner does lookout nights for you. |
| **Pentito** | Accepted Ciro's offer (3.4), or turned yourself in after Totore talked. | A flat in a town with no name, a new surname. Your sister safe or not depending on whether she came. Mamma stays in the rione; the walls say what you are. The bloodline is cursed (3.1 dynasty). |
| **Latitanza abroad** | A warrant you can't beat and enough money (needs 7.1b; until then, an epilogue only). | A port city, the phone rings once a week, less every year. |
| **The name dies** | The boss is killed (any time after Act 1, or 3.6 goes the other way) **and there is no heir or capo to take over**, or a scissione (14.10) leaves nobody loyal. | The last funeral; who comes. Don Mimì says the mass, or refuses. Totore's fate decides who carries the coffin. |
| **Fine pena mai** (life in jail) | Convicted at the trial in 3.5 with no way out. | The colloquio glass, Mamma older every Saturday. Assunta or a capo runs what's left. |
| **Out clean** (the hard one) | Low violence across all acts (no civilian killed, Saverio bought out not shot, baby paranza sent home), Don Mimì's trust high, Assunta out, and you hand the piazze over or close them. | A job in a community up north, or a bar that pays its pizzo to someone else. It's possible and it's small. Answers open question 12 "is there a clean ending": yes, and it costs everything you built. |

**A boss's death is not an ending** while someone can carry the name: play passes to the heir (14.9) and the story goes on from the current act. The endings above are reached by whoever holds the name when Act 3 resolves, and their epilogue names every boss the family buried on the way.

### 14.7 Tone notes
- **Show the bill, every act.** Each act's climax has a cost scene for someone not in the System: Mamma at the market (1), the shopkeeper whose child you baptised (2), a mother at the door after the baby paranza (3).
- **No victory music.** Rank-ups and the 'O Capo ending are played quiet. The loudest moments are funerals and the blitz at five in the morning.
- **Violence is cheap to order and expensive to live with.** Operation cards stay a few clicks; their consequences come as people, days later.
- **Nobody is a cartoon.** Ciro isn't a hero, Gennarino isn't a monster, Don Mimì takes money too. Everyone has a reason.
- **The rione is the chorus.** Crowds after a fight, the neighbours' gossip, La Sirena, the Napoli match: the existing street life reacts to story beats (a funeral procession, shutters down after a hit).
- **Dialect:** a line or two per scene at most, as in Dialect.cs. English carries the meaning.

### 14.8 Death is final
Today a shot that takes a pawn to zero HP puts them on the ground with a wound that bleeds for most of a day, and the player's own bleed is slowed (`CombatSystem`, `Health.Downed`). That is too kind. The new rule, for **everyone** (the player, crew, rivals, police, civilians), in both modes:
- **A fatal hit kills on the spot.** A head shot, or a hit to the chest that rolls high (heart, lungs, a big artery), is death: no downed state, no crawl to cover. Body armour is the only thing that turns a fatal hit into a survivable one, which makes vests a real decision.
- **Everything else bleeds, fast.** A torso wound that isn't fatal at once bleeds out in **game minutes** (roughly 20–60) unless tended; a limb wound in a few hours. Downed pawns can still be carried, tended by a crew medic, or finished (Execute).
- **The 118.** Anyone nearby can call it: a crew member, a passer-by (more likely in daylight and in front of shops), or the player from his phone if he's conscious. The ambulance and paramedics already exist; they come in a few minutes, stabilise, and take the wounded to the Hospital. **The hospital reports gunshot wounds** (GDD 4.2): Heat, a police visit at the bedside, evidence. The bent doctor (a new contact) is slower and quieter.
- **Beatings stay non-lethal** unless they go on after the victim is down (a choice, with its own weight).
- **No downed grace for the player.** The player bleeds like everyone else. If he dies, play passes to the heir at once (14.9); if there is none, the name dies (14.6). In story mode this is never a game-over screen while the family exists.
- **Saves:** death this harsh invites reloading. Proposed: autosave on boss death that can't be skipped, so the heir's first scene always plays (see open questions).
- **Tuning target:** a gunfight should be over in seconds and nobody should want one. The sim already prefers negotiation (GDD 5.5); this makes the preference felt.

### 14.9 Generations
The dynasty is already in the design (GDD 3.1: "a run = one boss's life", the heir inherits) and partly in the code: `Organization.Generation` is saved, `CheckSuccession` raises a "Who leads the family now?" card with the three best candidates by loyalty and intimidation, and `AppointBoss` cuts respect by a quarter, resets evidence and has the rivals test the new boss. Building on that:
- **Marriage and children become a mechanic.** The boss can marry (a fidanzata met in the rione, or a marriage alliance from LifeCards); the couple lives in a home of the family's; children are born and grow up. They are people in the Famiglia roster with traits shaped by what they're exposed to (GDD 7.3): a son who sees the stash grows *Ambitious* or *Hothead*; a daughter kept away from it may refuse the name.
- **Time.** A child takes years to come of age, and the sim runs in days. Proposal: when the boss dies and the heir is a minor, a **regent** takes over (the widow, the sister, Mamma, or a capo) and the player plays the regent; when the regent dies, retires or chooses to hand over, a **time skip** of several years is simulated cheaply (like districts nobody is watching: piazze sell, heat cools, clans move), and the child comes of age as the new boss.
- **Who can be heir**, in order of legitimacy: a son or daughter of age, the widow or husband, a sibling, a nephew, then a chosen **capo** (no blood: less legitimacy, more scissione risk). The player picks from the eligible ones on the existing succession card.
- **What carries over:**
  - *The name*: feared, honoured or cursed (a pentito in the bloodline curses it; GDD 3.1).
  - *Territory*: the piazze and rioni the family still holds on the day of death (minus whatever a scissione takes).
  - *Money*: the bacinella and stashes the heir knows about. Stashes only the dead boss knew are lost unless a pizzino or Mamma reveals them.
  - *Reputation*: respect cut (as today), more for a capo heir than for a son.
  - *Enemies and grudges*: every rival clan's hostility, vendettas for men killed, debts owed and owed to the family. A rival whose father you killed comes for the son.
  - *NPC memory*: shopkeepers, contacts and cast remember what the last boss did (GDD 3.1).
  - *Evidence*: the dead boss's file closes; the heir starts with a smaller one of their own (today: evidence 20).
- **In story mode:** the acts belong to the family, not one man. If the boss dies mid-act, the heir picks up the current beat; the cast re-aims at the heir (Mamma becomes the grandmother, Assunta the aunt or the heir herself, Ciro opens a file on the new name, Gennarino goes after the son). Each beat already names a fallback (14.1); succession is the biggest one. Generation flags join the endings: an 'O Capo ending reached in the third generation reads differently from one in the first.
- **In sandbox:** generations run without limit, as GDD 3.1 intended.

### 14.10 Scissione
When a boss dies, or is jailed for life, **any clan** can split: the player's family and the rival clans alike. GDD 6.2 already promises it ("this can happen to you"); `Organization.Character` already has one ambitious capo walking out with his piazza to the nearest clan. Scissione is the bigger version: the capo founds a **new clan**.
- **When:** checked at each succession (and again weekly for a few weeks after, while the new boss is unproven). Also possible without a death when unrest runs high for long (unpaid wages, a humiliated capo), as GDD 6.2 describes.
- **Who splits:** each capo works out whether he accepts the successor:
  - his *loyalty* to the family;
  - his *respect for the heir* (the heir's respect and intimidation; blood legitimacy; a capo raised over his peers is resented by them);
  - *who controls the piazze*: a capo whose piazza earns well and whose crew is loyal to him more than to the family can afford to go;
  - *money*: wages paid on time, assistenza paid to jailed men's families, the bacinella's health;
  - grudges against the dead boss or the heir.
- **The warning:** low acceptance first shows as word reaching the boss (gossip, Informants, a capo who stops answering the phone), a few days before the split. That window is the player's chance: pay, promote, give him a second piazza, a marriage, a comparaggio, or kill him first (the others watch, and fear and resentment both rise).
- **The split:** the capo takes his piazze, the crew who are more loyal to him than to the family, part of whatever stash he guarded, and a name of his own. He becomes a new rival clan with a feud already open (high hostility, vendettas both ways). Several capi can go together, or each his own way.
- **For rivals:** a beheaded rival clan (`RivalClan` already handles "the boss came out and didn't come back") rolls the same check; a split rival is a chance (two weak clans) and a danger (more men with guns and nothing to lose).
- **In story mode:** a scissione is a beat whenever it happens. Two places it is likely by design: the heir's clan after its boss is arrested at the midpoint (2.6), which can turn the Act 3 war three-way; and the player's own family after a boss's death, where the heir's first job is holding it together. Totore, as capo of the second piazza, is the cast member most likely to lead one.
- **In sandbox:** the same rules, with a setting for how readily clans split.

### 14.11 Sandbox
Today's free play, unchanged, with settings at new game (all can be changed until day 1 ends, then locked):
- **Start:** money (low / normal / high), rank (Guaglione / Paranza / Clan: higher ranks start with a piazza and crew placed).
- **Clans:** how many rival clans (1–4) and how aggressive (patient / normal / violent: maps to the storyteller modes in 3.3).
- **Police:** harshness (lenient / normal / hard: evidence gain, blitz threshold, trial odds).
- **Death:** final (default) / forgiving (today's long bleed, for players who want the sim without the stakes).
- **Scissione:** how readily clans split (rare / normal / often).
- **Story events:** on (the cast still exists and random scenes with them fire, no acts) / off (only the generic event deck).
- **Don Saverio:** present (normal start) / absent (the corner is free from day one).
- **Achievements** are earned in both modes; a few story-only (each ending).

### 14.12 New tech needed (build order)
1. **Story state in the save:** a `StoryState` (mode, act, beat index, flags, per-cast numbers: trust, debt, grudge, alive/where). Saved with the family. Sandbox has it empty.
2. **Cast as persistent people:** the six as named Npcs with homes and routines, reusing Contacts' "a life" (home, spot, break). Saverio exists; the rest are new. Each can die in the sim and the state notices.
3. **Beat engine:** a list of beats, each with a *trigger* (a condition over game state: rank, respect, money, a piazza, a clan's state, Heat, a flag), a *nudge* after N days waiting (a call, a visit), and *fallbacks* if a cast member is gone. Checked once a game hour.
4. **Objectives hook:** a beat sets the current objective (UI/Objectives.cs already works out "what next"; story goals take priority over the tutorial ones) with a map marker.
5. **Scene cards:** a GameEvent with a portrait, a speaker per line and choices that set story flags. Delivered through the existing walk-up or phone call.
6. **Beat-specific hooks** into existing systems: Saverio's adviser option, the scripted fallbacks (the port job that goes wrong, Totore's arrest at the first blitz), Ciro's offer as a choice on the warrant, the heir as the rival boss with a fixed name.
7. **Epilogues:** an end screen of cast cards and a last La Sirena front page, picked from flags.
8. **Mode select and sandbox settings** on the new-game screen (Hud.Creator), feeding Game setup.
9. **Harsh death** (14.8): fatal-hit roll by body part and armour in `CombatSystem`, fast bleed rates, the 118 called by bystanders and crew, the hospital report, a bent doctor contact. Independent of the story engine: can be built first, in both modes.
10. **Scissione** (14.10): an acceptance score per capo, the warning window, and creating a rival clan at runtime from a split (name, rioni, piazze, men, open feud). Extends `Organization.Character`'s defection and `RivalClan`'s beheading.
11. **Generations** (14.9): marriage, children as roster people who age, the regent, the time skip (reusing the offline district tick), inheritance rules on top of `CheckSuccession` / `AppointBoss`, and the cast re-aiming at the heir.
12. **Content pass:** write all scenes, then native dialect review.

Steps 1–5 are the engine and can ship with Act 1 alone as a test (it's also a better tutorial than today's objectives). Acts 2–3 need nothing new beyond step 6; the endings need 7. Steps 9 and 10 matter to sandbox as much as story and can come before the engine; step 11 is the largest and can follow Act 1. Latitanza abroad and the Poggioreale map are not required: the story uses today's screens and upgrades when 7.1b and 7.2b land.

### 14.13 Open questions
- Does story mode use procedural maps and clans (with fixed cast inserted) or one hand-made district? Recommendation: procedural, fixed cast and fixed heir's clan name.
- Is the player always male in story mode, or can the creator pick, with the cast's lines adjusted?
- Names: are Carmela, Assunta, Gennarino *'o Principe*, Ciro Iovine, Don Mimì and Totore right, and safe (not echoing real people)?
- Don Saverio as a possible adviser after Act 1: worth the extra system, or keep only kill / buy out?
- Should the pentito ending be playable for a while (the protection programme as a short mode) or an epilogue only?
- Ironman-lite story flags: wanted, or plain saves?
- Does the clean ending need its own act-4 chapter (getting out has to be *played*), or is the epilogue enough?
- How dark may 3.4's "reach him inside" go on screen?
- Harsh death and saves: an unskippable autosave on boss death, full ironman, or free reloading?
- Generations: regent plus time skip, or something else (heirs only of age, the run waits for a child)? How many years per skip?
- Can the player pick a daughter, the widow or the sister as heir freely, or does the rione's old-fashioned view cost them respect at first (as it would)?
- Scissione: should the player ever be able to *lead* one (as a capo in another boss's clan), or only suffer and cause them?
