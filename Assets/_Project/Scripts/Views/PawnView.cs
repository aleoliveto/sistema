using OSistema.Input;
using OSistema.Pawns;
using UnityEngine;

namespace OSistema.Views
{
    public sealed class PawnView : MonoBehaviour
    {
        static readonly Color SelectedColor = new Color(1f, 1f, 1f, 0.9f);
        static readonly Color DraftedSelectedColor = new Color(1f, 0.35f, 0.3f, 0.95f);

        Pawn pawn;
        SpriteRenderer body;
        SpriteRenderer ring;

        public void Init(Pawn p)
        {
            pawn = p;
            transform.localScale = Vector3.one * 0.8f;

            body = gameObject.AddComponent<SpriteRenderer>();
            body.sprite = SpriteFactory.Disc;
            body.sharedMaterial = SpriteFactory.UnlitMaterial;
            body.sortingOrder = 10;

            var ringGo = new GameObject("Selection");
            ringGo.transform.SetParent(transform, false);
            ringGo.transform.localScale = Vector3.one * 1.35f;
            ring = ringGo.AddComponent<SpriteRenderer>();
            ring.sprite = SpriteFactory.Ring;
            ring.sharedMaterial = SpriteFactory.UnlitMaterial;
            ring.sortingOrder = 11;
            ring.enabled = false;

            LateUpdate();
        }

        void LateUpdate()
        {
            if (pawn == null || body == null) return; // state lost to a script reload during Play

            // In prison: not on the map.
            body.enabled = !pawn.InJail;
            if (pawn.InJail)
            {
                ring.enabled = false;
                return;
            }

            transform.position = new Vector3(pawn.Position.x, pawn.Position.y, 0f);

            // Lying down (asleep, downed, dead) reads as a smaller, flatter body until real sprites exist.
            bool lying = pawn.Dead || pawn.Health.Downed || pawn.Sleep != SleepState.Awake;
            transform.localScale = lying ? new Vector3(0.75f, 0.5f, 1f) : Vector3.one * 0.8f;

            Color c = pawn.ShirtColor;
            if (pawn.Dead) c = new Color(0.35f, 0.35f, 0.35f, 0.8f);
            else if (pawn.Health.Downed) c *= 0.6f;
            body.color = c * WorldLight.Tint;

            var pc = PlayerController.Instance;
            bool selected = pc != null && pc.Selected.Contains(pawn);
            ring.enabled = selected;
            if (selected) ring.color = pawn.Drafted ? DraftedSelectedColor : SelectedColor;
        }
    }
}
