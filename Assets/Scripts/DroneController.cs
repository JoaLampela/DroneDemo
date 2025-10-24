using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody), typeof(PlayerInput))]
public class DroneController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text scoreText;
    
    [Header("Input Actions")]
    [SerializeField] private InputActionReference _liftAction;
    [SerializeField] private InputActionReference _swayAction;
    [SerializeField] private InputActionReference _lookAction;
    
    [Header("Propeller Transforms")]
    [SerializeField] private Transform _propellerFR;
    [SerializeField] private Transform _propellerFL;
    [SerializeField] private Transform _propellerBR;
    [SerializeField] private Transform _propellerBL;
    
    [Header("Settings")]
    [SerializeField] private float _thrustForce = 15f;
    [SerializeField] private float _swayForce = 5f;
    [SerializeField] private float _yawSpeed = 60f;
    [SerializeField] private float _pitchSpeed = 30f;
    [SerializeField] private float _propellerRotationSpeed = 1000f;
    [SerializeField] private float _cameraLerpSpeed = 5f;
    [SerializeField] private float _maxSpeed = 12f;
    [SerializeField] private float _maxFallSpeed = 12f;
    [SerializeField] private float _liftRampPerSec = 6f;
    [SerializeField] private float _linearDamping = 0.8f;
    [SerializeField] private float _angularDamping = 2.0f;
    private float _liftEffective; 
    
    private float _accFR;
    private float _accFL;
    private float _accBR;
    private float _accBL;
    
    private float _spinFR;
    private float _spinFL;
    private float _spinBR;
    private float _spinBL;
    
    private InputAction _lift;
    private InputAction _sway;
    private InputAction _look;
    private Rigidbody _rb;
    private PlayerInput _playerInput;
    private Camera _cam;
    private Transform _cameraPivot;
    private float _liftInput;
    private Vector2 _swayInput;
    private Vector2 _lookInput;
    private uint _score;
    
    private void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        _rb = GetComponent<Rigidbody>();
        _playerInput = GetComponent<PlayerInput>();
        _cam = Camera.main!;
        _cameraPivot = _cam.transform.parent;
        
        _lift = _playerInput.actions.FindAction(_liftAction.action.id);
        _sway = _playerInput.actions.FindAction(_swayAction.action.id);
        _look = _playerInput.actions.FindAction(_lookAction.action.id);
        
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.useGravity = true;
        _score = 0;
        _rb.linearDamping = _linearDamping;
        _rb.angularDamping = _angularDamping;
    }

    private void OnEnable()
    {
        _lift.performed += OnLiftPerformed;
        _lift.canceled += OnLiftCanceled;
        _sway.performed += OnSwayPerformed;
        _sway.canceled += OnSwayCanceled;
        _look.performed += OnLookPerformed;
        _look.canceled += OnLookCanceled;
    }
    
    private void OnDisable()
    {
        _lift.performed -= OnLiftPerformed;
        _lift.canceled -= OnLiftCanceled;
        _sway.performed -= OnSwayPerformed;
        _sway.canceled -= OnSwayCanceled;
        _look.performed -= OnLookPerformed;
        _look.canceled -= OnLookCanceled;
    }
    
    private void OnLiftPerformed(InputAction.CallbackContext context)
    {
        _liftInput = context.ReadValue<float>();
    }
    
    private void OnLiftCanceled(InputAction.CallbackContext context) => _liftInput = 0f;
    
    private void OnSwayPerformed(InputAction.CallbackContext context)
    {
        _swayInput = context.ReadValue<Vector2>();
    }
    
    private void OnSwayCanceled(InputAction.CallbackContext context) => _swayInput = Vector2.zero;
    
    private void OnLookPerformed(InputAction.CallbackContext context)
    {
        _lookInput = context.ReadValue<Vector2>();
    }
    
    private void OnLookCanceled(InputAction.CallbackContext context) => _lookInput = Vector2.zero;

    private void Update()
    {
        RotatePropellers();
        HandleCameraPitch();
        UpdateScoreText();
    }

    private void FixedUpdate()
    {
        HandleLift();
        HandleSway();
        ClampVelocity();
    }
    
    private void ClampVelocity()
    {
        if (_rb.linearVelocity.sqrMagnitude > _maxSpeed * _maxSpeed)
            _rb.linearVelocity = _rb.linearVelocity.normalized * _maxSpeed;
        
        if (_rb.linearVelocity.y < -_maxFallSpeed)
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, -_maxFallSpeed, _rb.linearVelocity.z);
    }
    
    private void OnCollisionEnter(Collision other)
    {
        HandleCollision(other.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleCollision(other.gameObject);
    }

    private void UpdateScoreText()
    {
        scoreText.text = $"Score: {_score.ToString()}";
    }
    
    private void HandleLift()
    {
        _liftEffective = Mathf.MoveTowards(
            _liftEffective, 
            _liftInput, 
            _liftRampPerSec * Time.fixedDeltaTime
        );
        
        _rb.AddForce(transform.up * (_liftEffective * _thrustForce), ForceMode.Acceleration);
        
        if (_liftInput > 0f) _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, Mathf.Max(_rb.linearVelocity.y, -3f), _rb.linearVelocity.z);
    }
    
    private void HandleSway()
    {
        _accFR = 0f;
        _accFL = 0f;
        _accBR = 0f;
        _accBL = 0f;
        
        if (Mathf.Abs(_swayInput.y) > 0.001f)
        {
            float acc = Mathf.Abs(_swayInput.y) * _swayForce;

            if (_swayInput.y > 0f)
            {
                _accBR += acc;
                _accBL += acc;
            }
            else
            {
                _accFR += acc;
                _accFL += acc; 
            }
        }
        
        if (Mathf.Abs(_swayInput.x) > 0.001f)
        {
            float acc = Mathf.Abs(_swayInput.x) * _swayForce;

            if (_swayInput.x < 0f)
            {
                _accFR += acc;
                _accBR += acc;
            }
            else
            {
                _accFL += acc;
                _accBL += acc;
            }
        }
        
        if (_accFR > 0f) _rb.AddForceAtPosition(transform.up * _accFR, _propellerFR.position, ForceMode.Acceleration);
        if (_accFL > 0f) _rb.AddForceAtPosition(transform.up * _accFL, _propellerFL.position, ForceMode.Acceleration);
        if (_accBR > 0f) _rb.AddForceAtPosition(transform.up * _accBR, _propellerBR.position, ForceMode.Acceleration);
        if (_accBL > 0f) _rb.AddForceAtPosition(transform.up * _accBL, _propellerBL.position, ForceMode.Acceleration);
    }
    
    private void HandleCameraPitch()
    {
        Vector3 eulerAngle = _cameraPivot.localEulerAngles;
        
        if (eulerAngle.x > 180f) eulerAngle.x -= 360f;
        if (eulerAngle.y > 180f) eulerAngle.y -= 360f;
        
        float targetPitch = Mathf.Clamp(eulerAngle.x - (_lookInput.y * _pitchSpeed * Time.deltaTime), -45f, 45f);
        float targetYaw = eulerAngle.y + (_lookInput.x * _yawSpeed * Time.deltaTime);
        
        Quaternion targetRot = Quaternion.Euler(targetPitch, targetYaw, 0f);
        
        _cameraPivot.localRotation = Quaternion.Slerp(
            _cameraPivot.localRotation,
            targetRot,
            _cameraLerpSpeed * Time.deltaTime
        );
    }
    
    private void RotatePropellers()
    {
        float norm = Mathf.Max(1f, _thrustForce / 4f + _swayForce);
        float iFR = Mathf.Clamp01(_accFR / norm);
        float iFL = Mathf.Clamp01(_accFL / norm);
        float iBR = Mathf.Clamp01(_accBR / norm);
        float iBL = Mathf.Clamp01(_accBL / norm);
        
        float t = 1f - Mathf.Exp(-10f * Time.deltaTime);
        _spinFR = Mathf.Lerp(_spinFR, Mathf.Lerp(200f, _propellerRotationSpeed, iFR), t);
        _spinFL = Mathf.Lerp(_spinFL, Mathf.Lerp(200f, _propellerRotationSpeed, iFL), t);
        _spinBR = Mathf.Lerp(_spinBR, Mathf.Lerp(200f, _propellerRotationSpeed, iBR), t);
        _spinBL = Mathf.Lerp(_spinBL, Mathf.Lerp(200f, _propellerRotationSpeed, iBL), t);
        
        _propellerFR.Rotate(Vector3.up, _spinFR * Time.deltaTime, Space.Self);
        _propellerFL.Rotate(Vector3.up, _spinFL * Time.deltaTime, Space.Self);
        _propellerBR.Rotate(Vector3.up, _spinBR * Time.deltaTime, Space.Self);
        _propellerBL.Rotate(Vector3.up, _spinBL * Time.deltaTime, Space.Self);
    }

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
}
