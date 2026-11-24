using System.Collections.Generic;
using UnityEngine;

namespace ResidualSignal
{
    /// <summary>Allied bodies still block infected, scenery and raycasts, but do not push one another.</summary>
    static class SquadCollision
    {
        static readonly HashSet<Collider> Bodies = new();

        public static void Register(Collider body)
        {
            if (body == null || !Bodies.Add(body))
                return;
            foreach (var squadmate in Bodies)
                if (squadmate != null && squadmate != body)
                    Physics.IgnoreCollision(body, squadmate);
        }

        public static void Unregister(Collider body)
        {
            if (body == null || !Bodies.Remove(body))
                return;
            foreach (var squadmate in Bodies)
                if (squadmate != null && squadmate != body)
                    Physics.IgnoreCollision(body, squadmate, false);
        }
    }
}
