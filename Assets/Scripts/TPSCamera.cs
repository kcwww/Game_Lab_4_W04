using Unity.Cinemachine;
using UnityEngine;

public class TPSCamera : MonoBehaviour
{
    [Header("Compenent")]
    private CinemachineCamera cam;
    private CinemachineOrbitalFollow orbital;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float zoomLerpSpeed = 10f;
    [SerializeField] private float minDistance = 3f;
    [SerializeField] private float maxDistance = 10f;
    private Vector2 scrollDelta;
    private Vector2 scrollPostion;

    private float targetZoom;
    private float currentZoom;

    void Start()
    {
        //InputManager.Instance.
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
