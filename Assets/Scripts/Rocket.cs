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

    private void Player_OnParrying(object sender, float e)
    {
        // 1. 현재 거리를 계산
        Vector3 dir = target.position - rb.position - scaleVector;
        float distance = dir.magnitude;

        // 2. 현재 목적지까지의 도달 시간을 계산
        float currentSpeed = rb.linearVelocity.magnitude; // 속도
        //if (currentSpeed < 1f) currentSpeed = 1f;
        if (currentSpeed <= speed) currentSpeed = speed; // 속력이 아직 없을 때 스피드로 대체

        // 3. 도달 예상 시간
        float timer = distance / currentSpeed;
        Debug.Log(gameObject.name + " 의 도달 예상 시간 : " + timer);

        // 3. 해당 시간을 비교하여 패링 조건 갱신
        if (e > timer) // 패링 판정시간보다 빠르게 도착한다면 실패처리 
        {
            Player.Instance.FailParrying();
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
            if (Player.Instance.parryingSucces) // 판정 성공
            {
                ParryingDamage();
            }
            else Debug.Log("패링 실패!!!!");
        }
    }
}
