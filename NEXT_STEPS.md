# Next steps (after the HUD overhaul, Oct 2026)

## 1. Playtest first (essential)
- **Cigarettes:** do days 1–3 feel tight but survivable? Does €7 a pack make lavoretti feel pointless? (`Prices.PackStreet`, `Organization.TotonnoDailyPacks`)
- **Big crew:** play to day 10–15 with 8+ crew. Do payday walk-outs and Mammà's lunch and dinner keep the family alive?
- **Capi:** with two piazze, appoint a capo. Do day and night teams hand over at the piazza, and does a piazza without a capo lose its nights? (`Organization.Capi.cs`, `PostJob`)
- **Bodyguards:** do Wide and Ahead behave in narrow vicoli? (`BodyguardJob`)
- **Lavoretti:** Taccuino has no button now (J still opens it). If jobs feel hidden, put the button back until a Lavori board exists.

## 2. Animation (high value for feel, trailers and screenshots)
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
