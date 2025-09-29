using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public bool isTutorial { get; private set; } = true;
    public bool isStart { get; private set; } = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    // 튜토리얼을 이미 진행
    public void NotTutorial()
    {
        isTutorial = false;
    }

    public void StartGame()
    {
        isStart = true;
    }

}
