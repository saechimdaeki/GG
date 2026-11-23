using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ResidualSignal
{
    public sealed partial class StationTrial
    {
        // Around existing walls: station wings, ticket hall, transfer block and service passage.
        static readonly Vector3[] EntryPockets =
        {
            new(-7.2f, 0, -5.6f), new(7.2f, 0, -5.6f),
            new(-4.6f, 0, 0.1f), new(4.6f, 0, 0.1f),
            new(-4.2f, 0, 7.0f), new(4.2f, 0, 7.0f),
            new(-3.4f, 0, 12.2f), new(3.4f, 0, 12.2f),
            new(-3.4f, 0, 15.8f), new(3.4f, 0, 15.8f),
            new(-3.7f, 0, 27.2f), new(3.7f, 0, 27.2f),
            new(-1.7f, 0, 34.2f), new(1.7f, 0, 34.2f),
            new(-4.4f, 0, 39.7f), new(4.4f, 0, 42.5f), new(3.7f, 0, 46.0f),
            new(-4.4f, 0, 48.7f), new(4.3f, 0, 50.7f), new(4.5f, 0, 58.7f),
            new(-5.3f, 0, 57.2f), new(4.8f, 0, 65.1f),
        };
        static readonly Vector3[] CoverSamples =
        {
            new(0, 0.27f, 0), new(0, 1.30f, 0), new(0, 2.62f, 0),
            new(-0.59f, 1.43f, 0), new(0.59f, 1.43f, 0),
            new(0, 1.43f, -0.59f), new(0, 1.43f, 0.59f),
            new(-0.48f, 2.1f, 0), new(0.48f, 2.1f, 0),
        };

        sealed class Reinforcements
        {
            public int Remaining;
            public Vector3 Near;
            public float Expires;
            public bool Alert;
            public bool Calm;
            public InfectedKind Special;
        }

        readonly List<Reinforcements> routes = new();
        readonly RaycastHit[] coverHits = new RaycastHit[24];
        NavMeshPath entryPath;
        float nextDelivery;
        bool initialPopulationPrepared;

        void QueueReinforcements(int count, Vector3 near, bool alert, InfectedKind special = InfectedKind.Common, bool calm = false)
        {
            routes.Add(new Reinforcements
            {
                Remaining = count, Near = near, Alert = alert, Calm = calm, Special = special,
                Expires = Time.time + (phase == Phase.Opening ? 16f : 10f)
            });
        }

        void DeliverReinforcements()
        {
            if (phase is Phase.Complete or Phase.Failed)
            {
                routes.Clear();
                return;
            }
            if (routes.Count == 0 || Time.time < nextDelivery)
                return;

            nextDelivery = Time.time + 0.45f;
            var group = routes[0];
            if (Time.time >= group.Expires)
            {
                routes.RemoveAt(0);
                return;
            }

            threats.RemoveAll(threat => threat == null || threat.Dead);
            if (threats.Count >= maxInfected || !TryFindCoveredEntry(group.Near, out Vector3 entry))
                return;

            InfectedKind kind = group.Special;
            if (kind != InfectedKind.Common)
                foreach (var active in threats)
                    if (active.Kind != InfectedKind.Common && active.Alerted &&
                        SquadNear(active.transform.position, 16f))
                    {
                        kind = InfectedKind.Common;
                        break;
                    }
            group.Special = InfectedKind.Common;
            bool alerted = !group.Calm;
            SpawnInfected(entry.x, entry.z, alerted && group.Remaining % 3 == 0, kind, calm: group.Calm);
            if (group.Alert)
            {
                group.Alert = false;
                PlayHorde(entry);
                if (MissionSync.Instance != null)
                    MissionSync.Instance.HordeRpc(entry);
            }
            if (--group.Remaining == 0)
                routes.RemoveAt(0);
        }

        bool TryFindCoveredEntry(Vector3 near, out Vector3 entry)
        {
            entry = default;
            float best = float.PositiveInfinity;
            entryPath ??= new NavMeshPath();
            foreach (var pocket in EntryPockets)
            {
                for (int offset = -1; offset <= 1; offset++)
                {
                    Vector3 sample = pocket + new Vector3(offset * 0.9f, 0, offset * 0.48f);
                    if (!NavMesh.SamplePosition(sample, out var origin, 0.6f, NavMesh.AllAreas))
                        continue;
                    float score = (origin.position - near).sqrMagnitude;
                    if (score >= best || !IsConcealedEntry(origin.position))
                        continue;
                    if (!HasApproach(origin.position))
                        continue;
                    best = score;
                    entry = origin.position;
                }
            }
            return best < float.PositiveInfinity;
        }

        bool HasApproach(Vector3 entry)
        {
            foreach (var member in SquadMember.All)
                if (member.Available &&
                    NavMesh.SamplePosition(member.body.position, out var destination, 1.5f, NavMesh.AllAreas) &&
                    NavMesh.CalculatePath(entry, destination.position, NavMesh.AllAreas, entryPath) &&
                    entryPath.status == NavMeshPathStatus.PathComplete)
                    return true;
            return false;
        }

        // Check physical cover even behind the camera. A player turning during replication
        // must see an infected enter through a passage, not materialize in open floor space.
        public bool IsConcealedEntry(Vector3 entry)
        {
            foreach (var threat in threats)
                if (threat != null && !threat.Dead && (threat.transform.position - entry).sqrMagnitude < 1.15f)
                    return false;
            foreach (var member in SquadMember.All)
            {
                if (member.body == null)
                    continue;
                if ((member.body.position - entry).sqrMagnitude < 36f)
                    return false;
                if (member.body.GetComponent<SurvivorBot>() != null)
                    continue;
                Vector3 view = member.body == transform ? eyes.transform.position :
                    member.body.position + Vector3.up * (member.down ? 0.55f : 1.65f);
                foreach (var sample in CoverSamples)
                    if (!CoveredByMap(view, entry + sample))
                        return false;
            }
            return true;
        }

        bool CoveredByMap(Vector3 view, Vector3 sample)
        {
            Vector3 ray = sample - view;
            int hits = Physics.RaycastNonAlloc(view, ray.normalized, coverHits, ray.magnitude - 0.08f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
            {
                var collider = coverHits[i].collider;
                if (collider is CharacterController || !collider.gameObject.isStatic &&
                    (collider.attachedRigidbody != null || collider.GetComponentInParent<SurvivorAvatar>() != null ||
                     collider.GetComponentInParent<SurvivorBot>() != null))
                    continue;
                if (collider.TryGetComponent<Renderer>(out var surface) && surface.enabled &&
                    surface.sharedMaterial != null && surface.sharedMaterial.renderQueue < 3000)
                    return true;
            }
            return false;
        }
    }
}
