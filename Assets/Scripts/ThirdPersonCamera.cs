using UnityEngine;
using UnityEngine.InputSystem;

// Local third-person orbit camera. Holds a fixed distance behind the target and lets the
// player swing the angle with the mouse. Camera is pure local presentation: it only runs
// for the local player's own character and has no networked state. Run direction and camera
// angle are independent -- looking around does not steer the runner.
//
// Attach this to your Main Camera. The target (the local player's character) is assigned at
// runtime by the player when it spawns with input authority (see PlayerCameraTarget below).
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform _target;         // the character to follow
    [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 1.5f, 0f); // look at head height, not feet

    [Header("Distance & framing")]
    [SerializeField] private float _distance = 6f;       // fixed follow distance
    [SerializeField] private float _followLerp = 12f;    // how quickly the pivot catches the target

    [Header("Mouse orbit")]
    [SerializeField] private float _mouseSensitivity = 0.15f;
    [SerializeField] private float _minPitch = -20f;     // how far down you can look
    [SerializeField] private float _maxPitch = 60f;      // how far up you can look

    private float _yaw;     // horizontal orbit angle
    private float _pitch;   // vertical orbit angle
    private Vector3 _pivot; // smoothed point the camera orbits (follows the target)

    // Called by the local player's character to hand the camera its follow target.
    public void SetTarget(Transform target)
    {
        _target = target;
        if (_target != null)
        {
            _pivot = _target.position + _targetOffset;
            // Start the camera looking along the character's current facing.
            _yaw = _target.eulerAngles.y;
            _pitch = 15f;
        }
    }

    private void LateUpdate()
    {
        if (_target == null)
            return;

        // --- Read mouse to update orbit angles ---
        var mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 delta = mouse.delta.ReadValue();
            _yaw += delta.x * _mouseSensitivity;
            _pitch -= delta.y * _mouseSensitivity;             // invert so moving mouse up looks up
            _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
        }

        // --- Smoothly follow the target with the orbit pivot ---
        Vector3 desiredPivot = _target.position + _targetOffset;
        _pivot = Vector3.Lerp(_pivot, desiredPivot, _followLerp * Time.deltaTime);

        // --- Place the camera at fixed distance, at the current orbit angle ---
        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 position = _pivot - (rotation * Vector3.forward) * _distance;

        transform.position = position;
        transform.rotation = rotation;
    }
}
