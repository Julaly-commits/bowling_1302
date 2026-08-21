using UnityEngine;

/// <summary>
/// Remembers where every pin started so the rack can be stood back up between
/// shots. Put this on the object the pins are parented to.
/// </summary>
public class PinRack : MonoBehaviour
{
    private struct PinPose
    {
        public Rigidbody Body;
        public Vector3 Position;
        public Quaternion Rotation;
    }

    private PinPose[] pins;

    void Awake()
    {
        Snapshot();
    }

    /// <summary>
    /// Records the current pose of every pin as the one to return to. Called on
    /// Awake, and worth calling again if pins are added at runtime.
    /// </summary>
    public void Snapshot()
    {
        var bodies = GetComponentsInChildren<Rigidbody>(true);
        pins = new PinPose[bodies.Length];

        for (int i = 0; i < bodies.Length; i++)
        {
            pins[i] = new PinPose
            {
                Body = bodies[i],
                Position = bodies[i].transform.position,
                Rotation = bodies[i].transform.rotation,
            };
        }
    }

    /// <summary>Stands every pin back up at its recorded pose, motionless.</summary>
    public void ResetPins()
    {
        if (pins == null)
            return;

        foreach (var pin in pins)
        {
            if (pin.Body == null)
                continue;

            pin.Body.linearVelocity = Vector3.zero;
            pin.Body.angularVelocity = Vector3.zero;
            pin.Body.position = pin.Position;
            pin.Body.rotation = pin.Rotation;
            pin.Body.transform.SetPositionAndRotation(pin.Position, pin.Rotation);

            // A pin that toppled and went to sleep will not settle correctly
            // unless it is woken for the next simulation step.
            pin.Body.WakeUp();
        }
    }
}
