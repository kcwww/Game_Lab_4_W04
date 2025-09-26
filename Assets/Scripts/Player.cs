using System;
using System.Collections;
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
    //[field: SerializeField] public ParryingPivot parryingBoxTrigger { get; private set; } // 패링 박스 콜라이더

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
    //private float curGuardTimer = 0f; // 현재 가드 타이머
    //private const float guardTimer = 0.5f; // 1초의 딜레이
    public bool isGuard { get; private set; } = false;

    [Header("Parrying")]
    private const float parryingFailDistance = 0.6f; // 패링 실패 거리
    private const float parryingRange = 1f; // 패링 성공 범위거리 (실패 거리도 포함했으나, 먼저 조건을 비교하므로 사실상 실거리)
    private const float parryingAnimationTimer = 0.2f; // 애니메이션 실행 속도(패링이 지속될 시간 같은 느낌)
    private const float parryingMultiTimer = 0.1f; // 패링의 중복 튕기기 가능한 시간(다중 공격)
    private bool isMultiTimer = false; // 다중 공격 패링 활성화 여부
    public bool parryingEmpty { get; private set; } = true; // 패링이 끝날 때까지 도달할 객체가 없는지 판단 변수
    public bool parryingSucces { get; private set; } = false;// 패링의 성공 여부 판단 변수
    public bool isParrying { get; private set; } = false; // 패링 진행 확인 변수
    public event EventHandler<ParryingEventArgs> CheckParringDistance; // 패링 객체들의 거리를 판단
    public event EventHandler OnParrying; // 패링이 실행될 때 같이 진행할 이벤트 목록
    public event EventHandler OnParryingEnd; // 패링 끝날 때 같이 끝낼 이벤트 목록


    public class ParryingEventArgs : EventArgs
    {
        public float parryingFailDistance;
        public float parryingRange;
        public float parryingAnimationTimer;
    }

    [Header("Jump")]
    private const float jumpPower = 15f;
    private bool isGround = false;

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
        InputManager.Instance.OnJump += InputManager_OnJump;
    }

    private void InputManager_OnJump(object sender, EventArgs e)
    {
        if (!isGround) return;
        isGround = false;
        rb.AddForce(Vector3.up * jumpPower, ForceMode.Impulse);
    }

    // 패링 키 입력
    private void InputManager_OnParrying(object sender, System.EventArgs e)
    {     
        // 1. 패링 기본 조건 파악(가드 여부)
        if (!isGuard) return; // 가드중이 아니라면 return

        parryingSucces = true; // 성공 초기화
        parryingEmpty = true; // 근처 객체 여부 초기화

        // 2. 패링 물체 확인
        CheckParringDistance?.Invoke(this, new ParryingEventArgs
        { 
            parryingFailDistance = parryingFailDistance,
            parryingRange = parryingRange,
            parryingAnimationTimer = parryingAnimationTimer,
        }); // 주변 충돌 객체들의 패링 참고

        // 3. 하나라도 패링 시전 시간보다 이전에 도착하면 리턴
        if (!parryingSucces) return;

    }

    private void InputManager_OffGuard(object sender, System.EventArgs e)
    {
        isGuard = false;
        anim.SetBool(GuardAnim, isGuard);
    }

    private void InputManager_OnGuard(object sender, System.EventArgs e)
    {
        //if (curGuardTimer > 0) return; // 가드 쿨타임
        if (isGuard) return;

        isGuard = true;
        //curGuardTimer = guardTimer; // 가드 타이머 초기화
        anim.SetBool(GuardAnim, isGuard);
    }

    private void FixedUpdate()
    {
        Move();
    }

    private void Update()
    {
        //if (!isGuard) curGuardTimer -= Time.deltaTime; // 가드가 아닐 때 타이머 계산

        // 패링 시간 체크
        /*if (isParrying)
        {
            curParryingTimer -= Time.deltaTime; // 패링 지속 타이머 실행

            if(curParryingTimer < 0) // 패링 비활성화
            {
                isParrying = false;
                parryingSucces = false;
                parryingBoxTrigger.SetActive(false);
                OnParryingEnd?.Invoke(this, EventArgs.Empty);
            }
        }*/
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

    // 패링 시도 중 객체가 들어옴
    public void NonEmptyParrying()
    {
        parryingEmpty = false;
    }

    // 패링 성공 (패링 범위 안에 객체 존재), 객체가 멈춰있으면 치명적인 오류임
    public void SuccessParrying()
    {
        isParrying = true; // 패링 활성화
        //isMultiTimer = true; // 패링 중복 감지 활성화 (변수를 하나 더 둬서 막아야할듯)

        //StartCoroutine(ParryingAnimation());
    }

    // 패링을 진행
    public void StartParrying()
    {
        // 1. 패링 구독 함수 진행
        OnParrying?.Invoke(this, EventArgs.Empty);

        anim.SetBool(GuardAnim, false);
        isGuard = false;
        anim.SetTrigger(ParryingAnim);

        StartCoroutine(ParryingAnimation());
        // 변수를 추가해서 이거 못하게 막아보자
    }

    private IEnumerator ParryingAnimation()
    {
        yield return new WaitForSeconds(parryingAnimationTimer);

        EndParrying();
    }

    // 패링 종료
    public void EndParrying()
    {
        isParrying = false;
        parryingSucces = false;
        OnParryingEnd?.Invoke(this, EventArgs.Empty);
    }

    /*// 패링 애니메이션 실행
    public void ParryingAnimation()
    {
        anim.SetTrigger(ParryingAnim);
    }*/

    //public bool GetParryingComplete => curParryingTimer >= 0f;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGround = true;
        }
    }
}
