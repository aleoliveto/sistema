using OSistema.Npcs;
using UnityEngine;

namespace OSistema.Views
{
    public sealed class NpcView : MonoBehaviour
    {
        Npc npc;
        PersonRig rig;
        SpriteRenderer scooter;

        public void Init(Npc n)
        {
            npc = n;
            float scale = n.Kind == NpcKind.Child ? 0.7f : 0.92f;
            rig = new PersonRig(transform, scale, 9);
            var hair = n.Kind == NpcKind.Police ? new Color(0.1f, 0.12f, 0.25f) : PersonRig.HairFor(n.Id);
            if (n.Kind == NpcKind.Elder) hair = new Color(0.75f, 0.75f, 0.75f);
            rig.SetColors(n.Color, PersonRig.SkinFor(n.Id), hair);

            if (n.Kind == NpcKind.Scooter)
            {
                var go = new GameObject("Scooter");
                go.transform.SetParent(rig.Root, false);
                scooter = go.AddComponent<SpriteRenderer>();
                scooter.sprite = SpriteFactory.CenteredPixel;
                scooter.sharedMaterial = SpriteFactory.LitMaterial;
                scooter.sortingOrder = 8;
                scooter.color = new Color(0.75f, 0.1f, 0.1f);
            }
            LateUpdate();
        }

        void LateUpdate()
        {
            if (npc == null || rig == null) return;
            if (npc.Despawned)
            {
                Destroy(gameObject);
                return;
            }
            bool visible = FogOfWar.CanSee(npc.Mover.Position);
            rig.SetVisible(visible);
            if (scooter != null) scooter.enabled = visible;
            rig.Update(npc.Mover.Position, false, Time.deltaTime);

            if (scooter != null)
            {
                // The scooter sits under the rider, pointing the way they're heading.
                var pivot = rig.Root.Find("Pivot");
                scooter.transform.localRotation = pivot.localRotation;
                scooter.transform.localScale = new Vector3(0.35f, 1.1f, 1f);
            }
        }
    }
}
