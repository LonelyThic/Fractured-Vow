using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Makes every run different: from the run's seed it promotes some husks to tougher, glowing Elites
    /// (more health, harder hits, quicker dodges, 2.5x shards) and shuffles where spell tomes and gear lie.
    /// Runs once when each floor loads.
    /// </summary>
    public class RunRandomizer : MonoBehaviour
    {
        [Range(0f, 1f)] public float eliteChance = 0.15f;

        void Start()
        {
            int seed = RunState.Seed * 31 + PlayerProgress.SceneName.GetHashCode();
            var rng = new System.Random(seed);
            EnemyScaling.Populate(new System.Random(seed + 7)); // Hard / Nightmare: extra husks

            foreach (var ai in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
            {
                ProceduralMotion.On(ai.gameObject);
                if (rng.NextDouble() < eliteChance) MakeElite(ai);
            }

            var loot = new List<Pickup>();
            foreach (var p in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
                if (p.type == PickupType.Spell || p.type == PickupType.Gear) loot.Add(p);
            for (int i = loot.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                Vector3 a = loot[i].transform.position, b = loot[j].transform.position;
                loot[i].transform.position = b; loot[j].transform.position = a;
            }
            EnemyScaling.ScaleAll(); // match the knight's level and the difficulty

        }

        static void MakeElite(EnemyAI ai)
        {
            ai.elite = true;
            ai.attackDamage *= 1.4f;
            ai.chaseSpeed *= 1.12f;
            ai.baseColor = new Color(1f, 0.55f, 0.4f);
            ai.name = "Elite " + ai.name;
            ai.transform.localScale *= 1.12f;
            var h = ai.GetComponent<Health>();
            if (h != null) { h.maxHealth *= 1.8f; h.Revive(); }
            var lt = new GameObject("Elite Glow").AddComponent<Light>();
            lt.transform.SetParent(ai.transform, false);
            lt.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            lt.type = LightType.Point; lt.color = new Color(1f, 0.35f, 0.15f); lt.range = 4f; lt.intensity = 2f;
        }
    }
}
