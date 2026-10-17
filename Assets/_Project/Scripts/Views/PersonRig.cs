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
        readonly SpriteRenderer shadow, body, head, hair, gun, handL, handR;
        Vector2 lastPos;
        float heading;              // degrees; 0 = facing north
        bool hasPos;
        float stride;               // walking cycle phase
        float moving;               // 0 standing .. 1 walking, smoothed
        bool aiming;

        static readonly Vector3 HandLRest = new Vector3(-0.3f, 0.14f, 0f);
        static readonly Vector3 HandRRest = new Vector3(0.3f, 0.14f, 0f);

        public Transform Root => root;

        public PersonRig(Transform parent, float scale, int sortingOrder)
        {
            root = parent;
            root.localScale = Vector3.one * scale;

            shadow = Part("Shadow", root, SpriteFactory.Disc, SpriteFactory.UnlitMaterial, sortingOrder - 1);
            shadow.color = new Color(0f, 0f, 0f, 0.35f);
            shadow.transform.localPosition = new Vector3(0.08f, -0.1f, 0f);
            shadow.transform.localScale = new Vector3(0.95f, 0.7f, 1f);

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

        public void SetColors(Color shirt, Color skin, Color hairColor)
        {
            body.color = shirt;
            head.color = skin;
            handL.color = skin;
            handR.color = skin;
            hair.color = hairColor;
        }

        public void SetVisible(bool visible)
        {
            shadow.enabled = visible;
            body.enabled = visible;
            head.enabled = visible;
            hair.enabled = visible;
            handL.enabled = visible;
            handR.enabled = visible;
            if (!visible) gun.enabled = false;
        }

        public void SetArmed(bool armed) => gun.enabled = armed && body.enabled;

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
                if (delta.sqrMagnitude > 0.00002f) heading = Mathf.Atan2(-delta.x, delta.y) * Mathf.Rad2Deg;
                speed = dt > 0f ? delta.magnitude / dt : 0f;
            }
            lastPos = position;
            hasPos = true;

            // Arms swing while walking; both hands come forward when aiming.
            moving = Mathf.MoveTowards(moving, !lying && speed > 0.2f ? 1f : 0f, dt * 6f);
            stride += speed * dt * 9f;
            float swing = Mathf.Sin(stride) * 0.12f * moving;
            if (aiming && !lying)
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
            aiming = false;
            // A slight bob of the shoulders as the weight shifts.
            body.transform.localScale = Vector3.one * (1f + Mathf.Abs(Mathf.Sin(stride)) * 0.04f * moving);

            root.position = new Vector3(position.x, position.y, 0f);
            float current = pivot.localEulerAngles.z;
            float target = lying ? heading + 90f : heading;
            pivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.MoveTowardsAngle(current, target, TurnSpeed * dt));
            // Lying figures read flatter and slightly offset, standing ones bob very slightly when moving.
            pivot.localPosition = lying ? new Vector3(0f, -0.1f, 0f) : Vector3.zero;
            shadow.transform.localScale = lying ? new Vector3(1.2f, 0.55f, 1f) : new Vector3(0.95f, 0.7f, 1f);
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
