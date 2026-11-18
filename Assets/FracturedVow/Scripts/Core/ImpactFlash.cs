using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// The flash where a spell lands or is cast. It used to be a solid sphere, which read as a big white ball
    /// in the built game; now it's a soft glow that pops and fades (GameFX.Flash).
    /// </summary>
    public static class ImpactFlash
    {
        public static void Spawn(Vector3 pos, Color color, Material template, float size = 0.6f)
        {
            GameFX.Flash(pos, Color.Lerp(color, Color.white, 0.25f), size * 1.3f);
        }
    }
}