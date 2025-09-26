using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;


public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set;}

    public event EventHandler OnGuard;
    public event EventHandler OffGuard;
    public event EventHandler OnRun;
    public event EventHandler OffRun;
    public event EventHandler OnJump;
    public event EventHandler OnParrying; // 패링입력
    public event EventHandler OnLockOn;
    public event EventHandler OnLockOff;

    public PlayerInput playerInput {  get; private set; }
    public bool connectGamePad { get; private set; } = false;
    private Gamepad gamepad;
    private Coroutine motorCoroutine;
   
    public float cameraSensitivity = 1f;

    private void Awake()
    {
        if(Instance == null) Instance = this;

        playerInput = new PlayerInput();
        playerInput.Player.Enable();

        // performed는 조건에 만족할 때 실행(별다른 input 시스템을 설정한 게 아니므로 started와 동일)
        playerInput.Player.Guard.started += Guard_performed; // 가드 시작
        playerInput.Player.Guard.canceled += Guard_canceled; // 가드 해제(수동)

        playerInput.Player.Run.started += Run_started;
        playerInput.Player.Run.canceled += Run_canceled;

        playerInput.Player.Jump.performed += Jump_performed;

        playerInput.Player.LockOn.started += LockOn_started;
        playerInput.Player.LockOn.canceled += LockOn_canceled;

        playerInput.Player.Parrying.performed += Parrying_performed;
    }

    private void LockOn_canceled(InputAction.CallbackContext obj)
    {
        OnLockOff?.Invoke(this, EventArgs.Empty);
    }

    private void LockOn_started(InputAction.CallbackContext obj)
    {
        OnLockOn?.Invoke(this, EventArgs.Empty);
    }

    private void Jump_performed(InputAction.CallbackContext obj)
    {
        OnJump?.Invoke(this, EventArgs.Empty);
    }

    private void Run_canceled(InputAction.CallbackContext obj)
    {
        OffRun?.Invoke(this, EventArgs.Empty);
    }

    private void Run_started(InputAction.CallbackContext obj)
    {
        OnRun?.Invoke(this, EventArgs.Empty);
    }

    private void Parrying_performed(InputAction.CallbackContext obj)
    {
        OnParrying?.Invoke(this, EventArgs.Empty);
    }

    private void Guard_canceled(InputAction.CallbackContext obj)
    {
        OffGuard?.Invoke(this, EventArgs.Empty);
    }

    private void Guard_performed(InputAction.CallbackContext obj)
    {
        OnGuard?.Invoke(this, EventArgs.Empty);
    }

    private void Start()
    {
        // 컨트롤러 체크
        foreach (var device in InputSystem.devices)
        {
            if (device is Gamepad)
            {
                gamepad = device as Gamepad;
                connectGamePad = true;
                ChangeDeviceState(true);
                break;
            }
        }
    }

    private void OnEnable()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDisable()
    {
        if(gamepad != null) gamepad.SetMotorSpeeds(0, 0);

        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void OnDestroy()
    {
        //playerInput.Player.Pause.performed -= Pause_performed;
        playerInput.Player.Guard.started -= Guard_performed;
        playerInput.Player.Guard.canceled -= Guard_canceled;

        playerInput.Player.Run.started -= Run_started;
        playerInput.Player.Run.canceled -= Run_canceled;

        playerInput.Player.Jump.performed -= Jump_performed;

        playerInput.Dispose();
    }

    // 디바이스가 변동되면 호출할 함수
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is Gamepad)
        {
            switch (change)
            {
                case InputDeviceChange.Added:
                    connectGamePad = true;
                    gamepad = device as Gamepad;
                    ChangeDeviceState(true);
                    cameraSensitivity = 0.7f;
                    break;
                case InputDeviceChange.Removed:
                    connectGamePad = false;
                    gamepad = null;
                    ChangeDeviceState(false);
                    cameraSensitivity = 1f;
                    break;
            }
        }
    }

    // 패드 입력 전환

    private void ChangeDeviceState(bool isController)
    {
        connectGamePad = isController;
        //PlayerController.Instance.ChangeSensity(isController);
    }


    public Vector2 MoveDirNormalized()
    {
        Vector2 dir = playerInput.Player.Move.ReadValue<Vector2>();
        dir.Normalize();
        return dir;
    }

    // 마우스 포인터 위치 반환
    public Vector2 MousePointerDirNormalized()
    {
        return playerInput.Player.Look.ReadValue<Vector2>().normalized;
    }

    // 진동 울리기
    public void OnMotor()
    {
        if (!connectGamePad) return; // 컨트롤러 아니면 반환

        if(motorCoroutine != null) StopCoroutine(motorCoroutine);
        motorCoroutine = StartCoroutine(MotorCoroutine());
    }

    private IEnumerator MotorCoroutine()
    {
        //gamepad.SetMotorSpeeds(0.8f, 0.8f);
        AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        float t = 0f;
        float d = 0.75f;
        float lowStart = 0.5f;
        float lowEnd = 0.1f;
        float highStart = 1f;
        float highEnd = 0.4f;

        gamepad.SetMotorSpeeds(lowStart, highStart);

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / d);       // 0→1로 진행률 계산
            float k = Mathf.Clamp01(curve.Evaluate(u)); // ease 곡선 적용
            //float low = Mathf.Lerp(lowStart, lowEnd, k);
            //float high = Mathf.Lerp(highStart, highEnd, k);
            float low = Mathf.Lerp(lowEnd, lowStart, k);
            float high = Mathf.Lerp(highEnd, highStart, k);
            gamepad.SetMotorSpeeds(low, high);
            yield return null;
        }

        gamepad.SetMotorSpeeds(0, 0);
    }
}

