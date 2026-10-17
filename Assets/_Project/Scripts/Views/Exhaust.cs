using System.Collections.Generic;
using UnityEngine;

namespace OSistema.Views
{
    /// <summary>Little puffs of two-stroke smoke behind a scooter, thicker when it pulls away.</summary>
    public sealed class Exhaust
    {
        sealed class Puff
        {
            public SpriteRenderer Sprite;
            public float Age, Life;
            public Vector2 Drift;
        }

        readonly Transform parent;
        readonly List<Puff> puffs = new List<Puff>();
        float timer;

        public Exhaust(Transform parent) => this.parent = parent;

        /// <summary>Call every frame. tail: world position of the exhaust; speed 0..1; accelerating thickens it.</summary>
        public void Update(Vector2 tail, float speed, bool accelerating, bool visible, float dt)
        {
            timer -= dt;
            if (visible && (speed > 0.05f || accelerating) && timer <= 0f)
            {
                timer = accelerating ? 0.05f : 0.12f;
                Emit(tail, accelerating);
            }
            foreach (var p in puffs)
            {
                if (!p.Sprite.enabled) continue;
                p.Age += dt;
                float t = p.Age / p.Life;
                if (t >= 1f || !visible)
                {
                    p.Sprite.enabled = false;
                    continue;
                }
                p.Sprite.transform.position += (Vector3)(p.Drift * dt);
                float s = Mathf.Lerp(0.18f, 0.6f, t);
                p.Sprite.transform.localScale = new Vector3(s, s, 1f);
                p.Sprite.color = new Color(0.82f, 0.82f, 0.84f, (1f - t) * 0.35f);
            }
        }

        void Emit(Vector2 at, bool thick)
        {
            Puff p = puffs.Find(x => !x.Sprite.enabled);
            if (p == null)
            {
                if (puffs.Count >= 24) return;
                var go = new GameObject("Smoke");
                go.transform.SetParent(parent, true);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.SoftShadow;
                sr.sharedMaterial = SpriteFactory.UnlitMaterial;
                sr.sortingOrder = 9;
                p = new Puff { Sprite = sr };
                puffs.Add(p);
            }
            p.Sprite.enabled = true;
            p.Age = 0f;
            p.Life = thick ? 1.1f : 0.7f;
            p.Drift = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.1f, 0.4f));
            p.Sprite.transform.position = new Vector3(at.x, at.y, 0f);
        }

        public void Clear()
        {
            foreach (var p in puffs)
                if (p.Sprite != null) Object.Destroy(p.Sprite.gameObject);
            puffs.Clear();
        }
    }
}
