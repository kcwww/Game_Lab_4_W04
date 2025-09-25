using System;
using UnityEngine;
using UnityEngine.InputSystem;


public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set;}

    public event EventHandler OnGuard;
    public event EventHandler OffGuard;
    public event EventHandler OnParrying; // 패링입력

    public PlayerInput playerInput {  get; private set; }
    public bool connectGamePad { get; private set; } = false;

    public float cameraSensitivity = 1f;

    private void Awake()
    {
        if(Instance == null) Instance = this;

        playerInput = new PlayerInput();
        playerInput.Player.Enable();

        // performed는 조건에 만족할 때 실행(별다른 input 시스템을 설정한 게 아니므로 started와 동일)
        playerInput.Player.Guard.started += Guard_performed; // 가드 시작
        playerInput.Player.Guard.canceled += Guard_canceled; // 가드 해제(수동)

        playerInput.Player.Parrying.performed += Parrying_performed;
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
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void OnDestroy()
    {
        //playerInput.Player.Pause.performed -= Pause_performed;
        playerInput.Player.Guard.started -= Guard_performed;
        playerInput.Player.Guard.canceled -= Guard_canceled;

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
                    ChangeDeviceState(true);
                    cameraSensitivity = 0.7f;
                    break;
                case InputDeviceChange.Removed:
                    connectGamePad = false;
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
}

