using Unity.Cinemachine;
using UnityEngine;

public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }

    [Header("Compnent")]
    private Rigidbody rb;
    private Animator anim;
    [SerializeField] private CinemachineCamera followCamera; // 시네머신 팔로우 카메라
    [SerializeField] private Transform followTarget;

    [Header("Const")]
    private const string WalkAnim = "isWalk";
    private const string GuardAnim = "isGuard";
    private const string ParryingAnim = "isParrying";

    [Header("Movement Status")]
    public bool isMoveInput { get; private set; } = false; // 입력이 들어간 상태
    private Vector3 moveForward;
    private Vector3 moveRight;
    private float speed = 5;
    private float rotationSpeed = 15;

    [Header("Gaurd")]
    public bool isGuard { get; private set; } = false;

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
    }

    private void InputManager_OnParrying(object sender, System.EventArgs e)
    {
        // 여기서 적의 공격을 파악해서 조건부로 실행을 정해야함
        if (!isGuard) return; // 가드중이 아니라면 return
        anim.SetBool(GuardAnim, false);
        isGuard = false;
        anim.SetTrigger(ParryingAnim);
    }

    private void InputManager_OffGuard(object sender, System.EventArgs e)
    {
        isGuard = false;
        anim.SetBool(GuardAnim, isGuard);
    }

    private void InputManager_OnGuard(object sender, System.EventArgs e)
    {
        isGuard = true;
        anim.SetBool(GuardAnim, isGuard);
    }

    private void FixedUpdate()
    {
        Move();
    }


    // 움직임 처리(회전도 포함)
    public void Move()
    {
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


        rb.MovePosition(rb.position + inputDir.normalized * speed * Time.fixedDeltaTime);

        if (inputDir != Vector3.zero)
        {
            Quaternion rotation = Quaternion.LookRotation(inputDir, Vector3.up);
            followTarget.localRotation = Quaternion.Slerp(followTarget.localRotation, rotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }
}
