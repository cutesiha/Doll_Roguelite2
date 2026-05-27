using UnityEngine;
using UnityEngine.SceneManagement;

public class DoorTrigger : MonoBehaviour
{
    [SerializeField] string mapSceneName = "MapScene";

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        MapRunState.CompletePendingRoom();
        SceneManager.LoadScene(mapSceneName);
    }
}
