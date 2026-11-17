using UnityEngine;

namespace FracturedVow
{
    /// <summary>A boss whose hazards (orbs, waves) report hits back to it, so it can damage the knight and shove his boat.</summary>
    public interface IBossHitter
    {
        void HitPlayer(float amount, Vector3 direction, float shove);
    }
}
