using UnityEngine;

public class RingRotationController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float _rotationSpeed;
    
    private Transform _tf;

    private void Awake()
    {
        _tf = transform;
    }

    private void Update()
    {
        _tf.Rotate(Vector3.forward, _rotationSpeed * Time.deltaTime);
    }
}
