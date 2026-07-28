using UnityEngine;

public class OnCollision : MonoBehaviour
{
    public PlayerController controller;


    void OnTriggerEnter(Collider other)
    {
        if (other.transform.tag == "Player" || other.transform.tag == "Ground")
            return;

        controller.OnCharacterColliderHit(other);
    }
}
