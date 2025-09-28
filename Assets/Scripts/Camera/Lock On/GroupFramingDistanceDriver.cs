using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// 플레이어-락온 타깃의 (수평)거리 d에 따라 CinemachineGroupFraming.FramingSize를 가변 적용.
/// - 락온 중에만 동작(Selector 상태 사용)
/// - 거리→사이즈 매핑: min/max + AnimationCurve로 튜닝
/// - 부드러운 감쇠 지원
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
public class GroupFramingDistanceDriver : MonoBehaviour
{
    [Header("Refs")]
    public LockOnSelector selector;                   // 현재 락온 타깃 확인
    public Transform player;                          // 플레이어 위치
    public CinemachineTargetGroup targetGroup;        // (선택) 있으면 중심 계산에 사용

    [Header("Cinemachine")]
    [Tooltip("동일 오브젝트의 CinemachineGroupFraming (없으면 자동 탐색)")]
    public CinemachineGroupFraming groupFraming;

    [Header("Distance → Framing Size Mapping")]
    [Tooltip("이 거리 이하일 때를 '가까움'으로 간주")]
    public float nearDistance = 3f;
    [Tooltip("이 거리 이상일 때를 '멀다'로 간주")]
    public float farDistance = 20f;

    [Tooltip("가까울 때 목표 FramingSize")]
    public float sizeAtNear = 0.2f;
    [Tooltip("멀 때 목표 FramingSize")]
    public float sizeAtFar = 0.8f;

    [Tooltip("0~1 입력(t)에 대한 보정 커브. x: 정규화 거리, y: 가중치")]
    public AnimationCurve distanceCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Smoothing")]
    [Tooltip("0이면 즉시, >0이면 지수 감쇠(초)")]
    public float sizeDamping = 0.12f;

    float _curSize;        // 감쇠용 캐시
    bool _inited;

    void Reset()
    {
        var vcam = GetComponent<CinemachineCamera>();
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
#if UNITY_2022_3_OR_NEWER
        if (!groupFraming) groupFraming = GetComponent<CinemachineGroupFraming>();
#endif
        // 기본 커브
        if (distanceCurve == null || distanceCurve.length < 2)
            distanceCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    }

    void LateUpdate()
    {
        if (!selector || !player) return;

        // 락온 중일 때만
        if (!selector.lockOnActive) return;
        var target = selector.CurrentTarget;
        if (!target) return;

        // 수평 거리 d
        Vector3 a = player.position; a.y = 0f;
        Vector3 b = target.position; b.y = 0f;
        

        float d = Vector3.Distance(a, b);
        Debug.Log($"[GroupFramingDistanceDriver] d={d}");
        

        // 0~1 정규화
        float t = Mathf.InverseLerp(nearDistance, farDistance, d);
        t = Mathf.Clamp01(t);
        t = distanceCurve.Evaluate(t);

        // 목표 사이즈
        float desired = Mathf.Lerp(sizeAtNear, sizeAtFar, t);

        // 감쇠
        if (sizeDamping > 0f)
        {
            if (!_inited) { _curSize = desired; _inited = true; }
            float k = 1f - Mathf.Exp(-Time.deltaTime / sizeDamping);
            _curSize = Mathf.Lerp(_curSize, desired, k);
        }
        else
        {
            _curSize = desired;
            _inited = true;
        }

        // 적용
        if (groupFraming)
        {
            groupFraming.FramingSize = _curSize;
            Debug.Log($"[GroupFramingDistanceDriver] size={_curSize}" );
        }
        else
        {
            Debug.LogWarning($"[GroupFramingDistanceDriver] CinemachineGroupFraming 컴포넌트가 없습니다.");
        }
        // 만약 컴포넌트명이 다르거나 다른 버전을 쓰면:
        // - CM3의 동일 컴포넌트가 붙어있는지 확인
        // - 또는 Position/Rotation Composer 조합을 쓰는 세팅이면 이 스크립트를 꺼두세요.
    }
}
