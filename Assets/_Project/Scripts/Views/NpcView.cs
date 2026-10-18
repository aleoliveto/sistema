using OSistema.Npcs;
using UnityEngine;

namespace OSistema.Views
{
    public sealed class NpcView : MonoBehaviour
    {
        Npc npc;
        PersonRig rig;
        Vector2 standLast;
        bool faced;

        Vector2 AwayFromWalls()
        {
            var map = OSistema.Core.GameRunner.CurrentMap;
            if (map == null) return Vector2.down;
            var c = npc.Mover.Cell;
            var away = Vector2.zero;
            for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                if ((dx == 0 && dy == 0) || !map.InBounds(c.x + dx, c.y + dy)) continue;
                if (!map.IsPassable(c.x + dx, c.y + dy)) away -= new Vector2(dx, dy) / (dx * dx + dy * dy);
            }
            return away.sqrMagnitude > 0.01f ? away.normalized : Vector2.down;
        }

        void OnDestroy()
        {
            if (headlight != null) Destroy(headlight.gameObject);
        }

        UnityEngine.Rendering.Universal.Light2D headlight;
        SpriteRenderer scooter;
        Exhaust exhaust;
        Vector2 exhaustLast;
        bool contactDressed;

        // Ambulance
        SpriteRenderer van, cross;
        UnityEngine.Rendering.Universal.Light2D beacon;
        float sirenTimer;
        Vector2 lastPos;

        public void Init(Npc n)
        {
            npc = n;
            if (n.Kind == NpcKind.Ambulance)
            {
                BuildAmbulance();
                LateUpdate();
                return;
            }
            float scale = n.Kind == NpcKind.Child ? 0.7f : 0.92f;
            rig = new PersonRig(transform, scale, 9);
            var hair = n.Kind == NpcKind.Police ? new Color(0.1f, 0.12f, 0.25f) : PersonRig.HairFor(n.Id);
            if (n.Kind == NpcKind.Elder) hair = new Color(0.75f, 0.75f, 0.75f);
            rig.SetColors(n.Color, PersonRig.SkinFor(n.Id), hair);
            switch (n.Kind)
            {
                case NpcKind.Police: rig.ApplyLook(OSistema.Pawns.Look.Police(n.Id)); break;
                case NpcKind.Elder: rig.ApplyLook(n.Id % 2 == 0 ? OSistema.Pawns.Look.Woman(n.Id, old: true) : OSistema.Pawns.Look.Old(n.Id)); break;
                case NpcKind.Child: rig.ApplyLook(n.Id % 3 == 0 ? OSistema.Pawns.Look.Woman(n.Id) : OSistema.Pawns.Look.Young(n.Id)); break;   // drawn smaller
                case NpcKind.Paramedic: rig.ApplyLook(new OSistema.Pawns.Look { Body = OSistema.Pawns.Look.Street + 4, Head = n.Id % 16, Hair = 0 }); break;
                default: rig.ApplyLook(OSistema.Pawns.Look.Passerby(n.Id + 5000)); break;
            }

            if (n.Kind == NpcKind.Scooter)
            {
                var go = new GameObject("Scooter");
                go.transform.SetParent(rig.Root, false);
                scooter = go.AddComponent<SpriteRenderer>();
                scooter.sprite = SpriteFactory.ScooterBody;
                scooter.sharedMaterial = SpriteFactory.LitMaterial;
                scooter.sortingOrder = 8;
                exhaust = new Exhaust(transform.parent != null ? transform.parent : transform);
                headlight = WorldLighting.Headlight(transform.parent != null ? transform.parent : transform);
                rig.Seated = true;
                // Every scooter its own colour: Liberty white, Vespa red, matte black, sky blue…
                var bodies = new[] { new Color(0.92f, 0.92f, 0.9f), new Color(0.75f, 0.1f, 0.1f), new Color(0.12f, 0.12f, 0.13f),
                    new Color(0.45f, 0.65f, 0.85f), new Color(0.85f, 0.75f, 0.3f), new Color(0.3f, 0.5f, 0.3f), new Color(0.55f, 0.55f, 0.58f) };
                scooter.color = bodies[Mathf.Abs(n.Id * 7) % bodies.Length];
            }
            LateUpdate();
        }

        void BuildAmbulance()
        {
            van = gameObject.AddComponent<SpriteRenderer>();
            van.sprite = SpriteFactory.CenteredPixel;
            van.sharedMaterial = SpriteFactory.LitMaterial;
            van.sortingOrder = 14;
            van.color = new Color(0.95f, 0.95f, 0.93f);
            transform.localScale = new Vector3(0.9f, 1.8f, 1f);

            var crossGo = new GameObject("Cross");
            crossGo.transform.SetParent(transform, false);
            crossGo.transform.localScale = new Vector3(0.45f, 0.22f, 1f);
            cross = crossGo.AddComponent<SpriteRenderer>();
            cross.sprite = SpriteFactory.CenteredPixel;
            cross.sharedMaterial = SpriteFactory.LitMaterial;
            cross.sortingOrder = 15;
            cross.color = new Color(0.8f, 0.1f, 0.1f);

            var lightGo = new GameObject("Beacon");
            lightGo.transform.SetParent(transform, false);
            beacon = lightGo.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            beacon.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
            beacon.color = new Color(0.3f, 0.5f, 1f);
            beacon.pointLightOuterRadius = 4f;
            beacon.intensity = 1.5f;
        }

        void UpdateAmbulance()
        {
            var pos = npc.Mover.Position;
            transform.position = new Vector3(pos.x, pos.y, 0f);
            var delta = pos - lastPos;
            if (delta.sqrMagnitude > 0.00002f)
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-delta.x, delta.y) * Mathf.Rad2Deg);
            lastPos = pos;

            bool visible = FogOfWar.CanSee(pos);
            van.enabled = cross.enabled = visible;
            beacon.enabled = visible && Mathf.Repeat(Time.time * 3f, 1f) < 0.5f;

            // The siren carries further than you can see.
            sirenTimer -= Time.deltaTime;
            if (sirenTimer <= 0f && npc.State != 1)
            {
                sirenTimer = 1.6f;
                SoundManager.PlayAt(AudioSynth.Siren, pos, 0.25f);
            }
        }

        void LateUpdate()
        {
            if (npc == null) return;
            if (npc.Despawned)
            {
                exhaust?.Clear();
                Destroy(gameObject);
                return;
            }
            if (van != null)
            {
                UpdateAmbulance();
                return;
            }
            if (rig == null) return;
            // Contacts are named after they appear: dress them for their part.
            if (!contactDressed && npc.ContactName != null && npc.ContactName != "visitor")
            {
                contactDressed = true;
                var look = npc.ContactName == OSistema.Crime.ContactBook.Nanninella
                    ? new OSistema.Pawns.Look { Body = OSistema.Pawns.Look.Women + 7, Head = 3, Hair = OSistema.Pawns.Look.Women + 8, Face = OSistema.Pawns.Look.Women + 4 }
                    : OSistema.Pawns.Look.Old(npc.ContactName.GetHashCode());
                rig.ApplyLook(look);
            }
            bool visible = FogOfWar.CanSee(npc.Mover.Position);
            rig.SetVisible(visible);
            if (scooter != null) scooter.enabled = visible;
            // Shopkeepers, vendors and contacts standing at their spot look out at the street, not at the wall.
            if ((npc.IsFixture || npc.ContactName != null) && (npc.Mover.Position - standLast).sqrMagnitude < 0.0001f)
            {
                if (!faced) { rig.LookTowards(AwayFromWalls()); faced = true; }
            }
            else faced = false;
            standLast = npc.Mover.Position;
            rig.Update(npc.Mover.Position, false, Time.deltaTime);

            if (scooter != null)
            {
                // The scooter sits under the rider, pointing the way they're heading.
                var pivot = rig.Root.Find("Pivot");
                int model = Mathf.Abs(npc.Id * 13) % 10;
                model = model < 4 ? 0 : model < 7 ? 1 : model < 9 ? 2 : 3;   // most people ride cheap
                PropArt.DressScooter(scooter, pivot.localEulerAngles.z, model, Mathf.Abs(npc.Id * 7), scooter.color);
                WorldLighting.AimHeadlight(headlight, visible, npc.Mover.Position, pivot.localEulerAngles.z);
                float h = pivot.localEulerAngles.z * Mathf.Deg2Rad;
                var fwd = new Vector2(-Mathf.Sin(h), Mathf.Cos(h));
                float speed = (npc.Mover.Position - exhaustLast).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
                exhaustLast = npc.Mover.Position;
                exhaust.Update(npc.Mover.Position - fwd * 0.75f, Mathf.Clamp01(speed / 8f), false, visible, Time.deltaTime);
            }
        }
    }
}
