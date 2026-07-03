using UnityEngine;

public class DestroyAfterWait : MonoBehaviour
{
    [SerializeField] private float waitTime = 1.0f;

    void Start()
    {
        Destroy(gameObject, waitTime);
    }

}
