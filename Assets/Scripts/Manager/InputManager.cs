using System;
using UnityEngine;
using UnityEngine.InputSystem;


public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set;}

    public event EventHandler OnAttack;

    public PlayerInput playerInput {  get; private set; }
    public bool connectGamePad { get; private set; } = false;

    public float cameraSensitivity = 1f;

    private void Awake()
    {
        if(Instance == null) Instance = this;

        playerInput = new PlayerInput();
        playerInput.Player.Enable();

        //playerInput.Player.Pause.performed += Pause_performed;
    }

    private void Pause_performed(InputAction.CallbackContext obj)
    {
       // OnPause?.Invoke(this, EventArgs.Empty);
    }

    private void Attack_performed(InputAction.CallbackContext obj)
    {
        OnAttack?.Invoke(this, EventArgs.Empty);
    }

    private void Start()
    {
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

        playerInput.Dispose();
    }

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

    /*public Vector2 CameraDirNormalized()
    {
        return playerInput.Player.Look.ReadValue<Vector2>().normalized;
    }*/
}

