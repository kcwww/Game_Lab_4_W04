using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using static UnityEditor.Experimental.AssetDatabaseExperimental.AssetDatabaseCounters;

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
    private const string StingAnim = "isSting";
    private const string HitAnim = "isHit";

    [Header("Movement Status")]
    public bool isMoveInput { get; private set; } = false; // 입력이 들어간 상태
    public bool isRun { get; private set; } = false;
    private Vector3 moveForward;
    private Vector3 moveRight;
    private float speed = 5;
    private float runSpeed = 5; // 합방식
    private float rotationSpeed = 15;

    [Header("Gaurd")]
    public bool isGuard { get; private set; } = false;

    [Header("Parrying")]
    private const float parryingFailDistance = 0.6f; // 패링 실패 거리
    private const float parryingRange = 4.5f; // 패링 성공 범위거리 (실패 거리도 포함했으나, 먼저 조건을 비교하므로 사실상 실거리)
    private const float parryingAnimationTimer = 0.2f; // 애니메이션 실행 속도(패링이 지속될 시간 같은 느낌)
    private const float parryingMultiTimer = 0.1f; // 패링의 중복 튕기기 가능한 시간(다중 공격)
    private const float parryingDelayTimer = 0.5f; // 패링 딜레이 타이머
    private bool isMultiTimer = false; // 다중 공격 패링 활성화 여부
    public bool parryingEmpty { get; private set; } = true; // 패링이 끝날 때까지 도달할 객체가 없는지 판단 변수
    public bool parryingSucces { get; private set; } = false;// 패링의 성공 여부 판단 변수
    public bool isParrying { get; private set; } = false; // 패링 진행 확인 변수
    public bool parryingDelay { get; private set; } = false; // 패링 딜레이 변수
    public bool isSlashParrying { get; private set; } = false; // 참격 패링
    private List<Rigidbody> enemys = new List<Rigidbody>();
    public event EventHandler CheckParringDistance; // 패링 객체들의 거리를 판단
    public event EventHandler OnParrying; // 패링이 실행될 때 같이 진행할 이벤트 목록
    public event EventHandler OnParryingEnd; // 패링 끝날 때 같이 끝낼 이벤트 목록

    [Header("Lock On")]
    public LockOnOrchestrator lockOnOrchestrator;

    [Header("Jump")]
    private const float jumpPower = 15f;
    private bool isGround = true;

    [Header("Hit")]
    [SerializeField] private GameObject hitEffect;
    public bool isHit { get; private set; } = false;

    [Header("Counter")]
    public bool isCounter = false; // 카운터 상태 체크
    public bool counterDelay = false; // 카운터 진행중 여부
    public event EventHandler OnCounter;
    private Transform enemyPos;

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
        InputManager.Instance.OnLockOnKeyboard += InputManager_OnLockOnKeyboard;
        InputManager.Instance.OnLockOnPad += InputManger_OnLockOn;
        InputManager.Instance.OnLockOffPad += InputManger_OnLockOff;
        InputManager.Instance.OnCounter += InputManager_OnCounter;
    }

    private void InputManager_OnCounter(object sender, EventArgs e)
    {
        if (isHit) return;
        if (!isCounter || counterDelay) return; // 카운터 활성화가 아니라면 return;
        counterDelay = true;
        isCounter = false;

        anim.SetTrigger(StingAnim); // 스팅 애니메이션 실행
        Vector3 dir = enemyPos.position - rb.position; // 적 방향 계산
        dir.Normalize();

        rb.AddForce(dir * 90f, ForceMode.Impulse);

        IngameManager.Instance.DamageBoss(1);
        IngameManager.Instance.CounterAttackOff(); // UI 끄기
        IngameManager.Instance.ResetTimer();
        StartCoroutine(CounterDelay());
    }

    private IEnumerator CounterDelay()
    {
        yield return new WaitForSeconds(1f);
        counterDelay = false;
    }

    private void InputManager_OnLockOnKeyboard(object sender, EventArgs e)
    {
        if (lockOnOrchestrator.isLockOn) lockOnOrchestrator.OnLockOnReleased();
        else lockOnOrchestrator.OnLockOnPressed();
    }

    private void InputManger_OnLockOff(object sender, EventArgs e)
    {
        lockOnOrchestrator.OnLockOnReleased();
    }

    private void InputManger_OnLockOn(object sender, EventArgs e)
    {
        lockOnOrchestrator.OnLockOnPressed();
    }

    private void InputManager_OnJump(object sender, EventArgs e)
    {
        if (!isGround) return;
        isGround = false;
        rb.AddForce(Vector3.up * jumpPower, ForceMode.Impulse);
    }

    // 패링 키 입력
    private void InputManager_OnParrying(object sender, EventArgs e)
    {     
        // 1. 패링 기본 조건 파악(가드 여부)
        if (!isGuard) return; // 가드중이 아니라면 return
        if (parryingDelay) return; // 패링 쿨이 안지났다면 return
        if (isParrying) return; // 패링 이미 성공 시
        if (counterDelay) return; // 반격중이면 못하게 막기
        if (isHit) return;

        StartCoroutine(ParryingDelay());

        enemys.Clear(); // 적 리스트 초기화

        parryingSucces = true; // 성공 초기화
        parryingEmpty = true; // 근처 객체 여부 초기화
        //isParrying = true; // 패링중인지 변수 초기화

        CheckParringDistance?.Invoke(this, e); // 구독 이벤트 전ㅇ달

        CheckParring(); // 패링 체크 함수

        // 패링에 성공했다면
        if(parryingSucces)
        {
            SuccessParrying(); // 활성화

            if (parryingEmpty) // 근처에 없는 경우
            {
                StartParrying();
            }
            else StartParrying();

            Debug.Log("패링 성공");
        }
        else
        {
            StartParrying();
        }
        /*else // 실패했는데
        {
            if(parryingEmpty) // 근처에 패링 객체가 없다면
            {
                SuccessParrying(); // 헛방
                StartParrying();
                Debug.Log("패링 일반");
            }
        }*/

        // 3. 하나라도 패링 시전 시간보다 이전에 도착하면 리턴
        //if (!parryingSucces) return;

    }

    private IEnumerator ParryingDelay()
    {
        parryingDelay = true;
        yield return new WaitForSeconds(parryingDelayTimer);
        parryingDelay = false;
    }

    private void InputManager_OffGuard(object sender, System.EventArgs e)
    {
        isGuard = false;
        anim.SetBool(GuardAnim, isGuard);
    }

    private void InputManager_OnGuard(object sender, System.EventArgs e)
    {
        if (isHit) return;
        //if (curGuardTimer > 0) return; // 가드 쿨타임
        if (isGuard) return;

        isGuard = true;
        //curGuardTimer = guardTimer; // 가드 타이머 초기화
        anim.SetBool(GuardAnim, isGuard);
    }

    private void FixedUpdate()
    {
        if (counterDelay) return;
        Move();
    }


    // ───────── 이동 처리(회전 포함) ─────────
    // 필요 시 함께 추가
    bool IsLockedOn(out Transform target)
    {
        target = null;
        if (lockOnOrchestrator == null || lockOnOrchestrator.selector == null) return false;
        if (!lockOnOrchestrator.selector.lockOnActive) return false;
        target = lockOnOrchestrator.selector.CurrentTarget;
        return target != null;
    }

    static Vector3 FlatNormalize(Vector3 v, Vector3 up)
    {
        v = Vector3.ProjectOnPlane(v, up);
        float m = v.magnitude;
        return (m > 1e-4f) ? (v / m) : Vector3.zero;
    }

    // ===== 교체할 Move() =====
    public void Move()
    {
        if (Time.timeScale != 1) return;

        Vector2 dir = InputManager.Instance.MoveDirNormalized();
        if (dir == Vector2.zero)
        {
            anim.SetBool(WalkAnim, false);
            return;
        }
        anim.SetBool(WalkAnim, true);

        Vector3 up = Vector3.up;

        // 1) 이동축은 항상 카메라 기준
        Vector3 camFwd = Vector3.ProjectOnPlane(followCamera.transform.forward, up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(followCamera.transform.right, up).normalized;

        Vector3 inputDir = (camRight * dir.x + camFwd * dir.y);

        // 2) 이동
        float curSpeed = isRun ? (speed + runSpeed) : speed;
        Vector3 delta = inputDir.normalized * curSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + delta);

        // 3) 회전
        if (IsLockedOn(out Transform lockTarget))
        {
            // 락온: 항상 타깃을 바라보게(Yaw)
            Vector3 faceDir = FlatNormalize(lockTarget.position - transform.position, up);
            if (faceDir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(faceDir, up);
                followTarget.localRotation = Quaternion.Slerp(
                    followTarget.localRotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
            }
        }
        else
        {
            // 비락온: 이동 방향을 바라보게
            Vector3 faceDir = FlatNormalize(inputDir, up);
            if (faceDir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(faceDir, up);
                followTarget.localRotation = Quaternion.Slerp(
                    followTarget.localRotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
            }
        }
    }

    // 패링 실패
    public void FailParrying()
    {
        parryingSucces = false;
        NonEmptyParrying(); // 근처에 패링 객체가 존재함을 의미
    }

    // 패링 시도 중 객체가 들어옴
    public void NonEmptyParrying()
    {
        parryingEmpty = false;
    }

    // 패링 성공 (패링 범위 안에 객체 존재)
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
        //isGuard = false;
        anim.SetTrigger(ParryingAnim);

        StartCoroutine(ParryingAnimation());
        // 변수를 추가해서 이거 못하게 막아보자
    }

    private IEnumerator ParryingAnimation()
    {
        yield return new WaitForSecondsRealtime(parryingAnimationTimer + 0.3f);   

        EndParrying();
    }

    // 패링 종료
    public void EndParrying()
    {
        if (parryingSucces && !parryingEmpty && !isCounter) isCounter = true; // 패링 완전 성공(패링 성공 + 주변 적 존재(공격을 진행한))
        isParrying = false;
        parryingSucces = false;
        OnParryingEnd?.Invoke(this, EventArgs.Empty);

        if(isGuard && !isCounter) anim.SetBool(GuardAnim, true); // 지금 카운터 활성화 상태가 아닌 경우에ㅇㄴ
    }


    public void SlashParrying() { isParrying = true; }
    /*// 패링 애니메이션 실행
    public void ParryingAnimation()
    {
        anim.SetTrigger(ParryingAnim);
    }*/

    //public bool GetParryingComplete => curParryingTimer >= 0f;

    // 패링에 적용된 적들
    public void AddEnemy(Rigidbody rigid)
    {
        enemys.Add(rigid);
    }

    public void CheckParring()
    {
        // 리스트 목록 점검
        foreach(var e in enemys)
        {
            // 1. 방향 및 거리 계산
            Vector3 dir = transform.position - e.position; // 방향 추출
            float distance = dir.magnitude - 1; // 보정 거리 (1은 스케일 값만큼 뺀것)

            float failDistacne = IngameManager.Instance.isCoward ? parryingFailDistance / 2 : parryingFailDistance;

            // 2. 피격 범위까지 들어온 경우 실패
            if (failDistacne >= distance)
            {
                FailParrying(); // 패링 실패
                return;
            }

            // 3. 후면인지 확인
            Vector3 toRocket = (e.position - transform.position).normalized;
            float dot = Vector3.Dot(transform.forward, toRocket);

            // 4. 패링 판정 이내에 들어온 경우
            if (parryingRange >= distance)
            {
                if (dot < 0)
                {
                    FailParrying(); // 실패
                }
                return;
            }

            // 5. 범위 밖 패링 물체 계산
            // 5-1. 속도 계산
            float currentSpeed = e.linearVelocity.magnitude; // 속도
            if (currentSpeed <= 0.01f) currentSpeed = 0.01f; // 속력이 스피드보다 작다면 거의 멈춘급으로 계산

            // 5-2. 도달 예상 시간
            float timer = distance / currentSpeed;
            Debug.Log(e.gameObject.name + " 의 도달 예상 시간 : " + timer);

            // 5-3. 판단
            // 애니메이션 실행보다 더 빨리 도착한다면 맞는 판정 (안그러면 실행 중에 패링이 될테니)
            if (parryingAnimationTimer > timer)
            {
                FailParrying();
                return;
            }

            // 5-4. 애니메이션 실행중에 도달 못한다고 판단했을 때
            // 애니메이션을 제외한 남은 거리를 계산
            float reachableDistance = distance - currentSpeed * parryingAnimationTimer;
            Debug.Log(gameObject.name + " 의 도달 남은 거리 : " + reachableDistance);
            // 5-4-1. 패링에 성공 경우
            if (reachableDistance <= parryingRange)
            {
                if (dot < 0)
                {
                    FailParrying(); // 실패
                    return;
                }
            }
            else // 헛방
            {
                if (!IngameManager.Instance.isCoward) // 겁쟁이가 아니라면 이건 진짜 패링 적용
                {
                    parryingSucces = false;
                    return;
                }
                else // 일단 겁쟁이라도 헛방임
                {
                    return;
                }
            }

            // 패링 성공이니 주변 적도 활성화
            NonEmptyParrying(); // 패링 성공이니 적도 존재함을 의미

            // 5-4-2. 아예 안맞는 경우 (헛스윙)
            //else 
            // {
            //StartParrying(); // 헛방
            //}
        }
    }

    // 피격 받음
    public void Damaged(int value)
    {
        IngameManager.Instance.DamagePlayer(value);
        hitEffect.transform.position = transform.position;
        hitEffect.transform.rotation = transform.rotation;
        hitEffect.gameObject.SetActive(true);

        StartCoroutine(OffEffect());
    }

    private IEnumerator OffEffect()
    {
        yield return new WaitForSeconds(0.5f);
        hitEffect.gameObject.SetActive(false);
    }

    public void GetEnemyPos(Transform pos)
    {
        enemyPos = pos;
    }
    
    public void Hit()
    {
        if (isHit) return;
        isHit = true;
        anim.SetTrigger(HitAnim);
        StartCoroutine(HitRoutine());
    }

    private IEnumerator HitRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        isHit = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGround = true;
        }
    }
}
