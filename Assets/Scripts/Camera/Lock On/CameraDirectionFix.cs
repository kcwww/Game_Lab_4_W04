using UnityEngine;
using Unity.Cinemachine;

public class CameraDirectionFix : MonoBehaviour
{
    [Header("Camera Reference")]
    [SerializeField] private CinemachineCamera cinemachineCamera;

    [Header("Player Reference")]
    [SerializeField] private Transform playerTransform;

    [Header("Settings")]
    [SerializeField] private float transitionSpeed = 3f; // (현재 예제에선 미사용)

    private CinemachineOrbitalFollow orbitalFollow;
    private CinemachineInputAxisController axisController; // 있으면 입력 잠금에 사용
    private bool isPressing = false;

    // 락 시점의 축 값 캐시
    private float lockedYaw;
    private float lockedPitch; // 필요하면 유지용 (여기선 그대로 보존만)

    void Start()
    {
        if (cinemachineCamera == null)
            cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();

        if (playerTransform == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player) playerTransform = player.transform;
        }

        if (cinemachineCamera != null)
        {
            orbitalFollow = cinemachineCamera.GetComponent<CinemachineOrbitalFollow>();
            axisController = cinemachineCamera.GetComponent<CinemachineInputAxisController>();
            if (orbitalFollow == null)
                Debug.LogError("CinemachineOrbitalFollow component not found on the camera!");
        }
    }

    void Update()
    {
        HandleInput();
    }

    void LateUpdate()
    {
        // Cinemachine 갱신 이후 최종 덮어쓰기 위해 LateUpdate 사용
        if (isPressing && orbitalFollow != null)
        {
            // 수평(yaw)만 고정
            orbitalFollow.HorizontalAxis.Value = lockedYaw;

            // Y 좌표/피치를 유지하고 싶으면 수직축도 캐시로 유지
            // 필요 없다면 아래 라인은 주석 처리 가능
            orbitalFollow.VerticalAxis.Value = lockedPitch;
        }
    }

    void HandleInput()
    {
        if (orbitalFollow == null || playerTransform == null) return;

        if (Input.GetKeyDown(KeyCode.Q))
        {
            // 1) 플레이어 바라보는 방향으로 수평축 스냅
            float targetYaw = GetPlayerYaw();
            orbitalFollow.HorizontalAxis.Value = targetYaw;

            // 2) 현재 축 값을 캐시하고 잠금 시작
            BeginLock();
        }
        else if (Input.GetKeyUp(KeyCode.Q))
        {
            EndLock();
        }
    }

    float GetPlayerYaw()
    {
        Vector3 fwd = playerTransform.forward;
        return Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
    }

    void BeginLock()
    {
        isPressing = true;

        // 현재 값을 캐시해서 그 값을 유지
        lockedYaw = orbitalFollow.HorizontalAxis.Value;
        lockedPitch = orbitalFollow.VerticalAxis.Value; // Y/pitch는 그대로

        // 입력 컨트롤러가 있으면 비활성화 → 사용자 입력으로 축이 바뀌는 것 방지
        if (axisController != null) axisController.enabled = false;

        // OrbitalFollow 자체는 유지 (비활성화하면 타겟 추적/오프셋 갱신까지 멈출 수 있음)
    }

    void EndLock()
    {
        isPressing = false;
        if (axisController != null) axisController.enabled = true;
    }
}
