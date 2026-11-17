using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FracturedVow
{
    /// <summary>
    /// Trailer mode (console: "trailer"). A cinematic tour of the current floor for recording a trailer with
    /// OBS or the Xbox Game Bar (Win+Alt+R): an establishing crane shot, a torch-lit corridor, an orbit around
    /// husks, the merchant, the boss and a close-up of the knight, with title cards over each shot.
    /// The HUD is hidden and the knight is invulnerable while it plays. Space / Enter skips.
    /// </summary>
    public class TrailerDirector : MonoBehaviour
    {
        static TrailerDirector inst;
        Text card;
        CanvasGroup cardGroup;

        public static void Play()
        {
            if (inst == null)
            {
                var go = new GameObject("Trailer Director");
                inst = go.AddComponent<TrailerDirector>();
                inst.BuildUI();
            }
            inst.StopAllCoroutines();
            inst.StartCoroutine(inst.Run());
        }

        void BuildUI()
        {
            var root = UIKit.Canvas("Trailer Cards", 45, transform);
            var r = UIKit.Rect("Card", root);
            UIKit.Stretch(r);
            cardGroup = r.gameObject.AddComponent<CanvasGroup>();
            cardGroup.alpha = 0f;
            cardGroup.blocksRaycasts = false;
            card = UIKit.Label(r, "", 84, UIKit.Gold, TextAnchor.MiddleCenter);
            UIKit.Stretch((RectTransform)card.transform);
            card.supportRichText = true;
            var sh = card.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.95f);
            sh.effectDistance = new Vector2(4f, -4f);
        }

        IEnumerator Run()
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc == null) yield break;
            var hp = pc.GetComponent<Health>();
            bool god = hp != null && hp.godMode;
            if (hp != null) hp.godMode = true;
            bool floor2 = PlayerProgress.SceneName.Contains("Floor2");

            Vector3 p = pc.transform.position, f = pc.transform.forward, up = Vector3.up;
            var shots = new List<Cinema.Shot>();
            var cards = new List<(float at, float len, string text)>();
            float t = 0f;
            void Shot(Vector3 cf, Vector3 ct, Vector3 lf, Vector3 lt, float sec, string text = null)
            {
                shots.Add(new Cinema.Shot { camFrom = cf, camTo = ct, lookFrom = lf, lookTo = lt, seconds = sec });
                if (text != null) cards.Add((t + 0.6f, sec - 1.2f, text));
                t += sec;
            }

            // 1. establishing: rising over the knight
            Shot(p + up * 3f - f * 6f, p + up * 9f - f * 10f, p + up * 1.5f, p + f * 10f, 5f, "SOME VOWS ARE BROKEN");
            // 2. a torch-lit stretch of dungeon
            var torch = Pick(FindObjectsByType<Light>(FindObjectsSortMode.None), l => l.type == LightType.Point && l.color.r > l.color.b, p, 12f, 40f);
            if (torch != null)
            {
                Vector3 c = torch.transform.position, side = Vector3.Cross(up, (c - p).normalized);
                Shot(c - up * 1.2f + side * 4f, c - up * 1.2f - side * 4f, c, c, 5f, "SOME ARE BURIED");
            }
            // 3. husks: a slow orbit
            var foe = Pick(FindObjectsByType<EnemyAI>(FindObjectsSortMode.None), e => e.elite, p, 6f, 60f) ??
                      Pick(FindObjectsByType<EnemyAI>(FindObjectsSortMode.None), e => true, p, 6f, 60f);
            if (foe != null)
            {
                Vector3 c = foe.transform.position + up * 1.2f;
                Shot(c + new Vector3(4f, 1.5f, 0f), c + new Vector3(0f, 1f, 4f), c, c, 5f, "FIGHT THE FORGOTTEN");
                Shot(c + new Vector3(0f, 1f, 4f), c + new Vector3(-4f, 0.6f, 0f), c, c, 4f);
            }
            // 4. the merchant / a sellsword
            var shop = (MonoBehaviour)FindFirstObjectByType<ShopNPC>() ?? FindFirstObjectByType<MercenaryPost>();
            if (shop != null)
            {
                Vector3 c = shop.transform.position + up * 1.5f, fw = shop.transform.forward;
                Shot(c + fw * 4f + up * 0.5f, c + fw * 2.4f + up * 0.2f, c, c, 4.5f, "ARM YOURSELF.  RECRUIT ALLIES.");
            }
            // 5. the boss
            Transform boss = floor2 ? (FindFirstObjectByType<LeviathanBoss>()?.transform) : (FindFirstObjectByType<WardenBoss>()?.transform);
            if (boss != null)
            {
                Vector3 c = boss.position + up * (floor2 ? 2f : 3f);
                Vector3 dir = (p - boss.position); dir.y = 0f; dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
                Shot(c + dir * 14f + up * 0.5f, c + dir * 8f - up * 0.6f, c, c + up * 1f, 5f, floor2 ? "WAKE THE LEVIATHAN" : "FACE THE WARDEN");
            }
            // 6. the knight
            Vector3 k = p + up * 1.6f, right = Vector3.Cross(up, f);
            Shot(k + f * 2.2f + right * 1.2f, k + f * 1.6f - right * 0.8f, k, k, 4.5f, "A KNIGHT WITHOUT A NAME");
            // 7. title
            Shot(k - f * 2f + up * 0.5f, k - f * 7f + up * 4f, k, k + f * 8f, 6f, "<size=130>FRACTURED VOW</size>\n<size=40>AVAILABLE NOW</size>");

            GameAudio.PlayMusic("music_boss", 1f);
            StartCoroutine(Cards(cards));
            yield return Cinema.Play(shots);
            cardGroup.alpha = 0f;
            if (hp != null) hp.godMode = god;
            GameAudio.PlayMusic("music_dungeon", 2f);
        }

        IEnumerator Cards(List<(float at, float len, string text)> cards)
        {
            float start = Time.time;
            foreach (var c in cards)
            {
                while (Time.time - start < c.at) { if (!Cinema.Running) yield break; yield return null; }
                card.text = c.text;
                for (float x = 0f; x < c.len; x += Time.deltaTime)
                {
                    if (!Cinema.Running) { cardGroup.alpha = 0f; yield break; }
                    float a = Mathf.Min(1f, x / 0.6f, (c.len - x) / 0.6f);
                    cardGroup.alpha = a;
                    card.rectTransform.localScale = Vector3.one * (1f + x * 0.01f);
                    yield return null;
                }
                cardGroup.alpha = 0f;
            }
        }

        static T Pick<T>(T[] list, System.Func<T, bool> ok, Vector3 from, float min, float max) where T : Component
        {
            T best = null;
            float bestD = float.MaxValue;
            foreach (var x in list)
            {
                if (x == null || !ok(x)) continue;
                float d = (x.transform.position - from).magnitude;
                if (d < min || d > max) continue;
                if (d < bestD) { bestD = d; best = x; }
            }
            return best;
        }
    }
}
