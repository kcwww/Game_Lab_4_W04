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
    [Tooltip("락온 카메라 사용 중에도 이 스냅-잠금을 유지할지")]
    [SerializeField] private bool keepWhileLockOn = false;

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

    // 폴링 제거 — Update() / HandleInput() 삭제

    void LateUpdate()
    {
        // Cinemachine 갱신 이후 최종 덮어쓰기
        if (isPressing && orbitalFollow != null)
        {
            orbitalFollow.HorizontalAxis.Value = lockedYaw;
            orbitalFollow.VerticalAxis.Value = lockedPitch; // 필요 없다면 주석 처리 가능
        }
    }

    /// <summary>입력 매니저의 'LockOn 눌림' 이벤트에서 호출</summary>
    public void OnLockOnDirection()
    {
        if (orbitalFollow == null || playerTransform == null) return;

        // 플레이어 바라보는 방향으로 즉시 스냅(수평만)
        float targetYaw = GetPlayerYaw();
        orbitalFollow.HorizontalAxis.Value = targetYaw;

        // 잠금 시작(축 값 유지)
        BeginLock();
    }

    /// <summary>입력 매니저의 'LockOn 해제' 이벤트에서 호출</summary>
    public void OnLockOffDirection()
    {
        EndLock();
    }

    float GetPlayerYaw()
    {
        Vector3 fwd = playerTransform.forward;
        return Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
    }

    void BeginLock()
    {
        // 락온 카메라를 사용할 땐 이 스냅-잠금을 끄고 싶다면 옵션 체크
        if (!keepWhileLockOn)
        {
            // 여기서 별도 상태 플래그를 보고 early-return 할 수도 있음
        }

        isPressing = true;

        // 현재 값을 캐시해서 그 값을 유지
        lockedYaw = orbitalFollow.HorizontalAxis.Value;
        lockedPitch = orbitalFollow.VerticalAxis.Value; // Y/pitch 유지(불필요하면 제거)

        // 사용자 입력 축 무력화(있을 때만)
        if (axisController != null) axisController.enabled = false;
    }

    void EndLock()
    {
        isPressing = false;

        // 입력 축 복구
        if (axisController != null) axisController.enabled = true;
    }
}
