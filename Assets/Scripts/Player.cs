using Unity.Cinemachine;
using UnityEngine;

public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }

    [Header("Compnent")]
    private Rigidbody rb;
    [SerializeField] private CinemachineCamera followCamera; // 시네머신 팔로우 카메라
    [SerializeField] private Transform followTarget;


    private float speed = 5;
    private float rotationSpeed = 150;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        rb= GetComponent<Rigidbody>();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void FixedUpdate()
    {
        Move();
        
    }
    private void LateUpdate()
    {
        Rotation();
    }

    // 움직임 처리
    public void Move()
    {

        /*// 1. 마우스 기준으로 캐릭터를 회전
        transform.rotation *= Quaternion.AngleAxis(InputManager.Instance.MousePointerDirNormalized().x * rotationSpeed, Vector3.up);

        // 2. 카메라 타겟 회전 (수직과 수평 기준)
        followTarget.rotation *= Quaternion.AngleAxis(InputManager.Instance.MousePointerDirNormalized().x * rotationSpeed, Vector3.up);
        followTarget.rotation *= Quaternion.AngleAxis(InputManager.Instance.MousePointerDirNormalized().y * rotationSpeed, Vector3.right);

        // 3. 회전각 계산
        var angles = followTarget.localEulerAngles;
        angles.z = 0; // z축 회전은 배제

        var angle = followTarget.transform.localEulerAngles.x; // 왜 x?

        // 최대 높이 각 계산 (땅 밑 꺼짐 방지)
        if (angle > 180 && angle < 340) angles.x = 340;
        else if (angle < 180 && angle > 40) angles.x = 40;

        followTarget.localEulerAngles = angles;

        var nextRotation = Quaternion.Lerp(followTarget.rotation, nextRotation,)
        
        
        Vector2 dir = InputManager.Instance.MoveDirNormalized();
       
        // 가만히 있는 상태
        if(dir == Vector2.zero) 
        {

        }
        else
        {
            Vector3 moveDir = cam.right * dir.x + cam.forward * dir.y; // 카메라 기준인지, 플레이어 기준인지를 나중에 파악해서 변경
            //Vector3 moveDir = new Vector3(dir.x, 0, dir.y); // 임시

            rb.MovePosition(rb.position + moveDir * speed * Time.fixedDeltaTime);

            //Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            //rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));




        }*/

        Vector2 dir = InputManager.Instance.MoveDirNormalized();
        Vector3 moveDir = followCamera.transform.right * dir.x + followCamera.transform.forward * dir.y;

        //Vector3 moveDir = new Vector3(dir.x, 0, dir.y); // 임시

        rb.MovePosition(rb.position + moveDir * speed * Time.fixedDeltaTime);
    }

    // 회전 처리
    public void Rotation()
    {
        Vector2 inputDir = InputManager.Instance.MousePointerDirNormalized();

        // 1. 수평 처리
        followTarget.Rotate(Vector3.up * inputDir.x * rotationSpeed * Time.deltaTime);

        // 2. 수직 회전
        followTarget.Rotate(Vector3.right * -inputDir.y * rotationSpeed * Time.deltaTime, Space.Self);

        var angles = followTarget.localEulerAngles;
        angles.z = 0; // z축 회전은 배제

        var angle = followTarget.transform.localEulerAngles.x;

        // 최대 높이 각 계산 (땅 밑 꺼짐 방지)
        if (angle > 180 && angle < 340) angles.x = 340;
        else if (angle < 180 && angle > 40) angles.x = 40;

        followTarget.localEulerAngles = angles; // 회전 제한 적용

        // 에이밍 로직 배제
    }
}
