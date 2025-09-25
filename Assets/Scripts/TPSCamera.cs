using Unity.Cinemachine;
using UnityEngine;

public class TPSCamera : MonoBehaviour
{
    [Header("Compenent")]
    private CinemachineCamera cam;
    private CinemachineOrbitalFollow orbital;
    private CinemachineInputAxisController inputAxis;

    private const float defaultGain = 1.25f;
    private float currentGain = 1.25f; // 현재 회전 속도

    private void Awake()
    {
        cam = GetComponent<CinemachineCamera>();
        orbital = GetComponent<CinemachineOrbitalFollow>();
        inputAxis = GetComponent<CinemachineInputAxisController>();
    }

    private void Start()
    {
        Player.Instance.OnParrying += Player_OnParrying;
        Player.Instance.OnParryingEnd += Player_OnParryingEnd;
    }

    private void Player_OnParryingEnd(object sender, System.EventArgs e)
    {
        OnRotation();
    }

    private void Player_OnParrying(object sender, System.EventArgs e)
    {
        OffRotation();
    }

    private void Update()
    {

    }

    public void OnRotation()
    {
        inputAxis.enabled = true
            
            
            ;
    }

    public void OffRotation()
    {
        inputAxis.enabled = false;
    }
}