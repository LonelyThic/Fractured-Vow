using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Small runtime particle bursts: sparks on hits, dust on deaths and walls, sparkles on pickups.
    /// Uses the particle materials the Production Pass saves in Resources/FVFX.
    /// </summary>
    public static class GameFX
    {
        static Material add, alpha;
        static bool loaded;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            add = Resources.Load<Material>("FVFX/FVP_FX_Additive");
            alpha = Resources.Load<Material>("FVFX/FVP_FX_AlphaBlend");
        }

        public static void Sparks(Vector3 pos, Color color, int count = 14)
        {
            var ps = Make("FX Sparks", pos, true);
            if (ps == null) return;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.startColor = color;
            main.gravityModifier = 1.2f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.1f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = 1.5f;
            Fire(ps, count);
        }

                /// <summary>A quick soft flash of light where something is struck (one bright glow that pops and fades).</summary>
        public static void Flash(Vector3 pos, Color color, float size = 0.8f)
        {
            var ps = Make("FX Flash", pos, true);
            if (ps == null) return;
            var main = ps.main;
            main.startLifetime = 0.14f;
            main.startSpeed = 0f;
            main.startSize = size;
            main.startColor = color;
            var sh = ps.shape; sh.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = FadeOut();
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.3f));
            Fire(ps, 1);
        }

        /// <summary>
        /// A sword striking: a white flash with a short bright streak across it, and a few hot sparks.
        /// 'across' is the swing direction (the streak lies along it).
        /// </summary>
        public static void SwordHit(Vector3 pos, Vector3 across, Color sparks)
        {
            Flash(pos, new Color(1f, 1f, 1f, 0.9f), 0.9f);
            var ps = Make("FX Hit Streak", pos, true);
            if (ps != null)
            {
                var main = ps.main;
                main.startLifetime = 0.12f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(9f, 12f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.1f);
                main.startColor = Color.white;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 4f; sh.radius = 0.01f;
                ps.transform.rotation = Quaternion.LookRotation(across.sqrMagnitude > 0.001f ? across : Vector3.right);
                var col = ps.colorOverLifetime; col.enabled = true; col.color = FadeOut();
                var r = ps.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.09f; r.lengthScale = 2f;
                ps.transform.position = pos - ps.transform.forward * 0.6f;
                Fire(ps, 2);
            }
            Sparks(pos, sparks, 8);
        }

        public static void Dust(Vector3 pos, float radius = 0.8f, int count = 24)
        {
            var ps = Make("FX Dust", pos, false);
            if (ps == null) return;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1f);
            main.startColor = new Color(0.45f, 0.42f, 0.38f, 0.45f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.05f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = radius;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = FadeOut();
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            Fire(ps, count);
        }

        public static void Burst(Vector3 pos, Color color, int count = 20)
        {
            var ps = Make("FX Burst", pos, true);
            if (ps == null) return;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = color;
            main.gravityModifier = -0.2f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = FadeOut();
            Fire(ps, count);
        }

        /// <summary>A looping particle trail that follows a spell bolt: embers for fire, glitter for frost.</summary>
        public static void Trail(Transform host, Color color, bool frost)
        {
            Load();
            if (add == null) return;
            var go = new GameObject("FX Trail");
            go.transform.SetParent(host, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(frost ? 0.35f : 0.25f, frost ? 0.6f : 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(frost ? 0.05f : 0.12f, frost ? 0.12f : 0.3f);
            main.startColor = color;
            main.gravityModifier = frost ? 0.3f : -0.15f;
            main.maxParticles = 200;
            var em = ps.emission; em.rateOverTime = frost ? 70f : 90f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.12f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 0.3f), new GradientColorKey(frost ? Color.white : new Color(0.6f, 0.1f, 0.02f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = add;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }

        /// <summary>Spell impact: a hot spark burst plus a soft puff (fire), or a shard spray plus icy mist (frost).</summary>
        public static void Explosion(Vector3 pos, Color color, bool frost)
        {
            var ps = Make("FX Explosion", pos, true);
            if (ps == null) return;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, frost ? 0.8f : 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(frost ? 3f : 2f, frost ? 7f : 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(frost ? 0.05f : 0.1f, frost ? 0.14f : 0.35f);
            main.startColor = Color.Lerp(color, Color.white, 0.3f);
            main.gravityModifier = frost ? 1f : 0.2f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = FadeOut();
            if (frost)
            {
                var r = ps.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.06f; r.lengthScale = 1.2f;
            }
            Fire(ps, frost ? 34 : 40);

            var puff = Make("FX Puff", pos, !frost);
            if (puff == null) return;
            var pm = puff.main;
            pm.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            pm.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            pm.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            pm.startColor = frost ? new Color(0.7f, 0.9f, 1f, 0.22f) : new Color(color.r, color.g * 0.6f, color.b * 0.4f, 0.35f);
            var psh = puff.shape; psh.shapeType = ParticleSystemShapeType.Sphere; psh.radius = 0.3f;
            var pc = puff.colorOverLifetime; pc.enabled = true; pc.color = FadeOut();
            var ps2 = puff.sizeOverLifetime; ps2.enabled = true; ps2.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            Fire(puff, 5);
        }

        static ParticleSystem.MinMaxGradient FadeOut()
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        static ParticleSystem Make(string name, Vector3 pos, bool additive)
        {
            Load();
            var mat = additive ? add : alpha;
            if (mat == null) return null;
            var go = new GameObject(name);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            var em = ps.emission; em.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.Destroy(go, 3f);
            return ps;
        }

        static void Fire(ParticleSystem ps, int count)
        {
            var em = ps.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            ps.Play();
        }
    }
}
