using System;
using System.Collections.Generic;
using UnityEngine;

namespace ResidualSignal
{
    /// <summary>
    /// A survivor infected can hunt, as seen by the simulation authority (host or solo game).
    /// The local player binds these hooks straight to its StationTrial; a remote player binds them to RPCs.
    /// </summary>
    public sealed class SquadMember
    {
        public static readonly List<SquadMember> All = new();

        public Transform body;
        public bool down;
        public bool held;
        public Action<int> hurt;
        public Action<Vector3, int> knock;
        public Action<Vector3> warn;
        public Func<StationInfected, bool> pin;
        public Func<StationInfected, bool> drag;
        public Action<StationInfected> release;
        public Action<float> blind;

        public bool Available => body != null && !down;

        public static SquadMember Nearest(Vector3 point)
        {
            SquadMember best = null;
            float bestDistance = float.MaxValue;
            foreach (var member in All)
            {
                if (!member.Available)
                    continue;
                float distance = (member.body.position - point).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = member;
                }
            }
            return best;
        }

        public bool TryPin(StationInfected source)
        {
            if (held || down || !pin(source))
                return false;
            held = true;
            return true;
        }

        public bool TryDrag(StationInfected source)
        {
            if (held || down || !drag(source))
                return false;
            held = true;
            return true;
        }

        public void Release(StationInfected source)
        {
            held = false;
            release(source);
        }
    }
}
