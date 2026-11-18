using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FracturedVow
{
    /// <summary>
    /// Floor 2's opening: the knight stands in a dark hallway that ends in a stone wall. The ground rumbles,
    /// the wall cracks, and the Leviathan's head bursts through it from the pool beyond, roars, and sinks back
    /// out of sight, leaving the broken wall as the way in. About 10 seconds; Space, Enter or a click skips it.
    /// </summary>
    public class CutsceneDirector : MonoBehaviour
    {
        public Transform wall;            // parent of the wall chunks
        public LeviathanBody cameo;       // a body used only for this scene
        public Transform risePoint;       // under the pool
        public Transform strikePoint;     // through the hole, into the hallway
        public string title = "FLOOR 2 - THE DROWNED DEEP";
        public string subtitle = "Something enormous lives in these waters.\nFind a boat. Find the way up.";

        public static bool Playing { get; internal set; }

        Camera cam;
        ThirdPersonCamera follow;
        PlayerInputHandler input;
        Transform player;
        bool skip, broken;
        Vector3 hallAxis = Vector3.forward, hallEnd;

        IEnumerator Start()
        {
            if (cameo != null) cameo.gameObject.SetActive(false);
            yield return null;
            while (GameMenu.IsOpen) yield return null; // wait for the title screen if this floor was launched directly
            yield return StartCoroutine(Play());
        }

        IEnumerator Play()
        {
            var pc = FindFirstObjectByType<PlayerController>();
            cam = Camera.main;
            if (pc == null || cam == null || wall == null) { Finish(); yield break; }
            player = pc.transform;
            input = pc.GetComponent<PlayerInputHandler>();
            follow = cam.GetComponent<ThirdPersonCamera>();

            Playing = true;
            playStart = Time.time;
            PrototypeHUD.Hidden = true;
            if (input != null) input.inputEnabled = false;
            if (follow != null) follow.enabled = false;

            Vector3 wallPos = wall.position;
            Vector3 toWall = wallPos - player.position; toWall.y = 0f; toWall.Normalize();
            hallAxis = toWall; hallEnd = wallPos;
            MeasureRoom(wallPos);
            if (cam != null) cam.nearClipPlane = 0.05f;

            Cinema.Say("Narrator", "The Drowned Deep. Where the vow was first sworn... and first broken.", 4f);
            // 1. slow push down the hallway toward the wall while the ground rumbles
            Vector3 camA = player.position - toWall * 3.5f + Vector3.Cross(Vector3.up, toWall) * 1.2f + Vector3.up * 2.8f; // high, over his shoulder
            Vector3 camB = wallPos - toWall * 12f + Vector3.up * 0.6f; // wide enough to frame the whole arch
            GameAudio.Play("rumble", wallPos, 1f, 0f);
            for (float t = 0f; t < 3.6f && !skip; t += Time.deltaTime)
            {
                Shot(Vector3.Lerp(camA, camB, Mathf.SmoothStep(0f, 1f, t / 3.6f)), wallPos + Vector3.up * 0.3f);
                if (t > 1.5f) CameraShake.Shake(0.03f + (t - 1.5f) * 0.03f, 0.2f);
                ShakeWall(t > 1.5f ? 0.015f + (t - 1.5f) * 0.01f : 0f);
                yield return null;
            }

            // 2. the Leviathan smashes through
            if (!skip)
            {
                if (cameo != null)
                {
                    cameo.gameObject.SetActive(true);
                    cameo.head.position = risePoint.position;
                    cameo.head.rotation = Quaternion.LookRotation(Vector3.up * 0.6f - toWall);
                    cameo.Snap();
                }
                Vector3 hover = new Vector3(strikePoint.position.x, strikePoint.position.y + 1.8f, risePoint.position.z - 1f);
                for (float t = 0f; t < 0.45f && !skip; t += Time.deltaTime)
                {
                    if (cameo != null) MoveHead(Vector3.Lerp(risePoint.position, hover, t / 0.45f), -toWall);
                    Shot(camB, wallPos);
                    yield return null;
                }
                BreakWall(toWall);
                GameAudio.Play("boss_roar", wallPos, 1f, 0f, 0.7f);
                CameraShake.Shake(0.6f, 1.2f);
                Cinema.Say("The Leviathan", "VOWBREAKER... I SMELL THE OATH ON YOU.", 3.5f);
                for (float t = 0f; t < 0.3f && !skip; t += Time.deltaTime)
                {
                    if (cameo != null) { MoveHead(Vector3.Lerp(hover, strikePoint.position, t / 0.3f), -toWall); cameo.jawOpen = t / 0.3f; }
                    Shot(camB - toWall * 1.5f, strikePoint.position);
                    yield return null;
                }
                // roar in the knight's face
                for (float t = 0f; t < 1.8f && !skip; t += Time.deltaTime)
                {
                    if (cameo != null)
                    {
                        cameo.jawOpen = 0.7f + Mathf.Sin(t * 9f) * 0.3f;
                        cameo.head.position = strikePoint.position + new Vector3(Mathf.Sin(t * 3f) * 0.25f, Mathf.Sin(t * 5f) * 0.1f, 0f);
                    }
                    Shot(camB - toWall * 1.5f + Vector3.up * 0.3f, strikePoint.position + Vector3.up * 0.2f);
                    yield return null;
                }
                // it pulls back and sinks into the pool
                Vector3 back = risePoint.position + Vector3.up * 4f + toWall * 2f;
                for (float t = 0f; t < 2.6f && !skip; t += Time.deltaTime)
                {
                    float k = t / 2.6f;
                    if (cameo != null)
                    {
                        cameo.jawOpen = 1f - k;
                        Vector3 p = k < 0.5f ? Vector3.Lerp(strikePoint.position, back, k * 2f) : Vector3.Lerp(back, risePoint.position + Vector3.down * 4f, (k - 0.5f) * 2f);
                        MoveHead(p, k < 0.5f ? toWall : Vector3.down + toWall);
                    }
                    // the camera steps up to the broken arch and watches it retreat into the pool
                    // (it starts 4.5 m out from the arch, so the camera holds back at 8 m until the head is gone)
                    Shot(Vector3.Lerp(camB, wallPos - toWall * 8f + Vector3.up * 1.5f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.4f - 0.4f))),
                         cameo != null ? cameo.head.position : wallPos + toWall * 4f);
                    yield return null;
                }
                GameAudio.Play("splash", risePoint.position, 1f);
                GameFX.Dust(new Vector3(risePoint.position.x, 0.2f, risePoint.position.z), 3f, 80);
                var fxLib = FVLibrary.Get;
                if (fxLib != null && fxLib.splash != null) MagicProjectile.PlayEffect(fxLib.splash, new Vector3(risePoint.position.x, 0f, risePoint.position.z), 1.2f);
                CameraShake.Shake(0.3f, 0.6f);
                for (float t = 0f; t < 0.8f && !skip; t += Time.deltaTime)
                {
                    Shot(wallPos - toWall * 4f + Vector3.up * 1.5f, new Vector3(risePoint.position.x, 0f, risePoint.position.z));
                    yield return null;
                }
            }

            // 3. hold on the broken arch for 3 seconds while the last of the rubble tumbles down and settles
            if (!skip)
            {
                Vector3 rubbleCam = wallPos - toWall * 10f + Vector3.up * 1.2f;
                Vector3 rubbleLook = new Vector3(wallPos.x, 0.8f, wallPos.z) - toWall * 3f;
                for (float t = 0f; t < 3f && !skip; t += Time.deltaTime)
                {
                    Shot(rubbleCam + Vector3.Cross(Vector3.up, toWall) * Mathf.Sin(t * 0.6f) * 1.5f, rubbleLook);
                    yield return null;
                }
            }

            Cinema.Say("Narrator", "Find a boat. Finish what you started.", 3f);
            // 4. back to the knight
            if (!broken && !skip) BreakWall(toWall);
            Vector3 from = cam.transform.position;
            Quaternion fromRot = cam.transform.rotation;
            Vector3 behind = player.position - toWall * 4.5f + Vector3.up * 2.6f;
            for (float t = 0f; t < 1f && !skip; t += Time.deltaTime)
            {
                Vector3 lookAt = player.position + Vector3.up * 1.5f + toWall * 4f;
                cam.transform.position = Safe(player.position + Vector3.up * 1.6f, Vector3.Lerp(from, behind, Mathf.SmoothStep(0f, 1f, t)));
                cam.transform.rotation = Quaternion.Slerp(fromRot, Quaternion.LookRotation(lookAt - cam.transform.position), t);
                yield return null;
            }
            Finish();
        }

        float playStart;

        void Update()
        {
            if (!Playing || Cinema.Running) return;
            // safety: never hold the camera hostage (e.g. if something interrupted the scene)
            if (Time.time - playStart > 28f) { StopAllCoroutines(); Finish(); return; }
#if ENABLE_INPUT_SYSTEM
            bool k = (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
                  || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                  || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
#else
            bool k = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
#endif
            if (k) skip = true;
        }

        void OnGUI()
        {
            if (!Playing || UIFocus.Any) return;
            float bar = Screen.height * 0.11f;
            var old = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, bar), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, Screen.height - bar, Screen.width, bar), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = Mathf.RoundToInt(Screen.height / 50f) };
            GUI.Label(new Rect(0f, Screen.height - bar, Screen.width - 30f, bar), "Space to skip", style);
            GUI.color = old;
        }

        float roomHalfWidth = 5f, roomTop = 7f;

        /// <summary>How wide and tall the room in front of the wall really is (the camera stays inside it).</summary>
        void MeasureRoom(Vector3 wallPos)
        {
            Vector3 mid = Vector3.Lerp(new Vector3(player.position.x, wallPos.y, player.position.z), wallPos, 0.5f);
            mid.y = player.position.y + 1.6f;
            Vector3 side = Vector3.Cross(Vector3.up, hallAxis);
            float Dist(Vector3 dir, float max)
            {
                foreach (var h in Physics.RaycastAll(mid, dir, max, ~0, QueryTriggerInteraction.Ignore))
                    if (!h.transform.IsChildOf(player) && (wall == null || !h.transform.IsChildOf(wall)) && h.collider.GetComponentInParent<Health>() == null)
                        max = Mathf.Min(max, h.distance);
                return max;
            }
            roomHalfWidth = Mathf.Clamp(Mathf.Min(Dist(side, 14f), Dist(-side, 14f)) - 0.7f, 0.5f, 6f);
            roomTop = Mathf.Clamp(player.position.y + 1.6f + Dist(Vector3.up, 12f) - 0.6f, player.position.y + 1.6f, player.position.y + 8f);
        }

        void Shot(Vector3 pos, Vector3 look)
        {
            pos = Corridor(pos);
            // first stay in the knight's own room (from his head, never through a wall), then keep the subject in view
            if (player != null) pos = Safe(player.position + Vector3.up * 1.5f, pos);
            pos = Safe(look, pos);
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(look - pos);
        }

        /// <summary>Keeps hallway shots on the hallway's centre line, at a sensible height.</summary>
        Vector3 Corridor(Vector3 p)
        {
            Vector3 rel = p - hallEnd;
            float along = Vector3.Dot(rel, hallAxis);
            if (along > -0.5f) return p; // past the wall (inside the hall beyond): leave as is
            Vector3 side = Vector3.Cross(Vector3.up, hallAxis);
            float lateral = Mathf.Clamp(Vector3.Dot(rel, side), -roomHalfWidth, roomHalfWidth); // measured width
            Vector3 q = hallEnd + hallAxis * along + side * lateral;
            q.y = Mathf.Clamp(p.y, 0.8f, roomTop);                                               // under the ceiling
            return q;
        }

        /// <summary>
        /// Keeps the camera on the near side of any wall between it and what it's looking at, so a shot
        /// never ends up inside or behind the hallway's stone. Ignores the knight and the flying wall chunks.
        /// </summary>
        Vector3 Safe(Vector3 look, Vector3 want)
        {
            Vector3 d = want - look;
            float len = d.magnitude;
            if (len < 0.01f) return want;
            float best = len;
            foreach (var h in Physics.SphereCastAll(look, 0.3f, d / len, len, ~0, QueryTriggerInteraction.Ignore))
            {
                if (player != null && h.transform.IsChildOf(player)) continue;
                if (wall != null && h.transform.IsChildOf(wall)) continue;
                if (cameo != null && h.transform.IsChildOf(cameo.transform)) continue;
                if (h.collider.attachedRigidbody != null && !h.collider.attachedRigidbody.isKinematic) continue; // flying rubble
                if (h.distance > 0f && h.distance < best) best = h.distance;
            }
            return look + d / len * Mathf.Max(0.5f, best - 0.25f);
        }

        void MoveHead(Vector3 pos, Vector3 facing)
        {
            if (cameo == null) return;
            cameo.head.position = pos;
            if (facing.sqrMagnitude > 0.001f) cameo.head.rotation = Quaternion.Slerp(cameo.head.rotation, Quaternion.LookRotation(facing), 10f * Time.deltaTime);
        }

        void ShakeWall(float amount)
        {
            if (wall == null || broken || amount <= 0f) return;
            foreach (Transform c in wall) c.localPosition += Random.insideUnitSphere * amount * 0.1f;
        }

        readonly List<Rigidbody> rubble = new List<Rigidbody>();

        /// <summary>
        /// Blows the arch apart. The built blocks may be merged into the level's static geometry (which can't move),
        /// so they're hidden and replaced by fresh loose blocks that burst out from the impact, tumble and land.
        /// </summary>
        void BreakWall(Vector3 toWall)
        {
            if (broken || wall == null) return;
            broken = true;
            GameAudio.Play("wall_break", wall.position, 1f, 0f);
            GameFX.Dust(wall.position, 4f, 120);
            Vector3 impact = strikePoint != null ? new Vector3(wall.position.x, strikePoint.position.y, wall.position.z) : wall.position;
            var chunks = new List<Transform>();
            foreach (Transform c in wall) chunks.Add(c);
            foreach (var c in chunks)
            {
                var r = c.GetComponent<Renderer>();
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = "Rubble";
                block.transform.SetPositionAndRotation(c.position, c.rotation);
                block.transform.localScale = c.lossyScale;
                if (r != null) block.GetComponent<Renderer>().sharedMaterial = r.sharedMaterial;
                else RuntimeVisuals.FixMaterial(block, new Color(0.4f, 0.38f, 0.35f));
                c.gameObject.SetActive(false);
                var rb = block.AddComponent<Rigidbody>();
                rb.mass = 40f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                // blocks nearest the impact fly hardest; everything bursts out into the antechamber
                Vector3 away = c.position - impact; away.y *= 0.5f;
                float close = Mathf.Clamp01(1f - away.magnitude / 6f);
                Vector3 push = -toWall * Random.Range(5f, 9f) * (0.6f + close) + away.normalized * Random.Range(1f, 4f) + Vector3.up * Random.Range(1.5f, 4.5f);
                rb.AddForce(push, ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 8f, ForceMode.VelocityChange);
                rubble.Add(rb);
            }
            StartCoroutine(SettleRubble());
        }

        /// <summary>Rubble stays as scenery once it has landed (and stops blocking the way).</summary>
        IEnumerator SettleRubble()
        {
            float until = Time.time + 12f;
            while (rubble.Count > 0)
            {
                yield return new WaitForSeconds(0.5f);
                bool timeUp = Time.time > until;
                for (int i = rubble.Count - 1; i >= 0; i--)
                {
                    var rb = rubble[i];
                    if (rb == null) { rubble.RemoveAt(i); continue; }
                    if (!timeUp && !rb.IsSleeping() && rb.linearVelocity.sqrMagnitude > 0.04f) continue;
                    rb.isKinematic = true;
                    var col = rb.GetComponent<Collider>();
                    if (col != null) col.enabled = false;
                    rubble.RemoveAt(i);
                }
            }
        }


        void Finish()
        {
            if (cameo != null) cameo.gameObject.SetActive(false);
            if (!broken && wall != null)
            {
                broken = true;
                foreach (Transform c in wall) c.gameObject.SetActive(false); // skipped: the wall is simply gone
            }
            if (follow != null) follow.enabled = true;
            if (input != null) input.inputEnabled = true;
            PrototypeHUD.Hidden = false;
            Playing = false;
            PlayerInputHandler.SetCursorLocked(true);
            PrototypeHUD.BigMessage(title, subtitle, 5f);
            GameAudio.PlayMusic("music_dungeon", 2f);
        }
    }
}
