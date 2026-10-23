using OSistema.Input;
using OSistema.Pawns;
using UnityEngine;

namespace OSistema.Views
{
    public sealed class PawnView : MonoBehaviour
    {
        static readonly Color SelectedColor = new Color(1f, 1f, 1f, 0.9f);
        static readonly Color DraftedSelectedColor = new Color(1f, 0.35f, 0.3f, 0.95f);
        static readonly Color DeadShirt = new Color(0.3f, 0.3f, 0.3f);

        Pawn pawn;
        PersonRig rig;
        SpriteRenderer ring;
        Color shirt, skin, hair;
        bool pooled;
        SpriteRenderer scooter;
        Exhaust exhaust;
        Vector2 lastPos;
        float lastSpeed;

        static readonly Color[] FamilyScooters =
        {
            new Color(0.75f, 0.1f, 0.12f), new Color(0.92f, 0.92f, 0.9f), new Color(0.12f, 0.12f, 0.13f), new Color(0.45f, 0.65f, 0.85f),
        };

        int wornNeck = -2, wornHat = -2;
        UnityEngine.Rendering.Universal.Light2D headlight;

        public Pawn Pawn => pawn;

        public void Init(Pawn p)
        {
            pawn = p;
            rig = new PersonRig(transform, 0.95f, 12);
            shirt = p.ShirtColor;
            skin = PersonRig.SkinFor(p.Id);
            hair = PersonRig.HairFor(p.Id + 3);
            rig.SetColors(shirt, skin, hair);
            rig.ApplyLook(p.Faction == Faction.Rival ? Look.Rival(p.Id) : p.Look);

            var scooterGo = new GameObject("Scooter");
            scooterGo.transform.SetParent(transform, false);
            scooter = scooterGo.AddComponent<SpriteRenderer>();
            scooter.sprite = SpriteFactory.ScooterBody;
            scooter.sharedMaterial = SpriteFactory.LitMaterial;
            scooter.sortingOrder = 10;
            scooter.color = FamilyScooters[Mathf.Abs(p.Id) % FamilyScooters.Length];
            scooter.enabled = false;
            exhaust = new Exhaust(transform.parent != null ? transform.parent : transform);
            headlight = WorldLighting.Headlight(transform.parent != null ? transform.parent : transform);

            var ringGo = new GameObject("Selection");
            ringGo.transform.SetParent(transform, false);
            ringGo.transform.localScale = Vector3.one * 1.25f;
            ring = ringGo.AddComponent<SpriteRenderer>();
            ring.sprite = SpriteFactory.Ring;
            ring.sharedMaterial = SpriteFactory.UnlitMaterial;
            ring.sortingOrder = 11;
            ring.enabled = false;

            LateUpdate();
        }

        /// <summary>Which way a bed points: its head (the pillow) is "up" when it was placed unturned.</summary>
        static float BedAngle(Vector2Int bed)
        {
            var map = OSistema.Core.GameRunner.CurrentMap;
            return map != null && map.InBounds(bed) ? map.GetRotation(bed.x, bed.y) * 90f + 180f : 180f;
        }

        void OnDestroy()
        {
            exhaust?.Clear();
            if (headlight != null) Destroy(headlight.gameObject);
        }

        void LateUpdate()
        {
            // Bought a chain or a hat: wear it.
            if (pawn != null && pawn.Look != null && pawn.Faction != Faction.Rival && (pawn.Look.Neck != wornNeck || pawn.Look.Hat != wornHat))
            {
                wornNeck = pawn.Look.Neck;
                wornHat = pawn.Look.Hat;
                rig.ApplyLook(pawn.Look);
            }
            if (pawn == null || rig == null) return; // state lost to a script reload during Play
            if (pawn.Departed)
            {
                Destroy(gameObject);
                return;
            }

            // In prison, or a rival out of our crew's sight: not drawn.
            bool visible = !pawn.InJail && !pawn.InHospital && (pawn.Faction == Faction.Crew || FogOfWar.CanSee(pawn.Position));
            rig.SetVisible(visible);
            if (!visible)
            {
                ring.enabled = false;
                return;
            }

            bool lying = pawn.Dead || pawn.Health.Downed || pawn.Sleep != SleepState.Awake;
            rig.Asleep = pawn.Sleep != SleepState.Awake && !pawn.Dead && !pawn.Health.Downed;
            if (Time.unscaledTime - pawn.LastHitTime < 0.05f) rig.Flinch();
            rig.LieAngle = pawn.Sleep == SleepState.InBed && pawn.Bed.HasValue && !pawn.Dead && !pawn.Health.Downed ? BedAngle(pawn.Bed.Value) : (float?)null;
            rig.Update(pawn.Position, lying, Time.deltaTime);
            if (pawn.AimTarget != null && !lying) rig.FaceTowards(pawn.AimTarget.Position);
            else if (pawn.AimPoint.HasValue && !lying) rig.FaceTowards(pawn.AimPoint.Value);
            rig.SetArmed(pawn.Armed && !pawn.Dead);
            // On a scooter: sitting, hands on the bars, the scooter turned the way it's going, and smoke.
            bool onScooter = pawn.Riding && !pawn.Pillion && !lying;
            rig.Seated = (pawn.Riding || pawn.Pillion) && !lying;
            scooter.enabled = onScooter;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            float speed = (pawn.Position - lastPos).magnitude / dt;
            lastPos = pawn.Position;
            if (onScooter)
            {
                float heading = pawn.Controlled ? pawn.Heading : transform.Find("Pivot").localEulerAngles.z;
                PropArt.DressScooter(scooter, heading, pawn.StolenScooter ? Mathf.Max(0, pawn.StolenLook) / 4 : pawn.RideModel, pawn.StolenScooter ? Mathf.Max(0, pawn.StolenLook) % 4 : pawn.RideColour >= 0 ? pawn.RideColour : Mathf.Abs(pawn.Id),
                    pawn.StolenScooter ? new Color(0.92f, 0.92f, 0.9f) : FamilyScooters[Mathf.Abs(pawn.Id) % FamilyScooters.Length]);
                var fwd = new Vector2(-Mathf.Sin(heading * Mathf.Deg2Rad), Mathf.Cos(heading * Mathf.Deg2Rad));
                WorldLighting.AimHeadlight(headlight, true, pawn.Position, heading);
                if (pawn.Controlled && !pawn.AimPoint.HasValue) rig.FaceTowards(pawn.Position + fwd);
                exhaust.Update(pawn.Position - fwd * 0.75f, Mathf.Clamp01(speed / 10f), speed > lastSpeed + 0.5f, true, Time.deltaTime);
            }
            else
            {
                exhaust.Update(pawn.Position, 0f, false, false, Time.deltaTime);
                WorldLighting.AimHeadlight(headlight, false, pawn.Position, 0f);
            }
            lastSpeed = Mathf.Lerp(lastSpeed, speed, 0.2f);
            if (pawn.Pillion && !lying) rig.FaceTowards(pawn.Position + pawn.Forward);

            var c = pawn.Dead ? DeadShirt : pawn.Health.Downed ? shirt * 0.65f : shirt;
            c.a = 1f;
            bool flash = Time.unscaledTime - pawn.LastHitTime < 0.12f;
            rig.SetColors(flash ? Color.white : c, flash ? Color.white : pawn.Dead ? skin * 0.6f : skin, hair);
            rig.SetArtTint(flash ? new Color(1f, 0.55f, 0.5f) : pawn.Dead ? new Color(0.5f, 0.5f, 0.52f) : pawn.Health.Downed ? new Color(0.75f, 0.72f, 0.72f) : Color.white);

            // Someone who goes down bleeds onto the street.
            if ((pawn.Dead || pawn.Health.Downed) && pawn.Health.BloodLoss > 0.05f && !pooled && CombatView.Instance != null)
            {
                pooled = true;
                CombatView.Instance.Pool(pawn.Position);
            }
            if (!pawn.Dead && !pawn.Health.Downed) pooled = false;

            var pc = PlayerController.Instance;
            bool selected = pc != null && pc.Selected.Contains(pawn);
            ring.enabled = selected;
            if (selected) ring.color = pawn.Drafted ? DraftedSelectedColor : SelectedColor;
        }
    }
}
