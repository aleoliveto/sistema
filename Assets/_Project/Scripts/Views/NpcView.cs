using OSistema.Npcs;
using UnityEngine;

namespace OSistema.Views
{
    public sealed class NpcView : MonoBehaviour
    {
        Npc npc;
        SpriteRenderer body;

        public void Init(Npc n)
        {
            npc = n;
            transform.localScale = Vector3.one * (n.Kind == NpcKind.Police ? 0.8f : 0.7f);
            body = gameObject.AddComponent<SpriteRenderer>();
            body.sprite = SpriteFactory.Disc;
            body.sharedMaterial = SpriteFactory.UnlitMaterial;
            body.sortingOrder = 9;
            LateUpdate();
        }

        void LateUpdate()
        {
            if (npc == null || body == null) return;
            if (npc.Despawned)
            {
                Destroy(gameObject);
                return;
            }
            transform.position = new Vector3(npc.Mover.Position.x, npc.Mover.Position.y, 0f);
            body.color = (Color)npc.Color * WorldLight.Tint;
        }
    }
}
