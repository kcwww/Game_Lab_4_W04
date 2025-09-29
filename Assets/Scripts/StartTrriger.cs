using UnityEngine;

public class StartTrriger : MonoBehaviour
{
    bool isfisrt = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (isfisrt) return;
            isfisrt = true;
            GameManager.Instance.StartGame();
        }
    }
}
