using UnityEngine;

public class Boss : MonoBehaviour
{
    public static Boss Instance { get; private set; }

    //[Header("Compenent")]
    //private 

    [Header("Parrying")]
    public bool isParrying { get; private set; } = false; // 플레이어 패링 성공 여부

    private void Awake()
    {
        if(Instance == null) Instance = this;
    }
}
