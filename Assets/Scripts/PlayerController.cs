using UnityEngine;

[System.Serializable]
public enum SIDE { Left, Mid, Right };

public enum HitX { Left, Mid, Right, None };
public enum HitY { Up, Mid, Down, None };
public enum HitZ { Forward, Mid, Backward, None };

public class PlayerController : MonoBehaviour
{
    public SIDE side = SIDE.Mid;

    private float newXPos = 0.0f;

    public float xValue = 5;
    public bool swipeLeft = false;
    public bool swipeRight = false;
    public bool swipeUp = false;
    public bool swipeDown = false;
    private CharacterController characterController;
    private Animator animator;
    private float x;
    public float dodgeSpeed = 10.0f;
    public float jumpPower = 7.0f;
    private float y;
    public bool inJump = false;
    public bool inRoll = false;
    public float moveSpeed = 7.0f;
    private float colHeight;
    private float colCenterY;
    internal float rollCounter;
    public HitX hitX = HitX.None;
    public HitY hitY = HitY.None;
    public HitZ hitZ = HitZ.None;

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        colHeight = characterController.height;
        colCenterY = characterController.center.y;
        animator = GetComponent<Animator>();

        transform.position = Vector3.zero;
    }

    private void Update()
    {
        swipeLeft = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
        swipeRight = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);
        swipeUp = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
        swipeDown = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);

        if (swipeLeft && !inRoll)
        {
            if (side == SIDE.Mid)
            {
                newXPos = -xValue;
                side = SIDE.Left;

                if (animator)
                {
                    animator.Play("DodgeLeft");
                }
            }
            else if(side == SIDE.Right) 
            {
                newXPos = 0;
                side = SIDE.Mid;

                if (animator)
                {
                    animator.Play("DodgeLeft");
                }
            }
        }
        if (swipeRight && !inRoll)
        {
            if (side == SIDE.Mid)
            {
                newXPos = xValue;
                side = SIDE.Right;

                if (animator)
                {
                    animator.Play("DodgeRight");
                }
            }
            else if (side == SIDE.Left)
            {
                newXPos = 0;
                side = SIDE.Mid;

                if (animator)
                {
                    animator.Play("DodgeRight");
                }
            }
        }

        Vector3 moveVector = new Vector3(x - transform.position.x, y * Time.deltaTime, moveSpeed * Time.deltaTime);

        x = Mathf.Lerp(x, newXPos, dodgeSpeed * Time.deltaTime);
        characterController.Move(moveVector);

        Jump();
        Roll();
    }

    public void Jump()
    {
        if (characterController.isGrounded)
        {
            if (animator.GetCurrentAnimatorStateInfo(0).IsName("Falling"))
            {
                animator.Play("Landing");
                inJump = false;
            }
            if (swipeUp)
            {
                y = jumpPower;
                if (animator)
                {
                    animator.CrossFadeInFixedTime("Jump", 0.1f);
                    inJump = true;
                }
            }
        }
        else
        {
            y -= jumpPower * 2 * Time.deltaTime;
            if(characterController.velocity.y < -0.1f)
            if (animator)
            {
                animator.Play("Falling");
            }
        }
    }

    public void Roll()
    {
        rollCounter -= Time.deltaTime;
        if (rollCounter <= 0f)
        {
            rollCounter = 0f;
            characterController.center = new Vector3(0, colCenterY, 0);
            characterController.height = colHeight;
            inRoll = false;
        }
        if (swipeDown)
        {
            rollCounter = 0.2f;
            y -= 10.0f;
            characterController.center = new Vector3(0, colCenterY / 2.0f, 0);
            characterController.height = colHeight / 2.0f;
            animator.CrossFadeInFixedTime("Roll", 0.1f);
            inRoll = true;
            inJump = false;
        }
    }

    public void OnCharacterColliderHit(Collider col)
    {
        GetHitX(col);
        GetHitY(col);
        GetHitZ(col);
    }

    public HitX GetHitX(Collider col)
    {
        Bounds charBounds = characterController.bounds;
        Bounds colBounds = col.bounds;
        float minX = Mathf.Max(colBounds.min.x, charBounds.min.x);
        float maxX = Mathf.Max(colBounds.max.x, charBounds.max.x);
        float average = (minX + maxX) / 2.0f - colBounds.min.x;

        HitX hit;
        if (average > colBounds.size.x - 0.33f)
        {
            hit = HitX.Right;
        }
        else if (average < 0.33f)
        {
            hit = HitX.Left;
        }
        else
        {
            hit = HitX.Mid;
        }
        return hit;
    }


    public HitY GetHitY(Collider col)
    {
        Bounds charBounds = characterController.bounds;
        Bounds colBounds = col.bounds;
        float minY = Mathf.Max(colBounds.min.y, charBounds.min.y);
        float maxY = Mathf.Max(colBounds.max.y, charBounds.max.y);
        float average = ((minY + maxY) / 2.0f - colBounds.min.y) / charBounds.size.y;

        HitY hit;
        if (average < 0.33f)
        {
            hit = HitY.Down;
        }
        else if (average < 0.66f)
        {
            hit = HitY.Mid;
        }
        else
        {
            hit = HitY.Up;
        }
        return hit;
    }

    public HitZ GetHitZ(Collider col)
    {
        Bounds charBounds = characterController.bounds;
        Bounds colBounds = col.bounds;
        float minZ = Mathf.Max(colBounds.min.z, charBounds.min.z);
        float maxZ = Mathf.Max(colBounds.max.z, charBounds.max.z);
        float average = ((minZ + maxZ) / 2.0f - colBounds.min.z) / charBounds.size.z;

        HitZ hit;
        if (average < 0.33f)
        {
            hit = HitZ.Backward;
        }
        else if (average < 0.66f)
        {
            hit = HitZ.Mid;
        }
        else
        {
            hit = HitZ.Forward;
        }
        return hit;
    }
}
