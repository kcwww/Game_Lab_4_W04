using UnityEngine;

public class TutorialDoor : MonoBehaviour
{
    public Animator animator;

    public Slime slime;
    public Transform pivot; // 돌아갈 위치



    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            slime.ResetPosition(pivot);
            GameManager.Instance.NotTutorial();
            animator.SetTrigger("isRotate");
        }
    }


}
