using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class TPSCamera : MonoBehaviour
{
    [Header("Compenent")]
    private CinemachineCamera cam;
    private CinemachineOrbitalFollow orbital;
    private CinemachineInputAxisController inputAxis;
    [SerializeField] private LayerMask obstacleLayers; // 장애물 layer

    private const float defaultGain = 1.25f;
    private float currentGain = 1.25f; // 현재 회전 속도
    private const float transeperencyDistance = 5f; // 투명화할 객체까지의 거리

    private List<Renderer> currentObstructions = new List<Renderer>();
    private List<Renderer> previousObstructions = new List<Renderer>();

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
        // 1. 거리 계싼
        Vector3 dir = Player.Instance.transform.position - transform.position;
        float distance = dir.magnitude;

        // 이번 프레임의 가려진 오브젝트 목록 초기화
        currentObstructions.Clear();

        // 레이캐스트 전체 체크
        RaycastHit[] hits = Physics.RaycastAll(transform.position, dir, distance, obstacleLayers);
        Debug.DrawLine(transform.position, dir * 15, Color.green);
        foreach (RaycastHit hit in hits)
        {
            Renderer rend = hit.collider.GetComponent<Renderer>();
            if (rend != null)
            {
                currentObstructions.Add(rend);
                SetAlpha(rend, 0.3f); // 투명 처리
            }
        }

        // 이전에 가려졌는데 지금은 안 가려진 오브젝트 복원
        foreach (Renderer rend in previousObstructions)
        {
            if (!currentObstructions.Contains(rend))
            {
                SetAlpha(rend, 1f);
            }
        }

        // 리스트 업데이트
        previousObstructions.Clear();
        previousObstructions.AddRange(currentObstructions);
    }

    private void SetAlpha(Renderer rend, float alpha)
    {
        foreach (Material mat in rend.materials)
        {
            mat.SetFloat("_Mode", 3); // Transparent 모드
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 1);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;

            Color c = mat.color;
            c.a = alpha;
            mat.color = c;
        }
    }

    public void OnRotation()
    {
        inputAxis.enabled = true;
    }

    public void OffRotation()
    {
        inputAxis.enabled = false;
    }
}