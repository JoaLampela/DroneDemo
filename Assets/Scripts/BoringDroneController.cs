using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody), typeof(PlayerInput))]
public class BoringDroneController : MonoBehaviour
{
    [Header("Actions")]
    [SerializeField] private InputActionReference _moveAction;
    [SerializeField] private InputActionReference _lookAction;
    [SerializeField] private InputActionReference _jumpAction;
    
    [Header("Propeller Transforms")]
    [SerializeField] private Transform _propellerFR;
    [SerializeField] private Transform _propellerFL;
    [SerializeField] private Transform _propellerBR;
    [SerializeField] private Transform _propellerBL;
    
    [Header("Settings")]
    [SerializeField] private float _movementSpeed = 5f;
    [SerializeField] private float _mouseSensitivity = 0.1f;
    [SerializeField] private float _gamepadSensitivity = 180f;
    [SerializeField] private float _liftForce = 15f;
    [SerializeField] private float _propellerSpeed = 1000f;

    [Header("Damping & Limits")]
    [SerializeField] private float _linearDamping = 0.8f;
    [SerializeField] private float _angularDamping = 2.0f;
    [SerializeField] private float _maxSpeed = 12f;
    [SerializeField] private float _maxFallSpeed = 12f;
    
    private InputAction _move;
    private InputAction _look;
    private InputAction _jump;
    private Rigidbody _rb;
    private PlayerInput _playerInput;
    private GameObject _cam;
    private Vector2 _moveInput;
    private Vector2 _gamepadLookRate;
    private float _yaw;
    private float _pitch;
    private float _hoverInput;
    private uint _score;
    
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _playerInput = GetComponent<PlayerInput>();
        _cam = GetComponentInChildren<Camera>(includeInactive: true).gameObject.transform.parent.gameObject;
        
        InputActionAsset inputActions = _playerInput.actions;
        _move = inputActions.FindAction(_moveAction.action.id);
        _look = inputActions.FindAction(_lookAction.action.id);
        _jump = inputActions.FindAction(_jumpAction.action.id);
        
        _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;

        _rb.linearDamping  = _linearDamping;
        _rb.angularDamping = _angularDamping;

        _score = 0;
    }
    
    private void OnEnable()
    {
        _move.performed += OnMovePerformed;
        _move.canceled  += OnMoveCanceled;
        _look.performed += OnLookPerformed;
        _look.canceled  += OnLookCanceled;
        _jump.performed += OnJumpPerformed;
        _jump.canceled  += OnJumpCanceled;
    }

    private void Update()
    {
        if (_gamepadLookRate != Vector2.zero)
        {
            _yaw   += _gamepadLookRate.x * Time.deltaTime;
            _pitch  = Mathf.Clamp(_pitch - _gamepadLookRate.y * Time.deltaTime, -90f, 90f);
        }

        RotatePropellers();
    }
    
    private void FixedUpdate()
    {
        _rb.angularVelocity = Vector3.zero;
        _rb.MoveRotation(Quaternion.Euler(0f, _yaw, 0f));
        _cam.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        
        Vector3 translation = transform.right * _moveInput.x + transform.forward * _moveInput.y;
        _rb.MovePosition(_rb.position + translation * (_movementSpeed * Time.fixedDeltaTime));

        if (_hoverInput > 0f)
        {
            Vector3 v = _rb.linearVelocity;
            if (v.y < -3f) v.y = -3f;
            _rb.linearVelocity = v;
        }
        _rb.AddForce(Vector3.up * (_liftForce * _hoverInput), ForceMode.Acceleration);

        ClampVelocity();
    }

    private void ClampVelocity()
    {
        Vector3 velocity = _rb.linearVelocity;
        float max = _maxSpeed;
        
        if (velocity.sqrMagnitude > max * max) velocity = velocity.normalized * max;
        if (velocity.y < -_maxFallSpeed) velocity.y = -_maxFallSpeed;

        _rb.linearVelocity = velocity;
    }
    
    private void OnDisable()
    {
        _move.performed -= OnMovePerformed;
        _move.canceled  -= OnMoveCanceled;
        _look.performed -= OnLookPerformed;
        _look.canceled  -= OnLookCanceled;
        _jump.performed -= OnJumpPerformed;
    }
    
    private void OnCollisionEnter(Collision other) => HandleCollision(other.gameObject);
    private void OnTriggerEnter(Collider other) => HandleCollision(other.gameObject);
    
    private void HandleCollision(GameObject other)
    {
        switch (other.transform.tag)
        {
            case "Ring":
                _score++;
                Destroy(other);
                break;
            case "Goal":
                _score++;
                Debug.Log($"You win! Score: {_score}");
                Destroy(other);
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
                break;
            case "Platform":
                _score = 0;
                Debug.Log("Back to platform: Score reset to 0.");
                break;
            default:
                Debug.Log($"You lose! Try again! Score: {_score}");
                _score = 0;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                break;
        }
    }
    
    private void RotatePropellers()
    {
        if (_propellerFR) _propellerFR.Rotate(Vector3.up, _propellerSpeed * Time.deltaTime, Space.Self);
        if (_propellerFL) _propellerFL.Rotate(Vector3.up, _propellerSpeed * Time.deltaTime, Space.Self);
        if (_propellerBR) _propellerBR.Rotate(Vector3.up, _propellerSpeed * Time.deltaTime, Space.Self);
        if (_propellerBL) _propellerBL.Rotate(Vector3.up, _propellerSpeed * Time.deltaTime, Space.Self);
    }
    
    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        Vector2 value = context.ReadValue<Vector2>();
        _moveInput = value.magnitude <= 1f ? value : value.normalized;
    }
    
    private void OnMoveCanceled(InputAction.CallbackContext context) => _moveInput = Vector2.zero;
    
    private void OnLookPerformed(InputAction.CallbackContext context)
    {
        Vector2 value = context.ReadValue<Vector2>();

        switch (context.control?.device)
        {
            case Mouse:
                _yaw   += value.x * _mouseSensitivity;
                _pitch  = Mathf.Clamp(_pitch - value.y * _mouseSensitivity, -90f, 90f);
                break;
            case Gamepad:
                _gamepadLookRate = value * _gamepadSensitivity;
                break;
            default:
                Debug.Log($"Unknown controller device: {context.control?.device}");
                break;
        }
    }
    
    private void OnLookCanceled(InputAction.CallbackContext context) => _gamepadLookRate = Vector2.zero;

    private void OnJumpPerformed(InputAction.CallbackContext context) => _hoverInput = context.ReadValue<float>();
    private void OnJumpCanceled (InputAction.CallbackContext context) => _hoverInput = 0f;
}
