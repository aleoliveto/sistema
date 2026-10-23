# People art for 'O Sistema: prompts for ChatGPT

**How to use this:** open a new ChatGPT chat. Send **Message 0** first. Then send the sheets one at a time, each as its own message, and attach the style reference image to every message:

- `Assets/_Project/Art/Characters/sheet_bodies.png` (the original painted bodies)
- for the hair sheets, also `Assets/_Project/Art/Characters/sheet_hair.png`

Save each result into `Assets/_Project/Art/Characters/` with the file name given. If a sheet has one or two bad cells, keep it: I'll skip those cells. If the whole sheet is wrong (wrong angle, arms on the torsos, text, a background), ask again with: *"Redo it. Follow every rule in the message exactly, especially: [the rule it broke]."*

Each prompt below repeats every rule it needs, so ChatGPT never has to remember an earlier message.

---

## Message 0: explain the job (send once, first)

> I'm making a 2D video game seen from directly above (top-down), set in the back streets of Naples. The people in the game are built from separate painted parts that the game moves to animate them: a torso, two arms that swing from the shoulder, walking legs, a head and hair. I need sprite sheets of these parts. They must line up exactly when stacked, so the size and position of every part in its square matter as much as the painting.
>
> Every sheet I ask for follows these rules:
> 1. One square image, ideally 2048×2048 pixels (1024×1024 is acceptable), divided into a **4×4 grid of 16 equal square cells**. Do NOT draw the grid lines. One item per cell, centred, with empty space around it; nothing touches or crosses a cell's border.
> 2. **Transparent background.** No floor, no cast shadows on the ground, no text, no numbers, no logos, no watermarks, no labels.
> 3. **Camera:** looking straight down from above, as if the camera were on the ceiling, with a very slight tilt so you can just see the front of the chest and the trousers below the shoulders. Orthographic, with no perspective distortion. The same camera in every cell and every sheet.
> 4. **Orientation:** the front of everything points DOWN the image (towards the bottom edge): the chest, the nose, the toes, the hairline. The back points UP.
> 5. **Style:** semi-realistic digital painting, like high-quality hand-painted game art. Soft natural light coming from the top-left. Gentle shading, realistic fabric folds, realistic skin, muted real-world colours. **No black outlines**, no cartoon style, no cel shading.
> 6. **Same scale in every cell and every sheet.** The attached image shows the style and the scale I want.
>
> I'll now send the sheets one by one. Please just reply "Ready".

---

## Sheet 1: men's torsos without arms (`sheet_torsos_men.png`)

> Make a sprite sheet for a top-down video game. Attached: a style reference.
>
> Rules:
> - One square image, 2048×2048 if possible, as a **4×4 grid of 16 equal square cells**. No grid lines drawn. One item per cell, centred, with empty space around it; nothing touches the cell borders.
> - **Transparent background.** No shadows on the ground, no text, no logos.
> - Camera straight down from above with a very slight tilt, so the top of the shoulders, the front of the chest and belly, and the trousers just below are visible. Orthographic. Front pointing DOWN the image.
> - Semi-realistic painted style, soft light from the top-left, realistic fabric folds, muted real colours, **no outlines**.
>
> Each cell shows **a man's torso with NO ARMS and NO HEAD**, like a tailor's mannequin bust:
> - **Shoulders:** across the cell, 75% of the cell's width, the top of the shoulders at 30% of the cell's height from the top. Each shoulder ends in a **smoothly rounded cap** (the top of the sleeve), with **nothing hanging down at the sides: no arms, no hands, no elbows**.
> - **Neck opening:** a round opening where the head will sit, 30% of the cell's width across, its centre at 40% of the cell's height from the top, filled with plain flat mid-grey.
> - **Below:** the chest and belly below the shoulders, then the top of the trousers and belt from 75% to 92% of the cell's height. No legs below that, no shoes.
> - The same build, size and position in all 16 cells. Only the clothes change.
>
> The 16 outfits, left to right, top to bottom, with no logos or text anywhere:
> 1. plain white T-shirt, dark jeans
> 2. plain black T-shirt, blue jeans
> 3. sky-blue football shirt with a white collar, white shorts
> 4. black tracksuit jacket with white stripes on the shoulders, zipped
> 5. navy tracksuit jacket, half open over a white T-shirt
> 6. worn brown leather jacket open over a black T-shirt
> 7. white sleeveless vest (singlet), the bare shoulders showing, jeans
> 8. white shirt with an open collar, dark trousers
> 9. grey hoodie with the hood down behind the neck
> 10. black suit jacket, white shirt, dark tie
> 11. black puffer jacket
> 12. denim jacket over a grey T-shirt
> 13. dark green polo shirt
> 14. olive bomber jacket
> 15. brown knitted cardigan over a checked shirt (an older man)
> 16. black padded sleeveless gilet over a red hoodie

---

## Sheet 2: men's arms, matching sheet 1 (`sheet_arms_men.png`)

> Make a sprite sheet for a top-down video game. Attached: a style reference.
>
> Rules:
> - One square image, 2048×2048 if possible, as a **4×4 grid of 16 equal square cells**. No grid lines drawn. Nothing touches the cell borders.
> - **Transparent background.** No shadows on the ground, no text, no logos.
> - Camera straight down from above with a very slight tilt, orthographic. Semi-realistic painted style, soft light from the top-left, **no outlines**.
>
> Each cell shows **ONE single arm on its own**: no body, no shoulder, no second arm. It is the arm that appears on the **left side of the image** when a person faces down the image.
> - **Top:** the top of the arm (a smoothly rounded end where it meets the shoulder) at the **top centre of the cell, 12% from the top**.
> - **Pose:** the arm hangs **straight down the cell**, relaxed: upper arm, elbow, forearm, then a loosely closed hand.
> - **Bottom:** the hand ends at 82% of the cell's height.
> - **Width:** about 18% of the cell's width. The thumb is on the right side of the hand (towards the body). The back of the hand faces up towards the camera.
> - The same length, width and position in all 16 cells. Only the sleeve and skin change.
>
> Each cell's sleeve **matches the outfit in the same position** of a sheet of 16 men's outfits, left to right, top to bottom:
> 1. short white T-shirt sleeve on the upper arm, bare forearm and hand
> 2. short black T-shirt sleeve, bare forearm and hand
> 3. short sky-blue football-shirt sleeve with a white edge, bare forearm and hand
> 4. long black tracksuit sleeve with a white stripe down it to the wrist, then the hand
> 5. long navy tracksuit sleeve to the wrist, then the hand
> 6. long worn brown leather sleeve to the wrist, then the hand
> 7. completely bare arm, muscular, no sleeve
> 8. long white shirt sleeve, cuff at the wrist, then the hand
> 9. long grey hoodie sleeve with a ribbed cuff, then the hand
> 10. long black suit sleeve with a little white shirt cuff showing, then the hand
> 11. long black puffer-jacket sleeve, quilted, then the hand
> 12. long denim sleeve, then the hand
> 13. short dark green polo sleeve, bare forearm and hand
> 14. long olive bomber sleeve with a ribbed cuff, then the hand
> 15. long brown knitted cardigan sleeve, then an older man's hand
> 16. long red hoodie sleeve, then the hand
>
> Vary the skin tone a little between cells (light olive to tanned Mediterranean).

---

## Sheet 3: women's torsos without arms (`sheet_torsos_women.png`)

> Make a sprite sheet for a top-down video game. Attached: a style reference.
>
> Rules:
> - One square image, 2048×2048 if possible, as a **4×4 grid of 16 equal square cells**. No grid lines drawn. Nothing touches the cell borders.
> - **Transparent background.** No shadows on the ground, no text, no logos.
> - Camera straight down from above with a very slight tilt (the top of the shoulders, the front of the chest and the skirt or trousers just below are visible), orthographic, front pointing DOWN the image.
> - Semi-realistic painted style, soft light from the top-left, realistic fabric, muted real colours, **no outlines**.
>
> Each cell shows **a woman's torso with NO ARMS and NO HEAD**, like a dressmaker's mannequin bust:
> - **Shoulders:** 66% of the cell's width (narrower than a man's), the top of the shoulders at 30% of the cell's height from the top. Each shoulder ends in a smoothly rounded cap, with **nothing hanging at the sides: no arms, no hands**.
> - **Neck opening:** round, 28% of the cell's width across, its centre at 40% of the cell's height, filled with plain flat mid-grey.
> - **Below:** the chest, then the waist and the top of the skirt or trousers from 75% to 92% of the cell's height.
> - The same build, size and position in all 16 cells.
>
> The 16 outfits, left to right, top to bottom, no logos or text:
> 1. black dress with a black cardigan (an old lady)
> 2. flowered housecoat with an apron
> 3. pink tracksuit jacket
> 4. black leather jacket
> 5. floral blouse
> 6. white shirt
> 7. white puffer jacket
> 8. tight black top with a gold necklace
> 9. light-blue nurse's top
> 10. shop apron over a beige jumper
> 11. dark coat with a fur collar
> 12. denim jacket
> 13. navy school-uniform jumper
> 14. black mourning dress with a black shawl
> 15. grey sports top
> 16. smart dark-red blazer

---

## Sheet 4: women's arms, matching sheet 3 (`sheet_arms_women.png`)

> Make a sprite sheet for a top-down video game. Attached: a style reference.
>
> Rules:
> - One square image, 2048×2048 if possible, as a **4×4 grid of 16 equal square cells**. No grid lines drawn. Nothing touches the cell borders.
> - **Transparent background**, no shadows on the ground, no text.
> - Camera straight down from above with a very slight tilt, orthographic. Semi-realistic painted style, soft light from the top-left, **no outlines**.
>
> Each cell shows **ONE single woman's arm on its own**: no body, no shoulder, no second arm. It is the arm that appears on the **left side of the image** when the woman faces down the image.
> - **Top:** a smoothly rounded top at the **top centre of the cell, 12% from the top**.
> - **Pose:** it hangs **straight down**, relaxed, with a relaxed open hand.
> - **Bottom:** the hand ends at 80% of the cell's height.
> - **Width:** about 14% of the cell's width. The thumb is on the right side (towards the body). The back of the hand faces up.
> - The same length, width and position in all 16 cells.
>
> Each sleeve matches, in the same position, these outfits, left to right, top to bottom:
> 1. long black cardigan sleeve, an old lady's hand
> 2. short flowered housecoat sleeve, bare forearm
> 3. long pink tracksuit sleeve
> 4. long black leather sleeve
> 5. short floral blouse sleeve, bare forearm
> 6. long white shirt sleeve with a cuff
> 7. long white puffer sleeve
> 8. bare arm with a thin gold bracelet
> 9. short light-blue nurse's sleeve, bare forearm
> 10. long beige jumper sleeve pushed up a little
> 11. long dark coat sleeve
> 12. long denim sleeve
> 13. long navy jumper sleeve
> 14. long black sleeve, an old lady's hand
> 15. long grey sports sleeve
> 16. long dark-red blazer sleeve

---

## Sheet 5: police torsos and arms (`sheet_torsos_police.png`, then `sheet_arms_police.png`)

**First message:**

> Make a sprite sheet for a top-down video game. Attached: a style reference.
>
> Rules:
> - One square image, 2048×2048 if possible, as a **4×4 grid of 16 equal square cells**. No grid lines drawn. Nothing touches the cell borders.
> - **Transparent background**, no shadows on the ground, **no text, no badges with writing, no real insignia**.
> - Camera straight down from above with a very slight tilt, orthographic, front pointing DOWN the image.
> - Semi-realistic painted style, soft light from the top-left, **no outlines**.
>
> Each cell shows **a man's uniformed torso with NO ARMS and NO HEAD**, like a mannequin bust:
> - **Shoulders:** 75% of the cell's width, the top at 30% from the top of the cell. Smoothly rounded shoulder caps, **nothing hanging at the sides**.
> - **Neck opening:** round, 30% of the cell's width across, centred at 40% from the top, filled with flat mid-grey.
> - **Below:** the chest, then the belt and the top of the trousers from 75% to 92% of the cell's height.
>
> The 16 uniforms, left to right, top to bottom:
> - **Cells 1–4:** a dark blue police jacket over a light blue shirt, with plain epaulettes and a black belt (four slight variations).
> - **Cells 5–8:** a very dark navy jacket with a white belt and a white cross-strap.
> - **Cells 9–12:** a grey-green jacket with yellow piping.
> - **Cells 13–14:** plain-clothes detectives: a leather jacket with a shoulder holster visible.
> - **Cells 15–16:** riot police in padded dark body armour.

**Second message:** send exactly the Sheet 2 prompt, with its list replaced by: 1–4 dark blue uniform sleeve; 5–8 very dark navy uniform sleeve; 9–12 grey-green sleeve with yellow piping; 13–14 leather jacket sleeve; 15–16 padded dark riot sleeve with a black glove. Save it as `sheet_arms_police.png`.

---

## Sheet 6: walking legs (`sheet_legs_walk.png`)

> Make a sprite sheet for a top-down video game. Attached: a style reference.
>
> Rules:
> - One square image, 2048×2048 if possible, as a **4×4 grid of 16 equal square cells**. No grid lines drawn. Nothing touches the cell borders.
> - **Transparent background**, no shadows on the ground, no text, no logos.
> - Camera straight down from above with a very slight tilt, orthographic. Semi-realistic painted style, soft light from the top-left, **no outlines**.
>
> Each cell shows **ONLY a person's hips, legs and shoes, seen from above while walking**: no torso, no arms, no head. The torso is drawn on top later, so the top of the hips is a straight, clean edge.
> - **Position:** the hips are centred across the cell, 46% of the cell's width wide, their top edge at 55% of the cell's height from the top. The legs and feet go DOWN the image (the person walks towards the bottom of the image).
> - **Front:** the toes point DOWN.
> - The same size and position in every cell.
>
> **Each ROW** is one pair of trousers and shoes through a 4-step walking cycle, left to right:
> 1. left foot forward (lower in the image), right foot back
> 2. feet passing each other, side by side
> 3. right foot forward, left foot back
> 4. feet passing each other, side by side
>
> The rows:
> - **Row 1:** blue jeans and white trainers
> - **Row 2:** black tracksuit trousers with a white stripe, black trainers
> - **Row 3:** dark grey suit trousers, black leather shoes
> - **Row 4:** a woman's dark knee-length skirt, bare lower legs, flat black shoes

---

## Sheet 7: heads (`sheet_heads.png`)

> Make a sprite sheet for a top-down video game. Attached: a style reference.
>
> Rules:
> - One square image, 2048×2048 if possible, as a **4×4 grid of 16 equal square cells**. No grid lines drawn. Nothing touches the cell borders.
> - **Transparent background**, no shadows, no text.
> - Camera **straight down from directly above**, orthographic. Semi-realistic painted style, soft light from the top-left, **no outlines**.
>
> Each cell shows **only the top of a completely bald human head, seen from directly above**:
> - **Shape:** a slightly oval skull, **38% of the cell's width wide** and 42% tall, **centred exactly in the middle of the cell**.
> - **Ears:** visible on the left and right sides of the head.
> - **Nose:** only its tip peeks out at the bottom edge of the head (the face points DOWN). No eyes, mouth or face visible from this angle.
> - **No hair at all, no hat, no neck, no shoulders.**
> - The same size and position in all 16 cells.
>
> **Variety:**
> - **Rows 1–2:** men's heads.
> - **Rows 3–4:** women's heads (a little smaller and rounder).
> - **Across each row:** skin from light olive to tanned Mediterranean to dark. Some young, some old (a few wrinkles on the neck side, sun spots on an old man's scalp).

---

## Sheet 8: men's hair, fitted to the heads (`sheet_hair_men.png`)

> Make a sprite sheet for a top-down video game. Attached: a style reference.
>
> Rules:
> - One square image, 2048×2048 if possible, as a **4×4 grid of 16 equal square cells**. No grid lines drawn. Nothing touches the cell borders.
> - **Transparent background**, no shadows, no text.
> - Camera **straight down from directly above**, orthographic. Semi-realistic painted style with fine realistic hair strands, soft light from the top-left, **no outlines**.
>
> Each cell shows **ONLY the hair, as it would sit on an invisible head**. The rules for this are very important:
> - **Fit:** the invisible head is an oval **38% of the cell's width wide and 42% tall, centred exactly in the cell**. The hair must fit that oval exactly, as if painted on it: it covers the top and back of the head and stops at the hairline.
> - **Front and back:** the front of the hair (the hairline above the forehead) is at the **BOTTOM** of the oval; the back of the head and the crown are at the TOP.
> - **Nothing else:** no skin, no scalp showing except through very short hair, no ears, no face, no neck. Everything outside the hair is transparent.
> - Hair that is longer than the head (a ponytail, long hair) hangs **upwards in the image** (behind the head) or down the sides, never over the face at the bottom.
>
> The 16 styles, left to right, top to bottom, all men, dark brown or black unless noted:
> 1. very short buzz cut
> 2. short neat crop
> 3. slicked back with gel, shiny
> 4. side parting, combed
> 5. short tight curls
> 6. high skin fade at the sides, longer on top
> 7. receding hairline
> 8. bald on top with a horseshoe ring of hair round the sides and back
> 9. shaved head, just a dark stubble shadow
> 10. hair tied in a small bun at the back
> 11. shoulder-length hair tied low at the back
> 12. spiky gelled hair
> 13. short grey hair (old man)
> 14. thin grey hair combed over
> 15. mullet, longer at the back
> 16. short mohawk

---

## Sheet 9: women's hair, fitted to the heads (`sheet_hair_women_fit.png`)

> Make a sprite sheet for a top-down video game. Attached: a style reference.
>
> Rules:
> - One square image, 2048×2048 if possible, as a **4×4 grid of 16 equal square cells**. No grid lines drawn. Nothing touches the cell borders.
> - **Transparent background**, no shadows, no text.
> - Camera **straight down from directly above**, orthographic. Semi-realistic painted style with fine realistic hair strands, soft light from the top-left, **no outlines**.
>
> Each cell shows **ONLY the hair, as it would sit on an invisible head**:
> - **Fit:** the invisible head is an oval **34% of the cell's width wide and 38% tall, centred exactly in the cell**. The hair fits that oval like a wig.
> - **Front and back:** the hairline is at the **BOTTOM** of the oval (the face points down); the crown and back are at the TOP.
> - **Long hair:** ponytails, buns and long hair go **upwards** in the image (behind the head) or fall down the sides of the oval, **never over the bottom (face) edge**.
> - **Nothing else:** no skin, no ears, no face, no neck. Everything else is transparent.
>
> The 16 styles, left to right, top to bottom:
> 1. long straight dark hair
> 2. long curly black hair
> 3. high ponytail
> 4. low bun
> 5. tight grey bun (old lady)
> 6. short grey perm
> 7. bob cut
> 8. dyed blonde with dark roots
> 9. red-dyed curls
> 10. a braid down the back
> 11. wavy shoulder-length hair
> 12. a headscarf tied at the back (seen from above)
> 13. pixie cut
> 14. half-up with a clip
> 15. big curly hair
> 16. slicked-back ponytail

---

## Order to do them in

1. **Sheets 1 and 2** (men's torsos and arms). Send me those first: I'll put them in the game and check the walk before you do the rest.
2. **Sheet 6** (walking legs).
3. **Sheets 7 and 8** (heads and men's hair). These fix the hair problems.
4. **Sheets 3, 4, 5 and 9** (women and police).
