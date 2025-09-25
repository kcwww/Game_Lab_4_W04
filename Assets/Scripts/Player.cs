using System;
using Unity.Cinemachine;
using UnityEngine;

public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }

    [Header("Compnent")]
    private Rigidbody rb;
    private Animator anim;
    [SerializeField] private CinemachineCamera followCamera; // 시네머신 팔로우 카메라
    [field: SerializeField] public Transform followTarget { get; private set; }
    [SerializeField] private GameObject parryingBoxTrigger; // 패링 박스 콜라이더

    [Header("Const")]
    private const string WalkAnim = "isWalk";
    private const string GuardAnim = "isGuard";
    private const string ParryingAnim = "isParrying";

    [Header("Movement Status")]
    public bool isMoveInput { get; private set; } = false; // 입력이 들어간 상태
    public bool isRun { get; private set; } = false;
    private Vector3 moveForward;
    private Vector3 moveRight;
    private float speed = 5;
    private float runSpeed = 5; // 합방식
    private float rotationSpeed = 15;

    [Header("Gaurd")]
    private float curGuardTimer = 0f; // 현재 가드 타이머
    private const float guardTimer = 1f; // 1초의 딜레이
    public bool isGuard { get; private set; } = false;

    [Header("Parrying")]
    private const float parryingWaitTimer = 0.35f; // 패링을 시도할 때까지 걸리는 시간 (이 시간보다 빠르게 도착하면 피격)
    private float curParryingTimer = 0; // 패링 중 반격을 진행할 시간
    private const float parryingTimer = 0.15f; // 패링 중 반격을 진행할 시간, (스케일이 다 달라서 보정이 들어간 시간이 좋을듯)
    public bool parryingSucces { get; private set; } = true;// 패링의 성공 여부 판단 변수
    public bool isParrying { get; private set; } = false; // 패링 진행 확인 변수
    public event EventHandler<float> OnParrying; // 주변 객체들의 정보를 담을 이벤트
    public event EventHandler OnParryEnd; // 패링 끝날 때

    private void Awake()
    {
        if (Instance == null) Instance = this;

        rb= GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Start()
    {
        InputManager.Instance.OnGuard += InputManager_OnGuard;
        InputManager.Instance.OffGuard += InputManager_OffGuard;
        InputManager.Instance.OnParrying += InputManager_OnParrying;
        InputManager.Instance.OnRun += (a, b) => isRun = true;
        InputManager.Instance.OffRun += (a, b) => isRun = false;
    }

    private void InputManager_OnParrying(object sender, System.EventArgs e)
    {     
        // 1. 패링 기본 조건 파악(가드 여부)
        if (!isGuard) return; // 가드중이 아니라면 return

        parryingSucces = true;

        // 2. 패링 물체 확인
        OnParrying?.Invoke(this, parryingWaitTimer); // 주변 충돌 객체들의 패링 참고

        // 3. 하나라도 패링 시전 시간보다 이전에 도착하면 리턴
        if (!parryingSucces) return; // 패링을 실패하면 리턴

        // 4. 조건을 만족하면 패링 실행
        isParrying = true; // 패링 활성화
        curParryingTimer = parryingTimer; // 패링 타이머 초기화
        parryingBoxTrigger.gameObject.SetActive(true);
        ParryingAnimation();

        anim.SetBool(GuardAnim, false);
        isGuard = false;
    }

    private void InputManager_OffGuard(object sender, System.EventArgs e)
    {
        isGuard = false;
        anim.SetBool(GuardAnim, isGuard);
    }

    private void InputManager_OnGuard(object sender, System.EventArgs e)
    {
        if (curGuardTimer > 0) return; // 가드 쿨타임

        isGuard = true;
        curGuardTimer = guardTimer; // 가드 타이머 초기화
        anim.SetBool(GuardAnim, isGuard);
    }

    private void FixedUpdate()
    {
        Move();
    }

    private void Update()
    {
        if (!isGuard) curGuardTimer -= Time.deltaTime; // 가드가 아닐 때 타이머 계산

        // 패링 시간 체크
        if (isParrying)
        {
            curParryingTimer -= Time.deltaTime; // 패링 지속 타이머 실행

            if(curParryingTimer < 0) // 패링 비활성화
            {
                isParrying = false;
                parryingSucces = false;
                parryingBoxTrigger.SetActive(false);
                OnParryEnd?.Invoke(this, EventArgs.Empty);
            }
        }
    }


    // 움직임 처리(회전도 포함)
    public void Move()
    {
        if (Time.timeScale != 1) return;

        Vector2 dir = InputManager.Instance.MoveDirNormalized();
        Vector3 inputDir;

        if (dir == Vector2.zero)
        {
            anim.SetBool(WalkAnim, false);
            return;
        }
        else anim.SetBool(WalkAnim, true);


        moveForward = Vector3.ProjectOnPlane(followCamera.transform.forward, Vector3.up).normalized;
        moveRight = Vector3.ProjectOnPlane(followCamera.transform.right, Vector3.up).normalized;
        inputDir = moveRight * dir.x + moveForward * dir.y;

        float curSpeed = speed;
        if (isRun) curSpeed += runSpeed;

        rb.MovePosition(rb.position + inputDir.normalized * curSpeed * Time.fixedDeltaTime);

        if (inputDir != Vector3.zero)
        {
            Quaternion rotation = Quaternion.LookRotation(inputDir, Vector3.up);
            followTarget.localRotation = Quaternion.Slerp(followTarget.localRotation, rotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    // 패링 실패
    public void FailParrying()
    {
        parryingSucces = false;
    }

    public void ParryingAnimation()
    {
        anim.SetTrigger(ParryingAnim);
    }

    //public bool GetParryingComplete => curParryingTimer >= 0f;
}
