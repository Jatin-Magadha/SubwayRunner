using SubwaySurferClone;
using UnityEngine;

public class MagnetRange : MonoBehaviour
{
    [SerializeField] private Transform playerTransform;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Coin"))
        {
            if (other.TryGetComponent(out Coin coin))
            {
                coin.EnableMagnetAbility(playerTransform.position);
            }
        }
    }
}
