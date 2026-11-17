using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FracturedVow
{
    /// <summary>End of the floor. Shows the clear message and loads the next scene if one is set and in the build list.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class FloorExit : MonoBehaviour
    {
        public string title = "FLOOR 1 CLEARED";
        [TextArea] public string body = "The stair winds upward toward the light.\nMore memories wait above.";
        public string nextSceneName = "";
        public float loadDelay = 4f;
        bool done;

        void Awake() { GetComponent<BoxCollider>().isTrigger = true; }

        void OnTriggerEnter(Collider other)
        {
            if (done || other.GetComponentInParent<PlayerController>() == null) return;
            done = true;
            GameAudio.PlayUI("victory_sting");
            StartCoroutine(ShowCleared());
        }

        IEnumerator ShowCleared()
        {
            yield return new WaitForSeconds(1.2f);
            bool hasNext = !string.IsNullOrEmpty(nextSceneName) && Application.CanStreamedLevelBeLoaded(nextSceneName);
            if (hasNext) SaveSystem.Save(nextSceneName);
            else SaveSystem.Delete(); // the game is finished: no run to continue
            GameMenu.ShowFloorCleared(title, body, nextSceneName);
        }
    }
}
