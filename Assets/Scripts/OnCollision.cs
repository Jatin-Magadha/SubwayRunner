using UnityEngine;

public class OnCollision : MonoBehaviour
{
    public PlayerController controller;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.transform.tag == "Player")
            return;

        controller.OnCharacterColliderHit(collision.collider);
    }
}
