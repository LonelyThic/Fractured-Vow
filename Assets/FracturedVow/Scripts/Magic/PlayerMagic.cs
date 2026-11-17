using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    public enum SpellType { Firebolt, FrostShard, StoneWall }

    [Serializable]
    public class Spell
    {
        public string displayName;
        public SpellType type;
        public float manaCost = 15f;
        public float castTime = 0.35f;
        public float cooldown = 0.6f;
        public int level = 1;
        public Color color = Color.red;
        [NonSerialized] public float readyTime;

        public static Spell Create(SpellType type)
        {
            switch (type)
            {
                case SpellType.FrostShard:
                    return new Spell { displayName = "Frost Shard", type = type, manaCost = 12f, castTime = 0.3f, cooldown = 0.5f, color = new Color(0.45f, 0.8f, 1f) };
                case SpellType.StoneWall:
                    return new Spell { displayName = "Stone Wall", type = type, manaCost = 25f, castTime = 0.5f, cooldown = 3f, color = new Color(0.6f, 0.55f, 0.5f) };
                default:
                    return new Spell { displayName = "Firebolt", type = SpellType.Firebolt, manaCost = 15f, castTime = 0.35f, cooldown = 0.6f, color = new Color(1f, 0.45f, 0.1f) };
            }
        }
    }

    /// <summary>
    /// Rune magic. E casts the equipped spell toward the screen centre, Q cycles, 1-3 picks a slot.
    /// Firebolt = damage, Frost Shard = damage + slow (status effect), Stone Wall = raises terrain cover.
    /// Picking up a tome you already know levels the spell up.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerMagic : MonoBehaviour
    {
        public List<Spell> spells = new List<Spell> { Spell.Create(SpellType.Firebolt) };
        public int currentIndex;

        [Header("Casting")]
        public Vector3 castOffset = new Vector3(0f, 1.4f, 0.8f);
        public float projectileSpeed = 22f;
        public float aimDistance = 60f;

        [Header("Materials (assigned by the scene builder)")]
        public Material effectMaterial;
        public Material wallMaterial;



        [HideInInspector] public bool magicEnabled = true;

        public bool IsCasting => Time.time < castEndTime;
        public Spell Current => spells.Count > 0 ? spells[Mathf.Clamp(currentIndex, 0, spells.Count - 1)] : null;

        PlayerInputHandler input;
        PlayerStats stats;
        PlayerCombat combat;
        Health health;
        AnimatorBridge anim;
        float castEndTime;

        void Awake()
        {
            input = GetComponent<PlayerInputHandler>();
            stats = GetComponent<PlayerStats>();
            combat = GetComponent<PlayerCombat>();
            health = GetComponent<Health>();
            anim = GetComponent<AnimatorBridge>();
        }

        void Update()
        {
            if (!magicEnabled || (health != null && health.IsDead)) return;

            int bar = Mathf.Min(HotbarSize, spells.Count);
            if (currentIndex >= bar) currentIndex = 0;
            if (input.NextSpellPressed && bar > 1)
            {
                currentIndex = (currentIndex + 1) % bar;
                PrototypeHUD.Message("Equipped " + Current.displayName, 1.2f);
            }
            if (input.SpellSlotPressed >= 0 && input.SpellSlotPressed < bar)
            {
                currentIndex = input.SpellSlotPressed;
                PrototypeHUD.Message("Equipped " + Current.displayName, 1.2f);
            }
            if (input.CastPressed) TryCast();
        }

        void TryCast()
        {
            var spell = Current;
            if (spell == null) { PrototypeHUD.Message("No spell learned yet"); return; }
            if (IsCasting) return;
            if (combat != null && (combat.IsAttacking || combat.IsBlocking || combat.IsCharging)) return;
            var pc = GetComponent<PlayerController>();
            if (pc != null && pc.IsDodging) return;
            if (Time.time < spell.readyTime) { PrototypeHUD.Message(spell.displayName + " is recharging", 0.8f); return; }
            if (stats != null && !stats.TryUseMana(spell.manaCost)) { PrototypeHUD.Message("Not enough mana", 1f); return; }

            Vector3 f = CameraForwardFlat();
            if (f.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(f);

            castEndTime = Time.time + spell.castTime;
            GameAudio.Play("spell_cast", transform.position + Vector3.up * 1.4f, 0.8f);
            spell.readyTime = Time.time + spell.castTime + spell.cooldown * (stats != null ? stats.cooldownMult : 1f);
            if (anim != null) anim.Trigger("Cast");
            StartCoroutine(Release(spell));
        }

        IEnumerator Release(Spell spell)
        {
            // charge-up glow in the knight's hand
            ImpactFlash.Spawn(transform.TransformPoint(castOffset), spell.color, effectMaterial, 0.25f);
            GameFX.Burst(transform.TransformPoint(castOffset), spell.color, 18);
            var lib = FVLibrary.Get;
            if (lib != null && lib.castFlash != null) MagicProjectile.PlayEffect(lib.castFlash, transform.TransformPoint(castOffset), 0.4f);
            yield return new WaitForSeconds(spell.castTime);
            if (health != null && health.IsDead) yield break;

            Vector3 origin = transform.TransformPoint(castOffset);
            Vector3 dir = AimDirection(origin);
            int lvl = spell.level - 1;
            float power = stats != null ? stats.spellMultiplier : 1f;

            switch (spell.type)
            {
                case SpellType.Firebolt:
                    MagicProjectile.Spawn(origin, dir, gameObject, spell.color, effectMaterial,
                        (20f + 6f * lvl) * power, 3f, 1f, 0f, projectileSpeed, 0.35f,
                        lib != null ? lib.fireBolt : null, lib != null ? lib.fireImpact : null, lib != null ? lib.fireTrail : null);
                    break;
                case SpellType.FrostShard:
                    MagicProjectile.Spawn(origin, dir, gameObject, spell.color, effectMaterial,
                        (8f + 3f * lvl) * power, 0.5f, 0.45f, 3f + 0.75f * lvl, projectileSpeed * 1.2f, 0.25f,
                        lib != null ? lib.frostBolt : null, lib != null ? lib.frostImpact : null);
                    break;
                case SpellType.StoneWall:
                    StoneWall.Spawn(transform, wallMaterial, 6f + 2f * lvl);
                    if (lib != null && lib.wallBurst != null) MagicProjectile.PlayEffect(lib.wallBurst, transform.position + transform.forward * 3f, 0.8f);
                    break;
            }
        }

        Vector3 AimDirection(Vector3 origin)
        {
            Camera cam = Camera.main;
            if (cam == null) return transform.forward;
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 point = ray.GetPoint(aimDistance);
            float best = aimDistance;
            foreach (var h in Physics.RaycastAll(ray, aimDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.transform.IsChildOf(transform.root)) continue;
                if (h.distance < best) { best = h.distance; point = h.point; }
            }
            Vector3 dir = point - origin;
            if (dir.sqrMagnitude < 1f || Vector3.Dot(dir.normalized, transform.forward) < 0.2f) return transform.forward;
            return dir.normalized;
        }

        Vector3 CameraForwardFlat()
        {
            Camera cam = Camera.main;
            Vector3 f = cam != null ? cam.transform.forward : transform.forward;
            f.y = 0f;
            return f.normalized;
        }

        public const int HotbarSize = 3;

        /// <summary>Called by spell tome pickups. A tome dropped earlier remembers its level.</summary>
        public void LearnSpell(SpellType type, int level = 1)
        {
            var known = spells.Find(s => s.type == type);
            if (known != null)
            {
                known.level = Mathf.Max(known.level + 1, level);
                PrototypeHUD.Message(known.displayName + " upgraded to level " + known.level, 3f);
                GameAudio.PlayUI("spell_learn");
                SyncToRun();
                return;
            }
            var s = Spell.Create(type);
            s.level = Mathf.Max(1, level);
            spells.Add(s);
            if (spells.Count <= HotbarSize)
            {
                currentIndex = spells.Count - 1;
                PrototypeHUD.Message("Learned " + s.displayName + "! Press E to cast, Q to swap.", 3.5f);
            }
            else PrototypeHUD.Message("Learned " + s.displayName + " - it's in your bag. Equip it from the Spells page (Tab).", 3.5f);
            GameAudio.PlayUI("spell_learn");
            SyncToRun();
        }

        /// <summary>Takes the run's spells (they carry over between floors).</summary>
        public void LoadFromRun()
        {
            spells.Clear();
            foreach (var save in RunState.Spells)
            {
                var s = Spell.Create(save.type);
                s.level = Mathf.Max(1, save.level);
                spells.Add(s);
            }
            currentIndex = 0;
        }

        public void SyncToRun()
        {
            RunState.Spells.Clear();
            foreach (var s in spells) RunState.Spells.Add(new SpellSave { type = s.type, level = s.level });
        }

        /// <summary>Puts the spell at 'from' into hotbar slot 'slot' (swapping with whatever was there).</summary>
        public void MoveSpell(int from, int slot)
        {
            if (from < 0 || from >= spells.Count || slot < 0 || slot >= spells.Count || from == slot) return;
            var t = spells[slot]; spells[slot] = spells[from]; spells[from] = t;
            currentIndex = slot;
            SyncToRun();
            GameAudio.PlayUI("ui_click");
        }

        /// <summary>Removes a spell and leaves its tome (with its level) on the ground in front of the knight.</summary>
        public void DropSpell(int index)
        {
            if (index < 0 || index >= spells.Count) return;
            var s = spells[index];
            spells.RemoveAt(index);
            currentIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, spells.Count - 1));
            SyncToRun();

            var go = RuntimeVisuals.Primitive(PrimitiveType.Cube, effectMaterial, s.color, false);
            go.name = s.displayName + " tome";
            go.transform.position = transform.position + transform.forward * 1.3f + Vector3.up * 0.8f;
            go.transform.localScale = new Vector3(0.5f, 0.15f, 0.4f);
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true; col.radius = 1.6f;
            var lt = go.AddComponent<Light>();
            lt.type = LightType.Point; lt.color = s.color; lt.range = 3f; lt.intensity = 1.5f;
            var p = go.AddComponent<Pickup>();
            p.type = PickupType.Spell; p.spell = s.type; p.amount = s.level; p.autoPickup = false;
            p.displayName = s.displayName + " tome (Lv " + s.level + ")";
            PrototypeHUD.Message("Dropped " + s.displayName, 1.5f);
            GameAudio.Play("pickup", go.transform.position, 0.5f, 0.05f, 0.7f);
        }

        /// <summary>One line of numbers for the Spells page.</summary>
        public string Describe(Spell s)
        {
            int lvl = s.level - 1;
            float power = stats != null ? stats.spellMultiplier : 1f;
            float cd = (s.castTime + s.cooldown * (stats != null ? stats.cooldownMult : 1f));
            switch (s.type)
            {
                case SpellType.Firebolt: return Mathf.RoundToInt((20f + 6f * lvl) * power) + " damage   " + s.manaCost + " mana   " + cd.ToString("0.0") + " s";
                case SpellType.FrostShard: return Mathf.RoundToInt((8f + 3f * lvl) * power) + " damage, slows " + (3f + 0.75f * lvl).ToString("0.0") + " s   " + s.manaCost + " mana   " + cd.ToString("0.0") + " s";
                default: return "wall lasts " + (6f + 2f * lvl) + " s   " + s.manaCost + " mana   " + cd.ToString("0.0") + " s";
            }
        }
    }
}
