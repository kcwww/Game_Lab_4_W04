using UnityEngine;

public class StartTrriger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            GameManager.Instance.StartGame();
        }
    }
}
