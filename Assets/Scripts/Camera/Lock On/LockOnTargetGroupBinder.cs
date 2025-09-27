using Unity.Cinemachine;
using UnityEngine;

public class LockOnTargetGroupBinder : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CinemachineTargetGroup group;
    [SerializeField] private LockOnSelector selector;
    [SerializeField] private Transform player;

    [Header("Player Member (optional)")]
    public bool keepPlayerInGroup = true;
    public float playerWeight = 1f;
    public float playerRadius = 0.5f;

    [Header("Locked Target Member")]
    public float targetWeight = 1f;
    public float targetRadius = 0.5f;

    Transform _currentBoundTarget;
    bool _playerEnsured;

    void Reset()
    {
        group = FindFirstObjectByType<CinemachineTargetGroup>();
        selector = FindFirstObjectByType<LockOnSelector>();
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
    }

    void LateUpdate()
    {
        if (!group || !selector) return;

        // 1) 플레이어 멤버 보장(옵션)
        if (keepPlayerInGroup && player && !_playerEnsured)
        {
            EnsureMember(player, playerWeight, playerRadius);
            _playerEnsured = true;
        }

        // 2) 락온 상태/타깃
        var wantTarget = selector.lockOnActive ? selector.CurrentTarget : null;

        // 같으면 가중치/반경만 동기화(재추가로 간단 동기화)
        if (wantTarget == _currentBoundTarget)
        {
            if (_currentBoundTarget)
                EnsureMember(_currentBoundTarget, targetWeight, targetRadius);
            return;
        }

        // 3) 이전 타깃 제거
        if (_currentBoundTarget)
        {
            RemoveMemberSafe(_currentBoundTarget);
            _currentBoundTarget = null;
        }

        // 4) 새 타깃 추가
        if (wantTarget)
        {
            EnsureMember(wantTarget, targetWeight, targetRadius);
            _currentBoundTarget = wantTarget;
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    void EnsureMember(Transform t, float weight, float radius)
    {
        if (!t) return;

        int idx = group.FindMember(t);
        if (idx >= 0)
        {
            // CM3에 개별 SetMemberWeight/Radius가 없으므로 재등록 방식으로 동기화
            group.RemoveMember(t);
        }
        group.AddMember(t, weight, radius);
    }

    void RemoveMemberSafe(Transform t)
    {
        if (!t) return;
        int idx = group.FindMember(t);
        if (idx >= 0) group.RemoveMember(t);
    }
}
