# World art for 'O Sistema: prompts for ChatGPT (furniture, ground, vehicles, doors)

**How to use this:** open a new ChatGPT chat for each group (furniture, ground, vehicles). Send each prompt as its own message, and attach one style reference to every message: a screenshot of the game (one of the interior shots, e.g. `Screenshots/interiors-in-game-v1.jpg`) works best.

Save each result into `Assets/_Project/Art/World/` with the file name given (create the folder). Tell me when a batch is in and I'll cut and fit it. If one or two cells are bad, keep the sheet: I'll skip those. If the whole sheet is wrong (perspective, shadows on the ground, text, a background), reply: *"Redo it. Follow every rule exactly, especially: [the rule it broke]."*

Every prompt repeats all the rules it needs, so ChatGPT never has to remember an earlier message.

**Why the cell shapes matter:** the game puts furniture on a grid of 1×1 metre squares. A sofa is 2 squares long and 1 deep, a double bed 2×2. If the picture's shape matches the footprint, nothing gets squashed or cut. So each sheet below holds only one footprint size.

---

## A. Furniture

### A1. Small pieces, 1×1 (`furn_1x1_home.png`)

> Make a sprite sheet of furniture for a 2D video game seen from directly above.
>
> **Rules:**
> - One square image, 2048×2048 pixels if possible, as a **4×4 grid of 16 equal square cells**. Do NOT draw the grid lines. One object per cell, centred, filling about **85% of the cell** and touching nothing; nothing crosses a cell's edge.
> - **Transparent background.** No floor, no rug under it, no shadow on the ground, no text, no labels, no logos.
> - **Camera straight down from the ceiling** (pure top view, orthographic, no perspective): you see the top surfaces of the furniture, not their fronts. The front of each piece (where you'd stand to use it) faces the **bottom** of the image.
> - **Style:** semi-realistic hand-painted game art, soft natural light from the top-left, gentle shading, real materials (worn wood, chipped Formica, brass, marble), muted warm colours of an old Neapolitan home. **No black outlines**, no cartoon style.
> - Every object in the same scale: each cell is 1 metre by 1 metre.
>
> The 16 objects, left to right, top to bottom:
> 1. a wooden dining chair with a rush seat
> 2. a small round bedside table with a lace doily and a lamp
> 3. a tall dark-wood bookcase seen from above (the top of the case, books just visible at the front edge)
> 4. a white washing machine, round door at the front edge
> 5. a fridge from the 1980s, cream coloured, seen from above
> 6. an old cathode-ray television on a small stand
> 7. a standing floor lamp with a fabric shade
> 8. a potted fern in a terracotta pot
> 9. a big potted palm in a glazed pot
> 10. a small iron safe
> 11. a home shrine: a little shelf with a Madonna statue, candles and plastic flowers
> 12. a wooden coat stand with a coat hanging
> 13. a laundry basket with folded sheets
> 14. a gas cylinder (orange bombola) next to a small stove top
> 15. a wicker armchair
> 16. a bar stool with a red vinyl seat

### A2. Long pieces, 2×1 (`furn_2x1_home.png`)

> Make a sprite sheet of furniture for a 2D video game seen from directly above.
>
> **Rules:**
> - One image **2048 pixels wide and 2048 tall**, divided into **2 columns and 4 rows** of equal cells, so each cell is **twice as wide as it is tall** (1024×512). Do NOT draw the grid lines. One object per cell, lying **horizontally**, filling about **90% of the cell's width and 85% of its height**, touching nothing.
> - **Transparent background.** No floor, no shadow on the ground, no text, no labels, no logos.
> - **Camera straight down from the ceiling** (pure top view, orthographic, no perspective). The front of each piece (the side you sit on or open) faces the **bottom** of the image.
> - **Style:** semi-realistic hand-painted game art, soft light from the top-left, real materials, muted warm colours of an old Neapolitan home. **No black outlines**, no cartoon style.
> - Same scale everywhere: each cell is 2 metres long by 1 metre deep.
>
> The 8 objects, left to right, top to bottom:
> 1. a three-seat sofa in faded red velvet with crocheted doilies on the arms
> 2. a green floral sofa with two cushions
> 3. a tall dark wooden wardrobe with two doors (seen from above: its top, with a suitcase on it)
> 4. a chest of drawers with a mirror lying flat, a comb and perfume bottles on top
> 5. a kitchen counter with a sink, a moka pot and a drying rack
> 6. a kitchen table with a red-and-white checked tablecloth and two plates
> 7. a dark wooden sideboard with a lace runner, a fruit bowl and framed photos
> 8. a low TV cabinet with a flat television and a vase

### A3. Big pieces, 2×2 (`furn_2x2_home.png`)

> Make a sprite sheet of furniture for a 2D video game seen from directly above.
>
> **Rules:**
> - One square image, 2048×2048 if possible, as a **3×3 grid of 9 equal square cells**. Do NOT draw the grid lines. One object per cell, centred, filling about **88% of the cell**, touching nothing.
> - **Transparent background.** No floor, no shadow on the ground, no text, no logos.
> - **Camera straight down from the ceiling** (pure top view, orthographic). The front (foot of the bed, the side you sit at) faces the **bottom** of the image.
> - **Style:** semi-realistic hand-painted game art, soft light from the top-left, real fabrics and wood, muted warm colours. **No black outlines**, no cartoon style.
> - Same scale everywhere: each cell is 2 metres by 2 metres.
>
> The 9 objects:
> 1. a double bed with a white crocheted bedspread, two pillows at the top, a carved wooden headboard at the top edge
> 2. a double bed with a burgundy satin bedspread and a framed Madonna picture on the headboard
> 3. two single beds side by side for children, football blankets (sky blue)
> 4. a square dining table with four chairs tucked in, a white tablecloth and a bowl of pasta
> 5. a corner sofa (L shape) in brown leather
> 6. a Persian-style rug, red and blue (flat, no furniture on it)
> 7. a worn rag rug, oval, faded colours
> 8. a card table with four chairs, green felt top, cards and coins on it
> 9. a bunk bed seen from above (the top bunk with a rumpled blanket)

### A4. Shop pieces, 1×1 (`furn_1x1_shops.png`)

> Make a sprite sheet of shop furniture for a 2D video game seen from directly above, set in Naples.
>
> **Rules:**
> - One square image, 2048×2048 if possible, as a **4×4 grid of 16 equal square cells**. Do NOT draw the grid lines. One object per cell, centred, filling about **85% of the cell**, touching nothing.
> - **Transparent background.** No floor, no shadow on the ground, no text or readable writing anywhere, no brand logos.
> - **Camera straight down from the ceiling** (pure top view, orthographic). The side a customer stands at faces the **bottom** of the image.
> - **Style:** semi-realistic hand-painted game art, soft light from the top-left, real materials, muted colours. **No black outlines**, no cartoon style.
> - Same scale everywhere: each cell is 1 metre by 1 metre.
>
> The 16 objects:
> 1. a shop till (cash register) on a small counter
> 2. a newspaper stand with folded papers
> 3. a glass-front drinks fridge, bottles seen through the top glass
> 4. a cigarette display cabinet with packs in rows
> 5. a slot machine (video poker) seen from above
> 6. a round marble café table with two espresso cups
> 7. an ice-cream freezer with tubs of coloured gelato under glass
> 8. a wooden crate of lemons and oranges
> 9. a stack of flour sacks
> 10. a wine rack with bottles
> 11. a bread basket rack with loaves
> 12. a pharmacy shelf unit with white boxes (the top of the unit)
> 13. a jewellery case: glass top, rings and chains on dark velvet
> 14. a clothes rail with shirts on hangers
> 15. a stack of car tyres
> 16. a mechanic's toolbox on wheels, red

### A5. Shop pieces, 2×1 (`furn_2x1_shops.png`)

> Make a sprite sheet of shop furniture for a 2D video game seen from directly above, set in Naples.
>
> **Rules:**
> - One image 2048×2048, divided into **2 columns and 4 rows** of equal cells, so each cell is **twice as wide as tall**. No grid lines. One object per cell, lying **horizontally**, filling about **90% of the width and 85% of the height**, touching nothing.
> - **Transparent background.** No floor, no shadow on the ground, no text, no logos.
> - **Camera straight down from the ceiling** (pure top view, orthographic). The customer side faces the **bottom** of the image.
> - **Style:** semi-realistic hand-painted game art, soft light from the top-left, muted colours. **No black outlines**, no cartoon style.
> - Each cell is 2 metres by 1 metre.
>
> The 8 objects:
> 1. a long pizzeria counter in white marble with dough balls and a tomato bowl
> 2. a bar counter in dark wood with an espresso machine and cups
> 3. a deli counter: curved glass over salami, cheeses and olives
> 4. a butcher's counter: glass over cuts of meat, a chopping block
> 5. a pharmacy counter, white, with a small scale and boxes
> 6. a tobacconist's counter with a lottery ticket stand and lighters
> 7. a display table of folded clothes and handbags
> 8. a mechanic's workbench with a vice and scooter parts

### A6. Wood-fired oven and big fittings, 2×2 (`furn_2x2_shops.png`)

> Make a sprite sheet of shop fittings for a 2D video game seen from directly above, set in Naples.
>
> **Rules:**
> - One square image, 2048×2048, as a **3×3 grid of 9 equal square cells**. No grid lines. One object per cell, centred, filling about **88% of the cell**, touching nothing.
> - **Transparent background.** No floor, no shadow on the ground, no text, no logos.
> - **Camera straight down from the ceiling** (pure top view, orthographic). The working side faces the **bottom** of the image.
> - **Style:** semi-realistic hand-painted game art, soft light from the top-left. **No black outlines**, no cartoon style.
> - Each cell is 2 metres by 2 metres.
>
> The 9 objects:
> 1. a domed wood-fired pizza oven in tiles and brick, the mouth at the bottom edge with embers glowing
> 2. a fruit-and-veg market stall with a striped awning seen from above (the awning half rolled back, crates visible)
> 3. a scooter on a display stand in a showroom
> 4. a table for rolling cigarettes: tobacco, papers, a little wooden rolling machine, empty packs
> 5. a table for bagging: a digital scale, small plastic bags, a roll of tape (nothing illegal shown explicitly, just white powder in a bowl)
> 6. a pool table (biliardo), green felt, balls racked
> 7. a market stall of fake designer bags and Napoli football shirts on a folding table
> 8. a stall of contraband cigarettes: cartons on a folding table under an umbrella
> 9. a church pew pair (two short wooden benches side by side)

---

## B. Ground textures (Naples)

Send each as its own message. Each result is **one seamless texture**, 1024×1024 or 2048×2048.

> Make a **seamless, tileable texture** for the ground in a 2D video game seen from directly above, set in the historic centre of Naples.
>
> **Rules:**
> - One square image, 2048×2048 if possible. It must **tile seamlessly**: the left edge continues into the right edge and the top into the bottom, with no visible seam.
> - **Pure top view**, flat, no perspective, no horizon, no objects, no people, no shadows of buildings, no text.
> - **Style:** semi-realistic hand-painted game art, soft even daylight, muted real colours. No black outlines.
> - The texture covers about **4 metres by 4 metres** of ground.
>
> The ground: **[pick one from the list below and paste it here]**

1. `ground_basoli.png`: Neapolitan *basoli*: large dark grey volcanic lava-stone slabs, rectangular, laid in diagonal rows, polished smooth by centuries of feet, thin dark joints, a few cracks, a little grime.
2. `ground_basoli_wet.png`: the same basoli after rain: darker, glossy, small puddles reflecting a pale sky.
3. `ground_sanpietrini.png`: small square grey cobbles (sampietrini) in fan-shaped arcs, some sunken, weeds in a few joints.
4. `ground_marciapiede.png`: an old pavement of large pale grey stone slabs (piperno) with chipped edges and patched cement.
5. `ground_asphalt_patched.png`: worn asphalt of a narrow city street, patched in different shades, a faded white line fragment, cracks, a small manhole lid.
6. `ground_piazza.png`: a piazza paved in big light-grey stone with a darker lava-stone border pattern.
7. `ground_vicolo.png`: the floor of a narrow vicolo: uneven basoli, a gutter channel down the middle, damp corners.
8. `ground_cotto.png`: indoor floor of square terracotta tiles (cotto), worn and waxed.
9. `ground_maiolica.png`: indoor floor of old Neapolitan majolica tiles, blue and yellow patterns, some cracked.
10. `ground_graniglia.png`: indoor *graniglia* floor (terrazzo of the 1950s): speckled tiles in a two-colour checker.
11. `ground_marble.png`: a polished white marble floor with grey veins (for a rich shop or the boss's house).
12. `ground_lastrico.png`: a flat roof (lastrico solare): tarred and patched, with a few pebbles.
13. `ground_garden.png`: a small courtyard garden: earth and tufts of grass, some fallen lemon leaves.

Separate prompt for the **kerb edge** (`ground_kerb.png`):

> Make a texture strip for a 2D top-down video game set in Naples: **the edge of a pavement (kerb) seen from directly above**. One image 2048 wide by 256 tall. A row of long grey lava-stone kerbstones running left to right across the whole width, worn and chipped, with a thin strip of road asphalt along the bottom edge and a thin strip of pavement slabs along the top edge. It must **tile seamlessly left to right**. Pure top view, no perspective, no shadows, no text. Semi-realistic hand-painted game art, muted real colours, no black outlines.

---

## C. Cars and emergency vehicles

> Make a sprite sheet of vehicles for a 2D video game seen from directly above, set in Naples today.
>
> **Rules:**
> - One image **2048 pixels wide and 2048 tall**, divided into **4 columns and 2 rows** of equal cells, so each cell is **twice as tall as it is wide** (512×1024). Do NOT draw the grid lines. One vehicle per cell, **pointing DOWN** (the front bumper towards the bottom edge), centred, filling about **80% of the cell's height**, touching nothing.
> - **Transparent background.** No road, no shadow on the ground, no text, no readable number plates, no brand logos.
> - **Camera straight down from above** (pure top view, orthographic, no perspective): you see the roof, the bonnet and the windscreen from above.
> - **Style:** semi-realistic hand-painted game art, soft light from the top-left, real paint with a little dust and wear, glass slightly reflective. **No black outlines**, no cartoon style.
> - **Same scale in every cell:** a small city car is about 3.6 m long, a van about 5 m. Draw them at their true sizes relative to each other (so the small cars don't fill the whole cell).
>
> Sheet 1 (`cars_city.png`), the 8 vehicles:
> 1. a small old white Fiat Panda-style city car, a dent in the door
> 2. a small red Fiat 500-style car (modern)
> 3. a grey family hatchback, roof bars
> 4. a dark blue small hatchback with one mismatched door
> 5. an old green Fiat 126-style tiny car
> 6. a black saloon car with tinted windows (a boss's car)
> 7. a white small delivery van (Fiat Doblò style)
> 8. a three-wheeled Ape (Piaggio style) with a little flatbed of crates

Sheet 2 (`cars_emergency.png`), same rules, the 8 vehicles:
1. an Italian police car (Polizia): white and light-blue livery, blue light bar on the roof (no readable words)
2. the same police car with the light bar glowing blue
3. a Carabinieri car: dark navy blue with a red stripe along the side, light bar on the roof
4. an ambulance (118): white van with orange and fluorescent stripes, light bar, a red cross shape on the roof
5. a Guardia di Finanza car: grey with yellow-green details
6. a fire engine (Vigili del Fuoco): red truck, ladder on the roof (draw it as long as a van, fits the cell)
7. a rubbish lorry (white and green city waste truck)
8. a police van (blue and white minibus), for raids

Sheet 3 (`cars_parked.png`), same rules, 8 more everyday cars in different colours (silver, beige, burgundy, black, white, yellow, light blue, dark green), all slightly dusty, one with a cover half on, one with a scooter-shaped scratch: so the parked streets don't all look the same.

---

## D. Doors (`doors.png`)

> Make a sprite sheet of doors for a 2D video game seen from directly above, set in old Naples.
>
> **Rules:**
> - One image 2048×2048, divided into **4 columns and 4 rows** of equal square cells. No grid lines. One door per cell, drawn as a **door seen from directly above while it stands closed in a wall**: a long thin shape lying **horizontally** across the middle of the cell, as wide as the cell (one metre), about **one fifth of the cell tall** (the door's thickness and frame seen from above), with the threshold step on the bottom side.
> - **Transparent background.** No wall, no floor, no shadows, no text.
> - Pure top view, orthographic. Semi-realistic hand-painted game art, soft light from the top-left, no black outlines.
>
> The 16 doors (cells 1–8 closed, cells 9–16 the same doors swung **open 90°**, hinged on the left, so the door leaf now points DOWN from the left end of the frame):
> 1. a basso's double wooden door, green paint peeling
> 2. a heavy palazzo portone in dark carved wood with brass studs
> 3. a shop's rolled-up metal shutter (saracinesca), grey
> 4. a glass shop door with an aluminium frame
> 5. a simple interior door, white painted wood
> 6. an interior door in varnished wood
> 7. a reinforced steel door (porta blindata), dark brown
> 8. a church door in old oak with iron bands
> 9–16. the same eight doors, open

---

## E. If the minigames need art

The rolling and bagging minigames are drawn in code for now. If you want them painted, these are the pieces:

> Make a sprite sheet for a minigame in a 2D video game: a table seen **from directly above**, close up. One square image 2048×2048 as a **3×3 grid of 9 equal square cells**, one object per cell, centred, **transparent background**, no shadows on the ground, no text, no logos. Semi-realistic hand-painted game art, warm lamp light from the top-left, no black outlines.
>
> 1. a worn wooden tabletop (fill the whole cell edge to edge, seamless)
> 2. a small heap of loose cut tobacco
> 3. a single rolling paper, flat
> 4. a finished hand-rolled cigarette, lying horizontally
> 5. an empty red cigarette pack, open, seen from above (no brand, no writing)
> 6. a full pack, closed
> 7. a small digital pocket scale, display blank
> 8. a small clear plastic bag, empty, flat
> 9. a small knotted bag with white powder

