using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Drives the bowling ball: aiming before the shot, the shot itself, and
/// putting everything back for the next one.
/// </summary>
public class Bowling : MonoBehaviour
{
    [SerializeField]
    private Rigidbody rb;

    [SerializeField]
    private int forcePower;

    [SerializeField]
    [Tooltip("Pins put back upright when the shot is reset. Optional.")]
    private PinRack pinRack;

    [SerializeField]
    [Tooltip("How far the ball may slide sideways from its start, in world units.")]
    private float aimRange = 2.5f;

    [SerializeField]
    private float aimSpeed = 1f;

    private Vector3 startPosition;
    private Quaternion startRotation;

    /// <summary>True once the ball has been thrown and before it is reset.</summary>
    public bool HasShot { get; private set; }

    /// <summary>Raised whenever <see cref="HasShot"/> changes, so UI can follow it.</summary>
    public event Action ShotStateChanged;

    void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (pinRack == null)
            pinRack = FindAnyObjectByType<PinRack>();

        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.spaceKey.wasPressedThisFrame)
            ShootBall();

        if (keyboard.rKey.wasPressedThisFrame)
            ResetShot();

        // Aiming only makes sense while the ball is still on the approach.
        if (HasShot)
            return;

        if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed)
            Aim(1f);

        if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed)
            Aim(-1f);
    }

    /// <summary>
    /// Throws the ball down the lane. Ignored if a shot is already in progress,
    /// so mashing the button cannot stack impulses.
    /// </summary>
    public void ShootBall()
    {
        if (HasShot || rb == null)
            return;

        HasShot = true;
        rb.AddForce(Vector3.forward * forcePower, ForceMode.Impulse);
        ShotStateChanged?.Invoke();
    }

    /// <summary>
    /// Puts the ball back on the approach and stands the pins up again.
    /// </summary>
    public void ResetShot()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            // Move through the body so the physics engine does not keep the
            // old pose for one more step.
            rb.position = startPosition;
            rb.rotation = startRotation;
        }

        transform.SetPositionAndRotation(startPosition, startRotation);

        if (pinRack != null)
            pinRack.ResetPins();

        if (!HasShot)
            return;

        HasShot = false;
        ShotStateChanged?.Invoke();
    }

    private void Aim(float direction)
    {
        var position = transform.position;
        position.x = Mathf.Clamp(
            position.x + direction * aimSpeed * Time.deltaTime,
            startPosition.x - aimRange,
            startPosition.x + aimRange);

        transform.position = position;
    }
}
