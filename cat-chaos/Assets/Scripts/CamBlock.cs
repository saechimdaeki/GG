using UnityEngine;

namespace CatChaos
{
    /// <summary>
    /// Marks walls, panes and tall furniture. When one sits between the camera and the cat it is hidden
    /// (shadows stay), dollhouse style, so the camera never has to squeeze in against a wall.
    /// </summary>
    public sealed class CamBlock : MonoBehaviour { }
}
