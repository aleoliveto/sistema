using UnityEngine;

namespace OSistema.Views
{
    /// <summary>
    /// A top-down person built from procedural parts: shadow, shoulders (shirt colour), head (skin),
    /// hair, and an optional pistol. Turns smoothly to face the way it moves, lies down when downed.
    /// Shared by crew, rivals, police and civilians.
    /// </summary>
    public sealed class PersonRig
    {
        const float TurnSpeed = 720f;

        readonly Transform root;
        readonly Transform pivot;   // rotates to face the heading
        readonly SpriteRenderer shadow, body, head, hair, gun, handL, handR, footL, footR;
        bool drawnLegs;             // street people are painted with their legs and shoes
        float idleLook;             // standing still: now and then a look around
        readonly float lookSeed = Random.value * 100f;
        Vector2 lastPos;
        float heading;              // degrees; 0 = facing north
        bool hasPos;
        float stride;               // walking cycle phase
        float moving;               // 0 standing .. 1 walking, smoothed
        bool aiming;

        /// <summary>Lying asleep (not hurt): breathing slowly.</summary>
        public bool Asleep;
        bool wasLying;
        float fall = 1f;            // 0 the moment someone goes down, 1 when they've hit the ground
        float flinch;               // a hit: jolt back, decaying

        /// <summary>Hit: a jolt back from the blow.</summary>
        public void Flinch() => flinch = 1f;

        /// <summary>In bed: lie along it, shoulders across the mattress, head to the pillow.</summary>
        public float? LieAngle;

        /// <summary>On a scooter: no walking stride, hands on the handlebar.</summary>
        public bool Seated;

        static readonly Vector3 HandLRest = new Vector3(-0.3f, 0.14f, 0f);
        static readonly Vector3 HandRRest = new Vector3(0.3f, 0.14f, 0f);

        public Transform Root => root;

        // Every rig alive, so a shot can find the arm that fired it (domain reload is off: prune the dead).
        static readonly System.Collections.Generic.List<PersonRig> all = new System.Collections.Generic.List<PersonRig>();

        /// <summary>A shot fired from here: whoever is standing on the spot kicks back.</summary>
        public static void RecoilAt(Vector2 from)
        {
            all.RemoveAll(r => r.root == null);
            PersonRig best = null;
            float bestD = 0.8f * 0.8f;
            foreach (var r in all)
            {
                float d = ((Vector2)r.root.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = r; }
            }
            best?.Recoil();
        }

        public PersonRig(Transform parent, float scale, int sortingOrder)
        {
            all.Add(this);
            root = parent;
            root.localScale = Vector3.one * scale;

            shadow = Part("Shadow", root, SpriteFactory.SoftShadow, SpriteFactory.UnlitMaterial, sortingOrder - 1);
            shadow.color = new Color(0.05f, 0.04f, 0.06f, 0.55f);
            shadow.transform.localPosition = new Vector3(0.12f, 0.08f, 0f);   // cast towards the north-east, like the buildings'
            shadow.transform.localScale = new Vector3(1.15f, 0.95f, 1f);

            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(root, false);
            handL = Part("Hand L", pivot, SpriteFactory.Head, SpriteFactory.LitMaterial, sortingOrder);
            handL.transform.localPosition = new Vector3(-0.3f, 0.14f, 0f);
            handL.transform.localScale = Vector3.one * 0.45f;
            handR = Part("Hand R", pivot, SpriteFactory.Head, SpriteFactory.LitMaterial, sortingOrder);
            handR.transform.localPosition = new Vector3(0.3f, 0.14f, 0f);
            handR.transform.localScale = Vector3.one * 0.45f;
            body = Part("Body", pivot, SpriteFactory.Shoulders, SpriteFactory.LitMaterial, sortingOrder + 1);
            gun = Part("Gun", pivot, SpriteFactory.CenteredPixel, SpriteFactory.LitMaterial, sortingOrder + 2);
            gun.color = new Color(0.08f, 0.08f, 0.09f);
            gun.transform.localPosition = new Vector3(0.3f, 0.32f, 0f);
            gun.transform.localScale = new Vector3(0.09f, 0.3f, 1f);
            head = Part("Head", pivot, SpriteFactory.Head, SpriteFactory.LitMaterial, sortingOrder + 3);
            head.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            hair = Part("Hair", pivot, SpriteFactory.Hair, SpriteFactory.LitMaterial, sortingOrder + 4);
            // Shoes, under the body: they only show as they step out in front or behind.
            footL = Part("Foot L", pivot, SpriteFactory.Head, SpriteFactory.LitMaterial, sortingOrder);
            footR = Part("Foot R", pivot, SpriteFactory.Head, SpriteFactory.LitMaterial, sortingOrder);
            footL.color = footR.color = new Color(0.13f, 0.11f, 0.1f);
            footL.transform.localScale = footR.transform.localScale = new Vector3(0.36f, 0.5f, 1f);
            footL.enabled = footR.enabled = false;
            hair.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            gun.enabled = false;
        }

        static SpriteRenderer Part(string name, Transform parent, Sprite sprite, Material material, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = material;
            sr.sortingOrder = order;
            return sr;
        }

        // ---------- Painted parts ----------

        SpriteRenderer hat, neck, armL, armR;
        bool limbs;                 // the painted body is cut into a torso and two arms that swing
        bool art;
        bool visibleNow = true;
        Color artTint = Color.white;

        /// <summary>Switches to the painted parts (clothes, head, hair, hat, chain). False if the art isn't there.</summary>
        public bool ApplyLook(OSistema.Pawns.Look look)
        {
            if (look == null || !CharacterArt.Available) return false;
            var b = CharacterArt.Sprite("body", look.Body);
            var h = CharacterArt.Sprite("head", look.Head);
            var hr = CharacterArt.Sprite("hair", look.Hair);
            if (b == null || h == null || hr == null) return false;
            art = true;
            drawnLegs = look.Body >= OSistema.Pawns.Look.Street;
            body.sprite = b;
            limbs = CharacterArt.Limbs(look.Body, out var torso, out var al, out var ar, out var sl, out var sr);
            if (limbs)
            {
                body.sprite = torso;
                // Under the torso: the top of each arm tucks in beneath the sleeve.
                if (armL == null) armL = Part("Arm L", body.transform, null, SpriteFactory.LitMaterial, body.sortingOrder - 1);
                if (armR == null) armR = Part("Arm R", body.transform, null, SpriteFactory.LitMaterial, body.sortingOrder - 1);
                armL.sprite = al;
                armR.sprite = ar;
                armL.transform.localPosition = sl;
                armR.transform.localPosition = sr;
            }
            else if (armL != null)
            {
                armL.sprite = armR.sprite = null;
            }
            head.sprite = h;
            hair.sprite = hr;
            // The painted parts face the viewer (south); the figure faces north before it's turned.
            var back = Quaternion.Euler(0f, 0f, 180f);
            body.transform.localPosition = Vector3.zero;
            head.transform.localPosition = Vector3.zero;
            hair.transform.localPosition = Vector3.zero;
            body.transform.localRotation = back;
            head.transform.localRotation = back;
            hair.transform.localRotation = back;
            if (neck == null) neck = Part("Chain", pivot, null, SpriteFactory.LitMaterial, head.sortingOrder - 1);
            if (hat == null) hat = Part("Hat", pivot, null, SpriteFactory.LitMaterial, hair.sortingOrder + 1);
            neck.sprite = CharacterArt.Sprite("neck", look.Neck);
            hat.sprite = CharacterArt.Sprite("hat", look.Hat);
            neck.transform.localRotation = back;
            hat.transform.localRotation = back;
            SetVisible(visibleNow);
            SetColors(Color.white, Color.white, Color.white);
            return true;
        }

        /// <summary>For painted parts: white is normal, grey is downed or dead, reddish is a hit.</summary>
        public void SetArtTint(Color c)
        {
            artTint = c;
            if (!art) return;
            body.color = head.color = hair.color = c;
            if (armL != null) armL.color = armR.color = c;
            if (hat != null) hat.color = c;
            if (neck != null) neck.color = c;
        }

        public bool UsesArt => art;

        public void SetColors(Color shirt, Color skin, Color hairColor)
        {
            if (art)
            {
                handL.color = handR.color = skin == Color.white ? new Color(0.86f, 0.68f, 0.52f) : skin;
                SetArtTint(artTint);
                return;
            }
            body.color = shirt;
            head.color = skin;
            handL.color = skin;
            handR.color = skin;
            hair.color = hairColor;
        }

        public void SetVisible(bool visible)
        {
            visibleNow = visible;
            shadow.enabled = visible;
            body.enabled = visible;
            head.enabled = visible;
            hair.enabled = visible;
            // The painted bodies have their own arms: the hands show to hold a gun or the handlebars,
            // and swing at the sides while walking.
            bool hands = visible && (!art || (aiming && !Seated));
            handL.enabled = hands;
            handR.enabled = hands;
            if (hat != null) hat.enabled = visible && hat.sprite != null;
            if (armL != null) armL.enabled = armR.enabled = visible && limbs;
            if (neck != null) neck.enabled = visible && neck.sprite != null;
            if (!visible)
            {
                gun.enabled = false;
                footL.enabled = footR.enabled = false;
            }
        }

        public void SetArmed(bool armed) => gun.enabled = armed && body.enabled;

        /// <summary>Turns to look a way (degrees from a direction) without raising a gun: for people standing about.</summary>
        public void LookTowards(Vector2 dir)
        {
            if (dir.sqrMagnitude > 0.0001f) heading = Mathf.Atan2(-dir.x, dir.y) * Mathf.Rad2Deg;
        }

        /// <summary>Faces a point (e.g. the target being shot at) instead of the walking direction.</summary>
        public void FaceTowards(Vector2 point)
        {
            var d = point - (Vector2)root.position;
            if (d.sqrMagnitude > 0.0001f) heading = Mathf.Atan2(-d.x, d.y) * Mathf.Rad2Deg;
            aiming = true;
        }

        public void Update(Vector2 position, bool lying, float dt)
        {
            float speed = 0f;
            if (hasPos)
            {
                var delta = position - lastPos;
                // Someone aiming keeps facing the target while they move (strafing), instead of
                // flipping between the gun and the way they're walking every frame.
                if (delta.sqrMagnitude > 0.00002f && !aiming) heading = Mathf.Atan2(-delta.x, delta.y) * Mathf.Rad2Deg;
                speed = dt > 0f ? delta.magnitude / dt : 0f;
            }
            lastPos = position;
            hasPos = true;

            // Arms swing while walking; both hands come forward when aiming.
            moving = Mathf.MoveTowards(moving, !lying && !Seated && speed > 0.2f ? 1f : 0f, dt * 6f);
            // About one full step cycle (left and right) every 1.5 cells.
            stride += speed * dt * 4.2f;
            float swing = Mathf.Sin(stride) * 0.12f * moving;
            if (Seated && !lying)
            {
                handL.transform.localPosition = new Vector3(-0.3f, 0.42f, 0f);
                handR.transform.localPosition = new Vector3(0.3f, 0.42f, 0f);
                gun.transform.localPosition = new Vector3(0.3f, 0.5f, 0f);
            }
            else if (aiming && !lying)
            {
                handL.transform.localPosition = new Vector3(-0.08f, 0.36f, 0f);
                handR.transform.localPosition = new Vector3(0.1f, 0.4f, 0f);
                gun.transform.localPosition = new Vector3(0.02f, 0.55f, 0f);
            }
            else
            {
                handL.transform.localPosition = HandLRest + new Vector3(0f, swing, 0f);
                handR.transform.localPosition = HandRRest - new Vector3(0f, swing, 0f);
                gun.transform.localPosition = new Vector3(0.3f, 0.32f - swing, 0f);
            }
            float runAmt = Mathf.Clamp01((speed - 3.5f) / 3f);
            if (art)
            {
                // The painted bodies have their own arms: hands only appear holding a gun.
                bool hands = visibleNow && aiming && !lying && !Seated && !limbs;
                handL.enabled = hands;
                handR.enabled = hands;
                footL.enabled = footR.enabled = false;
            }

            if (limbs) PoseArms(lying, speed, Mathf.Sin(stride));
            aiming = false;
            if (art && !lying)
            {
                // Walking from above: the shoulders swing against the stride, the head nods, the body
                // rises and falls with each step. Running exaggerates all of it; standing, he breathes.
                float run = Mathf.Clamp01((speed - 3.5f) / 3f);
                float amp = moving * (1f + run * 0.8f);
                float sway = Mathf.Sin(stride) * 7f * amp;
                float breathe = Seated ? 0f : Mathf.Sin(Time.time * 2.2f + stride) * 0.012f * (1f - moving);
                float step = Mathf.Abs(Mathf.Sin(stride)) * 0.05f * amp;
                body.transform.localRotation = Quaternion.Euler(0f, 0f, 180f + sway);
                if (neck != null) neck.transform.localRotation = body.transform.localRotation;
                // Standing about: every so often a glance to one side.
                float look = 0f;
                if (moving < 0.05f && !Seated)
                {
                    idleLook += dt;
                    float n = Mathf.PerlinNoise(idleLook * 0.35f, lookSeed);
                    look = n > 0.62f ? (n - 0.62f) * 110f : n < 0.3f ? (n - 0.3f) * 90f : 0f;
                }
                var headRot = Quaternion.Euler(0f, 0f, 180f - sway * 0.35f + look);
                head.transform.localRotation = headRot;
                hair.transform.localRotation = headRot;
                if (hat != null) hat.transform.localRotation = headRot;
                // Leaning into the run.
                var lean = new Vector3(0f, 0.04f * run * moving, 0f);
                head.transform.localPosition = lean;
                hair.transform.localPosition = lean;
                if (hat != null) hat.transform.localPosition = lean;
                body.transform.localScale = new Vector3(1f + breathe, 1f + breathe + step, 1f);
                // The weight shifts onto each foot in turn.
                body.transform.localPosition = new Vector3(Mathf.Sin(stride) * 0.025f * amp, 0.03f * run * moving, 0f);
                if (neck != null) neck.transform.localPosition = body.transform.localPosition;
            }
            else
            {
                // A slight bob of the shoulders as the weight shifts.
                body.transform.localScale = Vector3.one * (1f + Mathf.Abs(Mathf.Sin(stride)) * 0.04f * moving);
            }

            root.position = new Vector3(position.x, position.y, 0f);
            // Going down (not to bed): a fall that takes a moment, turning over, a bounce as he lands.
            if (lying && !wasLying && !Asleep) fall = 0f;
            wasLying = lying;
            fall = Mathf.MoveTowards(fall, 1f, dt * 2.6f);
            flinch = Mathf.MoveTowards(flinch, 0f, dt * 7f);
            float current = pivot.localEulerAngles.z;
            float target = lying ? (LieAngle ?? heading + 90f) : heading;
            float turn = fall < 1f ? 260f : TurnSpeed;
            pivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.MoveTowardsAngle(current, target, turn * dt));
            // Lying figures read flatter and slightly offset, standing ones bob very slightly when moving.
            var back = -(Vector2)(pivot.localRotation * Vector3.up);
            pivot.localPosition = (lying ? new Vector3(0f, -0.1f * fall, 0f) : Vector3.zero) + (Vector3)(back * flinch * 0.12f);
            float landing = fall < 1f ? 1f + Mathf.Sin(fall * Mathf.PI) * 0.12f : 1f;
            // Asleep: the chest rises and falls, slowly.
            float sleepBreath = lying && Asleep ? Mathf.Sin(Time.time * 1.4f) * 0.025f : 0f;
            pivot.localScale = new Vector3(landing + sleepBreath * 0.6f, landing + sleepBreath, 1f);
            shadow.transform.localScale = lying ? new Vector3(1.2f, 0.55f, 1f) : new Vector3(0.95f, 0.7f, 1f);
            // A contact shadow always, thrown longer by the sun in the morning and evening.
            shadow.transform.localPosition = (Vector3)(Sun.Offset * (lying ? 0.12f : 0.32f));
            shadow.color = new Color(0.05f, 0.04f, 0.06f, 0.3f + 0.28f * Sun.Strength);
        }

        float recoil;

        /// <summary>A shot fired: the gun arm kicks back for a moment.</summary>
        public void Recoil() => recoil = 1f;

        /// <summary>
        /// The painted arms, seen from above: walking, each arm reaches forward as the other falls back
        /// (stretched or shortened from the shoulder); running, more. Aiming, both come forward and in
        /// to hold the gun, and kick back with each shot. On the scooter they hold the handlebars.
        /// </summary>
        void PoseArms(bool lying, float speed, float phase)
        {
            float dt = Time.deltaTime;
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 6f);
            float run = Mathf.Clamp01((speed - 3.5f) / 3f);
            Vector3 l = Vector3.one, r = Vector3.one;
            float rotL = 0f, rotR = 0f;
            if (lying) { }
            else if (Seated)
            {
                l.y = r.y = 1.3f;
                rotL = 18f; rotR = -18f;
            }
            else if (aiming)
            {
                // Arms reach forward and in towards the gun; the shooting arm a little further.
                l.y = 1.35f; r.y = 1.55f - recoil * 0.35f;
                rotL = 30f; rotR = -16f + recoil * 6f;
            }
            else
            {
                // Mostly a swing from the shoulder, with a little stretch as the hand comes forward.
                float amp = (1f + 0.6f * run) * moving;
                l.y = 1f + phase * 0.2f * amp;
                r.y = 1f - phase * 0.2f * amp;
                rotL = phase * 9f * amp;
                rotR = phase * 9f * amp;
            }
            // Arms point to the figure's front; the sprites face south and the body is turned round, so +y scale reaches forward.
            armL.transform.localScale = l;
            armR.transform.localScale = r;
            armL.transform.localRotation = Quaternion.Euler(0f, 0f, rotL);
            armR.transform.localRotation = Quaternion.Euler(0f, 0f, rotR);
        }

        // ---------- Appearance helpers ----------

        static readonly Color[] Skins =
        {
            new Color(0.93f, 0.78f, 0.64f), new Color(0.86f, 0.68f, 0.52f), new Color(0.76f, 0.58f, 0.43f),
            new Color(0.62f, 0.45f, 0.32f), new Color(0.96f, 0.84f, 0.72f),
        };

        static readonly Color[] Hairs =
        {
            new Color(0.08f, 0.06f, 0.05f), new Color(0.2f, 0.13f, 0.08f), new Color(0.32f, 0.22f, 0.14f),
            new Color(0.55f, 0.55f, 0.55f), new Color(0.12f, 0.1f, 0.1f),
        };

        public static Color SkinFor(int seed) => Skins[Mathf.Abs(seed * 7919) % Skins.Length];
        public static Color HairFor(int seed) => Hairs[Mathf.Abs(seed * 104729) % Hairs.Length];
    }
}
