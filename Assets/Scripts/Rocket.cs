using UnityEngine;

public class Rocket : MonoBehaviour, IParrying
{
    private Rigidbody rb;
    private float speed = 7;
    private Transform target;

    private float curParryingTimer = 0f;
    private float parryingTimer = 2f; // 패링 적용 타이머
    private bool isParryingDamage = false;
    private Vector3 scaleVector; // 자신의 스케일 
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        scaleVector = new Vector3(0, 0, transform.localScale.z/2);
    }

    private void Start()
    {
        Player.Instance.CheckParringDistance += Player_OnParrying;
        Player.Instance.OnParryingEnd += Player_EndParrying;

        target = Player.Instance.transform;
    }

    private void OnDisable()
    {
        Player.Instance.CheckParringDistance -= Player_OnParrying;
        Player.Instance.OnParryingEnd -= Player_EndParrying;
    }

    private void Player_EndParrying(object sender, System.EventArgs e)
    {
        isParryingDamage = false;
    }

    private void FixedUpdate()
    {
        if (isParryingDamage) return; // 로켓은 패링중에는 넉백움직임 처리할 것임

        Vector3 dir = target.position - rb.position;

        rb.MovePosition(rb.position + dir.normalized * speed * Time.fixedDeltaTime);

        Quaternion rotate = Quaternion.LookRotation(dir, Vector3.up);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, rotate, speed * Time.fixedDeltaTime));
    }

    // 패링 거리 체크
    private void Player_OnParrying(object sender, Player.ParryingEventArgs e)
    {
        // 1. 현재 거리를 계산(현재 scale 때문에 안닫는 부분을 방지하기 위한 변수)
        Vector3 dir = target.position - rb.position - scaleVector;
        float distance = dir.magnitude;

        // 2. 피격 범위까지 들어온 경우
        if(e.parryingFailDistance >= distance)
        {
            Player.Instance.FailParrying(); // 패링 실패
            return;
        }

        Vector3 toRocket = (rb.position - target.position).normalized;
        float dot = Vector3.Dot(Player.Instance.followTarget.forward, toRocket);

        // 3. 패링 판정 이내에 들어온 경우
        if (e.parryingRange >= distance)
        {
            // dot > 0 : 앞쪽 / dot < 0 : 뒤쪽
            if (dot > 0f)
            {
                Player.Instance.SuccessParrying(); // 앞쪽에 있을 때만 성공
            }
            else
            {
                Player.Instance.FailParrying(); // 뒤쪽이면 실패
            }

            return;
        }

        // 4. 범위 밖 패링 물체 계산
        // 4-1. 속도 계산
        float currentSpeed = rb.linearVelocity.magnitude; // 속도
        if (currentSpeed <= 0.01f) currentSpeed = 0.01f; // 속력이 스피드보다 작다면 거의 멈춘급으로 계산

        // 4-2. 도달 예상 시간
        float timer = distance / currentSpeed;
        Debug.Log(gameObject.name + " 의 도달 예상 시간 : " + timer);

        // 4-3. 판단
        // 애니메이션 실행보다 더 빨리 도착한다면 맞는 판정 (안그러면 실행 중에 패링이 될테니)
        if(e.parryingAnimationTimer > timer)
        {
            Player.Instance.FailParrying();
            return;
        }

        // 4-4. 애니메이션 실행중에 도달 못한다고 판단했을 때
        // 애니메이션을 제외한 남은 거리를 계산
        float reachableDistance = distance - currentSpeed * e.parryingAnimationTimer;
        Debug.Log(gameObject.name + " 의 도달 남은 거리 : " + reachableDistance);
        // 4-4-1. 패링에 성공 경우
        if (reachableDistance < e.parryingRange)
        {
            if (dot > 0f)
            {
                Player.Instance.SuccessParrying(); // 앞쪽에 있을 때만 성공
            }
            else
            {
                Player.Instance.FailParrying(); // 뒤쪽이면 실패
            }
        }
        // 4-4-2. 아예 안맞는 경우 (헛스윙)
        else
        {
            Player.Instance.StartParrying(); // 헛방
        }
    }


    public void ParryingDamage()
    {
        if (isParryingDamage) return;

        Debug.Log("AI 패링 진행");

        //Player.Instance.ParryingAnimation();

        PostProcessingManager.Instance.PulseDefault();
        isParryingDamage = true;
        curParryingTimer = parryingTimer;

        rb.AddForce((rb.position - target.position).normalized * 15, ForceMode.Impulse);
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (Player.Instance.parryingSucces) // 판정 성공일때만 진행
            {
                Player.Instance.StartParrying();
                ParryingDamage();
            }
            else Debug.Log("패링 실패!!!!");
        }
    }
}
