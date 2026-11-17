using System.Collections;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// A stone platform in the Leviathan's lake. When the Leviathan slams it, it cracks (glowing red, shaking)
    /// for a few seconds, then collapses and sinks: anyone still standing on it ends up in the water.
    /// Its dock points switch off once it's gone, so the boat can't land there. Restored when the knight respawns.
    /// </summary>
    public class BreakablePlatform : MonoBehaviour
    {
        public Transform[] dockPoints;
        public GameObject cracks;
        [Tooltip("Objects that sit on the platform (its brazier fire) and sink with it.")]
        public Transform[] riders;
        public float sinkDepth = 7f;
        public float sinkTime = 2.5f;

        public bool IsBroken { get; private set; }
        public bool Breaking { get; private set; }

        Vector3 startPos;
        Quaternion startRot;
        Vector3[] riderStart;
        float shake;

        void Awake()
        {
            startPos = transform.position;
            startRot = transform.rotation;
            if (riders != null)
            {
                riderStart = new Vector3[riders.Length];
                for (int i = 0; i < riders.Length; i++) if (riders[i] != null) riderStart[i] = riders[i].position;
            }
        }

        void OnEnable() { GameManager.Respawned += Restore; }
        void OnDisable() { GameManager.Respawned -= Restore; }

        /// <summary>True if a point (the knight's feet) is on top of this platform.</summary>
        public bool IsStandingOn(Vector3 p)
        {
            if (IsBroken) return false;
            Vector3 local = p - transform.position;
            return Mathf.Abs(local.x) < 6.2f && Mathf.Abs(local.z) < 6.2f && p.y > -0.3f && p.y < 3f;
        }

        public void BeginBreak(float warning)
        {
            if (IsBroken || Breaking) return;
            Breaking = true;
            StartCoroutine(Warn(warning));
        }

        IEnumerator Warn(float warning)
        {
            if (cracks != null) cracks.SetActive(true);
            GameAudio.Play("platform_crack", transform.position, 1f, 0.02f);
            for (float t = 0f; t < warning; t += Time.deltaTime)
            {
                shake = Mathf.Lerp(0.02f, 0.09f, t / warning);
                yield return null;
            }
        }

        public void Collapse()
        {
            if (IsBroken) return;
            StopAllCoroutines();
            Breaking = false;
            IsBroken = true;
            shake = 0f;
            if (dockPoints != null) foreach (var d in dockPoints) if (d != null) d.gameObject.SetActive(false);
            GameAudio.Play("wall_break", transform.position, 1f, 0.05f);
            GameFX.Dust(transform.position + Vector3.up * 0.5f, 5f, 60);
            CameraShake.Shake(0.4f, 0.8f);
            StartCoroutine(Sink());
        }

        IEnumerator Sink()
        {
            Vector3 from = transform.position;
            Quaternion fromRot = transform.rotation;
            Quaternion tilt = fromRot * Quaternion.Euler(Random.Range(-14f, 14f), 0f, Random.Range(-14f, 14f));
            for (float t = 0f; t < 1f; t += Time.deltaTime / sinkTime)
            {
                float k = t * t;
                Move(from + Vector3.down * sinkDepth * k, Quaternion.Slerp(fromRot, tilt, t));
                yield return null;
            }
            Move(from + Vector3.down * sinkDepth, tilt);
            GameAudio.Play("splash", from, 0.9f);
        }

        void Move(Vector3 pos, Quaternion rot)
        {
            Vector3 delta = pos - transform.position;
            transform.SetPositionAndRotation(pos, rot);
            if (riders != null) foreach (var r in riders) if (r != null) r.position += delta;
        }

        public void Restore()
        {
            StopAllCoroutines();
            IsBroken = false; Breaking = false; shake = 0f;
            transform.SetPositionAndRotation(startPos, startRot);
            if (riders != null)
                for (int i = 0; i < riders.Length; i++) if (riders[i] != null) riders[i].position = riderStart[i];
            if (cracks != null) cracks.SetActive(false);
            if (dockPoints != null) foreach (var d in dockPoints) if (d != null) d.gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            if (!Breaking || shake <= 0f) return;
            // a small tremor on the platform (cosmetic)
            transform.position = new Vector3(startPos.x + Random.Range(-shake, shake), startPos.y, startPos.z + Random.Range(-shake, shake));
        }
    }
}
