using System.Collections.Generic;
using OSistema.Combat;
using UnityEngine;

namespace OSistema.Views
{
    /// <summary>Draws a brief tracer for every shot (and a short streak for punches).</summary>
    public sealed class CombatView : MonoBehaviour
    {
        const float TracerSeconds = 0.14f;

        static readonly Color HitColor = new Color(1f, 0.85f, 0.4f, 1f);
        static readonly Color MissColor = new Color(1f, 1f, 0.8f, 0.6f);

        readonly List<(SpriteRenderer sr, float until)> active = new List<(SpriteRenderer, float)>();
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        CombatSystem combat;

        public void Init(CombatSystem system)
        {
            combat = system;
            combat.Shot += OnShot;
        }

        void OnDestroy()
        {
            if (combat != null) combat.Shot -= OnShot;
        }

        void OnShot(ShotEvent shot)
        {
            var sr = pool.Count > 0 ? pool.Pop() : CreateTracer();
            sr.gameObject.SetActive(true);
            var delta = shot.To - shot.From;
            sr.transform.position = (Vector3)(shot.From + delta * 0.5f);
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            sr.transform.localScale = new Vector3(delta.magnitude, shot.Melee ? 0.12f : 0.06f, 1f);
            sr.color = shot.Hit ? HitColor : MissColor;
            active.Add((sr, Time.unscaledTime + TracerSeconds));
        }

        SpriteRenderer CreateTracer()
        {
            var go = new GameObject("Tracer");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.CenteredPixel;
            sr.sharedMaterial = SpriteFactory.UnlitMaterial;
            sr.sortingOrder = 30;
            return sr;
        }

        void LateUpdate()
        {
            float now = Time.unscaledTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (now < active[i].until) continue;
                active[i].sr.gameObject.SetActive(false);
                pool.Push(active[i].sr);
                active.RemoveAt(i);
            }
        }
    }
}
