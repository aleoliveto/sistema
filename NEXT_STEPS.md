# Next steps (after the HUD overhaul, Oct 2026)

## 1. Playtest first (essential)
- **Cigarettes:** do days 1–3 feel tight but survivable? Does €7 a pack make lavoretti feel pointless? (`Prices.PackStreet`, `Organization.TotonnoDailyPacks`)
- **Big crew:** play to day 10–15 with 8+ crew. Do payday walk-outs and Mammà's lunch and dinner keep the family alive?
- **Capi:** with two piazze, appoint a capo. Do day and night teams hand over at the piazza, and does a piazza without a capo lose its nights? (`Organization.Capi.cs`, `PostJob`)
- **Bodyguards:** do Wide and Ahead behave in narrow vicoli? (`BodyguardJob`)
- **Lavoretti:** Taccuino has no button now (J still opens it). If jobs feel hidden, put the button back until a Lavori board exists.

## 1b. Territory (DONE, 2545b93: check it in play)
Today a rione is yours only with a foothold (a property or an owned shop inside it) and a piazza with a pusher (`Organization.ControlsZone`). Taking a rival piazza needs ~7 game hours with none of theirs within 9 cells, and it only becomes yours under the rank's piazza cap (Guaglione 0, Paranza 1, Clan 2, Sistema 3; `RivalClan.CheckCaptures`). Nothing on screen explains any of this.
- A piazza with a pusher should take the rione on its own; a foothold becomes a bonus (more guards, less heat).
- Loosen the early piazza cap, or let a captured rival piazza go over it.
- Say why a rione isn't yours yet (needs a pusher / a foothold / rank cap) on the map and in Piazza.
- Show a "holding the corner" progress bar while taking a rival piazza.

## 1c. Exhaustion (DONE: check the pass-out in play with dev switch "tired")
When Gennaro's sleep (`Needs.Rest`) runs out:
- **At 0:** he walks really slowly, his vision blurs now and then (a soft blur and darkening pulse over the screen, e.g. in `PostFx`), and he yawns (a bark plus a yawn sound).
- **Past 0, down to −10:** Rest has to be allowed below zero (or tracked as a separate "overtired" counter). At −10 he passes out where he stands: he drops to the ground, sleeps a few hours, and is vulnerable in the street (police, rivals, pickpockets).
- Apply the same to crew (slow and yawning), so the shift system and beds matter.

## 5. Content to add (agreed Oct 2026)
**Pacing** (DONE): friends call one at a time, days apart; favours from respect 12; pizzo at Clan; Zì Tore only after Paranza; Paranza needs 4 lavoretti.

**Early-game hustles** (DONE: Piazza › Rackets, crew jobs Parking and Stall, E by a parked car / stall / scooter): parcheggiatore abusivo · bionde stall · cavallo di ritorno · fake goods (bags, Napoli shirts)

**Mid-game rackets** (DONE: Piazza › Rackets): lotto clandestino with the smorfia · video poker and betting on Napoli · usura (shops that can't pay become fronts) · abusivismo (illegal rooftop floors, inspectors)

**The city's calendar:** Napoli match days · San Gennaro's blood · Christmas (presepe shops, tombola) · New Year (illegal fireworks, stese in the noise) · processions bowing at the boss's house

**People and ties:** comparaggio (godfather at baptisms and weddings; marriages between clans) · baby paranza · crew traits and relationships (an ambitious capo may split off) · a neomelodic singer

**Pressure and story:** a journalist · the anti-racket association · the dawn blitz, trials and witnesses · asset seizures · a nemesis rival boss arc

**At the bar:** scopa / briscola for favours and debts

## 6. From the Oct 23 playtest (not built yet)
**Done the same day:** slower walking and less wobble · selling by hand at your piazza and the stall (G) · the cavallo ridden to Nanninella on the real scooter · vest bought, not given · "Don" only at 'O Sistema · Clan needs a piazza and a full crew · money requests from Clan · less rain · pop-ups only for what involves you · phone by touch · building card scrolls · follow me · scooters swerve · gunfight camera · fewer lavoretti offers · shop counters reachable from the door · new rolling and bagging tables.

**Early game:**
- **Don Saverio owns the starting piazza.** To take it: kill him and his guards, or pay him to retire (a big sum, or a cut for a while). Until then you deal on his corner by his leave.
- **Inventory per character:** what each person carries (doses, packs, a pistol, cash, the stolen scooter's keys), not one family stock. Needed for selling from the pocket, searches, and crew on errands.

**The crew's homes (built):** people live in a house, not a bed (the basso or a flat, picked in Famiglia or their menu). Its beds rotate between the day and night shifts; no free bed → they sulk, then sleep on the floor. They eat at home, or at a pizzeria near their post on shift. Playtest: a flat with one bed and two on opposite shifts; the "beds short" warning.

**Scooters as things:** a scooter is parked somewhere and you walk to it; no pulling one out of your pocket. Crew take one from where it's parked. How riders sit and look on scooters (seat position, legs, passenger).

**Cars:** parked cars on every street (the parcheggiatore needs them), moving traffic, police and ambulance cars (prompts in ART_PROMPTS_WORLD.md).

**Clothes:** a clothing shop; buy outfits and change at the dresser at home (the body sprite is the outfit).

**Doors:** painted doors that open and close (prompts in ART_PROMPTS_WORLD.md).

**Modes:** a story mode (chapters, the rise of one family) and a sandbox mode (today's game).

**Naples, not a grid:** curving vicoli, stairs (gradoni), piazzette, the sea front, churches, laundry between balconies, real ground textures (ART_PROMPTS_WORLD.md, section B). More realism in general.

**Art to make (ChatGPT, ART_PROMPTS_WORLD.md):** furniture by footprint so nothing is cut wrong, ground textures, cars and emergency vehicles, doors, and optional minigame art.

## 2. Animation (STEP 1 DONE: arms swing, recoil, flinch, fall, sleep breathing; next: shoes, scooter lean, work hands, punches)
Painted bodies are single images with the arms painted in, so the walk is only a small sway and reads as sliding (`Views/PersonRig.cs`).
1. **Split the painted body** into torso, two arms and two shoes, painted with `Tools/ArtGen` in the same style. Load them in `CharacterArt` and give them their own transforms in `PersonRig`.
2. **Walk and run:** shoes step out front and back, arms swing against them, a clear bob, all stronger when running.
3. **Shoot:** gun arm extends, recoil on each shot plus muzzle flash; a flinch when hit; a fall when shot down instead of snapping to lying.
4. **Sleep:** slow breathing, an occasional "Zzz".
5. **Ride:** arms on the handlebars, a lean into turns, the passenger holding on.
6. **Work:** hand motion at the rolling, bagging and cooking tables.
7. **Punch:** a wind-up, the hit, a stagger on the receiver.

## 3. UI polish (quality of life)
- Painted map image (ground and roofs) in place of the pixel render.
- Inspector card (bottom-left, click a person or building): frame and field rows like the person page.
- Event cards, shop, pause menu, game over: give them the painted pass.
- "New" alerts on Phone and Taccuino: a red dot like News, not a gold rim that looks like "selected".
- Clan threat bar: show the angriest clan's name.
- 'O SISTEMA logotype for the title screen and the top bar (`ui.py`).

## 4. Bigger additions (later)
- Lavori board: job cards with pay, risk and crew needed, assigned from there.
- Phone contacts screen (portraits, relationship, services) alongside the candybar.
- Territory map view: rioni tinted by owner, a side panel per rione.
- Painted portraits for contacts and capi.
- Heat as one % (police attention on piazze plus evidence).
- Organigram drawn as a tree with lines.

## Tools
- UI art: `cd Tools/ArtGen && python3 ui.py ../../Assets/_Project/Resources/UI/Art` (drawn at 2×, frames are 9-slice; import settings in `Editor/UiArtImport.cs`).
- UI tour screenshots: write `uitour piazza crew` into `~/Library/Application Support/DefaultCompany/O Sistema/dev_autoplay`, press Play, and read the `uitour/` folder. Delete the file afterwards.
- Soak test: write `soak` into the same file.
