using UnityEngine;

public class TutorialDoor : MonoBehaviour
{
    public Animator animator;


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            animator.SetTrigger("isRotate");
            GameManager.Instance.NotTutorial();
        }
    }
}
