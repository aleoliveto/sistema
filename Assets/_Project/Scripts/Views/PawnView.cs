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

        public Pawn Pawn => pawn;

        public void Init(Pawn p)
        {
            pawn = p;
            rig = new PersonRig(transform, 0.95f, 12);
            shirt = p.ShirtColor;
            skin = PersonRig.SkinFor(p.Id);
            hair = PersonRig.HairFor(p.Id + 3);
            rig.SetColors(shirt, skin, hair);

            var scooterGo = new GameObject("Scooter");
            scooterGo.transform.SetParent(transform, false);
            scooter = scooterGo.AddComponent<SpriteRenderer>();
            scooter.sprite = SpriteFactory.CenteredPixel;
            scooter.sharedMaterial = SpriteFactory.LitMaterial;
            scooter.sortingOrder = 10;
            scooter.color = new Color(0.12f, 0.12f, 0.13f);
            scooter.enabled = false;

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

        void LateUpdate()
        {
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
            rig.Update(pawn.Position, lying, Time.deltaTime);
            if (pawn.AimTarget != null && !lying) rig.FaceTowards(pawn.AimTarget.Position);
            rig.SetArmed(pawn.Armed && !pawn.Dead);
            scooter.enabled = pawn.OnScooter;
            if (pawn.OnScooter)
            {
                scooter.transform.localRotation = transform.Find("Pivot").localRotation;
                scooter.transform.localScale = new Vector3(0.36f, 1.15f, 1f);
            }

            var c = pawn.Dead ? DeadShirt : pawn.Health.Downed ? shirt * 0.65f : shirt;
            c.a = 1f;
            bool flash = Time.unscaledTime - pawn.LastHitTime < 0.12f;
            rig.SetColors(flash ? Color.white : c, flash ? Color.white : pawn.Dead ? skin * 0.6f : skin, hair);

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
