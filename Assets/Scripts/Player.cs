using Unity.Cinemachine;
using UnityEngine;

public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }

    [Header("Compnent")]
    private Rigidbody rb;
    [SerializeField] private CinemachineCamera followCamera; // 시네머신 팔로우 카메라
    [SerializeField] private Transform followTarget;


    [Header("Movement Status")]
    public bool isMoveInput { get; private set; } = false; // 입력이 들어간 상태
    private Vector3 moveDir;
    private Vector3 moveForward;
    private Vector3 moveRight;


    private float speed = 5;
    private float rotationSpeed = 150;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        rb= GetComponent<Rigidbody>();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Start()
    {
        InputManager.Instance.OnMoveInput += (a, b) => OnMoveInput();
    }

    private void FixedUpdate()
    {
        Move();
    }

    private void OnMoveInput()
    {
        //if (isMoveInput) return;
        //isMoveInput = true;

        //moveForward = Vector3.ProjectOnPlane(followCamera.transform.forward, Vector3.up).normalized;
        //moveRight = Vector3.ProjectOnPlane(followCamera.transform.right, Vector3.up).normalized;
    }


    // 움직임 처리
    public void Move()
    {
        Vector2 dir = InputManager.Instance.MoveDirNormalized();
        Vector3 inputDir;

        if (dir == Vector2.zero)
        {
            moveDir = Vector2.zero;
            //isMoveInput = false;
            return;
        }

        moveForward = Vector3.ProjectOnPlane(followCamera.transform.forward, Vector3.up).normalized;
        moveRight = Vector3.ProjectOnPlane(followCamera.transform.right, Vector3.up).normalized;
        inputDir = moveRight * dir.x + moveForward * dir.y;

        rb.MovePosition(rb.position + inputDir.normalized * speed * Time.fixedDeltaTime);

        if (inputDir != Vector3.zero)
        {
            Quaternion rotation = Quaternion.LookRotation(inputDir, Vector3.up);
            followTarget.localRotation = Quaternion.Slerp(followTarget.localRotation, rotation, 15 * Time.fixedDeltaTime);
        }
    }
}
