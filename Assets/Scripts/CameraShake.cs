using UnityEngine;
using System.Collections;

/// <summary>
/// Attach to the main camera. PlayerController calls Shake() on stumble.
/// Also used by GameManager for the death impact shake.
/// </summary>
public class CameraShake : MonoBehaviour
{
    private Vector3 originalLocalPosition;
    private Coroutine activeShake;

    private void Awake() => originalLocalPosition = transform.localPosition;

    public void Shake(float magnitude, float duration)
    {
        if (activeShake != null) StopCoroutine(activeShake);
        activeShake = StartCoroutine(ShakeRoutine(magnitude, duration));
    }

    private IEnumerator ShakeRoutine(float magnitude, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float progress  = elapsed / duration;
            float dampened  = magnitude * (1f - progress);   // fade out over time
            transform.localPosition = originalLocalPosition
                + (Vector3)Random.insideUnitCircle * dampened;
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localPosition = originalLocalPosition;
        activeShake = null;
    }
}