using System.Collections;
using UnityEngine;

public class Boss : MonoBehaviour
{
    public static Boss Instance { get; private set; }

    [Header("Compenent")]
    private Animator anim;
    private Rigidbody rb;
    private Transform target;

    [Header("Const")]
    private const string WalkAnim = "isWalk";
    private const string HorizontalAnim = "isHorizontal";
    private const string SmashAnim = "isSmash";

    [Header("Parrying")]
    public bool isParrying { get; private set; } = false; // 플레이어 패링 성공 여부

    [Header("Attack")]
    private float attackRange = 10f; // 공격 탐지 거리
    private float curAttackTimer = 5f; // 현재 공격 쿨타임
    private float attackTimer = 5f; // 공격 쿨타임
    private bool isAttack = false; // 공격 중인지 체크

    private float horizontalSpeed = 15f; // 가로베기 이동 속도
    private bool isHorizontal = false; // 발도 체크

    [Header("Status")]
    private float speed = 5f;
    private float rotationSpeed = 5f;

    private void Awake()
    {
        if(Instance == null) Instance = this;

        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        target = Player.Instance.transform;
    }

    private void FixedUpdate()
    {
        Move();
        Attack();
    }

    private void Update()
    {
        if(!isAttack)
        {
            curAttackTimer -= Time.deltaTime;
        }
    }

    private void Move()
    {
        if (isAttack) return; // 공격중 실행 x
        if (Time.timeScale != 1) return;

        // 1. 방향 계산
        Vector3 dir = (target.position - transform.position);

        if (dir.magnitude <= attackRange) return; // 공격 전환

        dir = dir.normalized;

        // 2. 이동
        anim.SetBool(WalkAnim, true);
        rb.MovePosition(rb.position + dir * speed * Time.fixedDeltaTime);

        // 3. 회전
        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
    }

    private void Attack()
    {
        if (isAttack) return;
        if (curAttackTimer > 0) return;
        isAttack = true; // 공격 진행


        // 나중에 공격 패턴 만들어서 더 넣기
        HorizontalSmash();

        curAttackTimer = attackTimer;
    }

    // 가로베기
    private void HorizontalSmash()
    {
        anim.SetBool(HorizontalAnim, true); // 발도 준비

        StartCoroutine(SmashCoroutine());
    }

    private IEnumerator SmashCoroutine()
    {
        // 1. 발도 대기
        yield return new WaitForSeconds(0.3f);

        // 2. 가로 베기 텍스트 및 이펙트 실행

        yield return new WaitForSeconds(0.5f);

        anim.SetBool(HorizontalAnim, false);

        // 3. 방향 계산
        Vector3 dir = (target.position - transform.position).normalized;
        Vector3 startPos = rb.position;
        Vector3 endPos = target.position - dir*2; // 2 정도의 거리만큼 뒤에 도착

        // 4. 방향 고정 및 애니메이션 실행
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        anim.SetTrigger(SmashAnim);

        float dashDuration = 0.5f; // 대시 시간
        float elapsed = 0f;

        while (elapsed < dashDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = elapsed / dashDuration;
            Vector3 newPos = Vector3.Lerp(startPos, endPos, t);
            rb.MovePosition(newPos);

            yield return new WaitForFixedUpdate();
        }

        // 마지막 위치 보정
        rb.MovePosition(endPos);

        // 4. 대시 끝
        isAttack = false;

        yield return null;
    }

}
