using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows the Shoot button before a throw and the Reset button after it. Both
/// buttons sit in the same spot, so only one is ever visible.
/// </summary>
public class ShotButtons : MonoBehaviour
{
    [SerializeField]
    private Bowling bowling;

    [SerializeField]
    private Button shootButton;

    [SerializeField]
    private Button resetButton;

    void Awake()
    {
        if (bowling == null)
            bowling = FindAnyObjectByType<Bowling>();
    }

    void OnEnable()
    {
        if (bowling == null)
        {
            Debug.LogError("[ShotButtons] No Bowling component assigned or found in the scene.");
            enabled = false;
            return;
        }

        // Wire the clicks in code so the buttons work no matter what the
        // inspector's persistent call list happens to hold.
        if (shootButton != null)
        {
            shootButton.onClick.RemoveListener(bowling.ShootBall);
            shootButton.onClick.AddListener(bowling.ShootBall);
        }

        if (resetButton != null)
        {
            resetButton.onClick.RemoveListener(bowling.ResetShot);
            resetButton.onClick.AddListener(bowling.ResetShot);
        }

        bowling.ShotStateChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (bowling == null)
            return;

        bowling.ShotStateChanged -= Refresh;

        if (shootButton != null)
            shootButton.onClick.RemoveListener(bowling.ShootBall);

        if (resetButton != null)
            resetButton.onClick.RemoveListener(bowling.ResetShot);
    }

    private void Refresh()
    {
        bool thrown = bowling.HasShot;

        if (shootButton != null)
            shootButton.gameObject.SetActive(!thrown);

        if (resetButton != null)
            resetButton.gameObject.SetActive(thrown);
    }
}
