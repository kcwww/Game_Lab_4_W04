using UnityEngine;

/// <summary>
/// 각 적(또는 락온 가능한 오브젝트)에 붙는 메타데이터.
/// - Arrow: 자식에 배치된 인디케이터(초기 비활성 권장)
/// - aimPoint: 조준/프레이밍 기준(없으면 transform)
/// - priorityBias: 보스/엘리트 가산점
/// - isLockable: 사망/기절 등으로 잠시 비활성화 가능
/// </summary>
public class LockableTarget : MonoBehaviour
{
    [Header("Indicator")]
    public GameObject arrow;
    [Tooltip("조준 기준 포인트(머리/가슴). 비우면 이 오브젝트의 Transform")]
    public Transform aimPoint;
    [Tooltip("선택 우선순위 가산(보스/엘리트 등)")]
    [Range(0f, 1f)] public float priorityBias = 0f;
    [Tooltip("잠시 락온 대상 제외할 때 false")]
    public bool isLockable = true;

    // 색상 적용(렌더러 or 스프라이트 모두 대응)
    Renderer[] _renderers;
    SpriteRenderer[] _spriteRenderers;
    MaterialPropertyBlock _mpb;

    void Awake()
    {
        if (arrow != null)
        {
            _renderers = arrow.GetComponentsInChildren<Renderer>(true);
            _spriteRenderers = arrow.GetComponentsInChildren<SpriteRenderer>(true);
            _mpb = new MaterialPropertyBlock();
        }
    }

    public Transform AimPointOrSelf() => aimPoint != null ? aimPoint : transform;

    public void SetArrowActive(bool on)
    {
        if (arrow != null && arrow.activeSelf != on)
            arrow.SetActive(on);
    }

    public void SetArrowColor(Color c)
    {
        if (arrow == null) return;

        if (_renderers != null)
        {
            foreach (var r in _renderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", c);       // URP/Lit, Unlit 기준
                _mpb.SetColor("_Color", c);           // 표준 셰이더 호환
                r.SetPropertyBlock(_mpb);
            }
        }
        if (_spriteRenderers != null)
        {
            foreach (var sr in _spriteRenderers)
            {
                if (!sr) continue;
                sr.color = c;
            }
        }
    }
}
