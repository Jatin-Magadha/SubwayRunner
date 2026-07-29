using System.Collections;
using UnityEngine;

[System.Serializable]
public enum SIDE { Left = -5, Mid = 0, Right = 5 };

public enum HitX { Left, Mid, Right, None };
public enum HitY { Up, Mid, Down, Low, None };
public enum HitZ { Forward, Mid, Backward, None };

public class PlayerController : MonoBehaviour
{
    public SIDE side = SIDE.Mid;

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
    private SIDE lastSide;
    public bool stopAllState = false;
    public float stumbleTolerance = 10.0f;
    private float stumbleTime;
    private bool canInput = true;
    public Collider CollisionCol;

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        colHeight = characterController.height;
        colCenterY = characterController.center.y;
        animator = GetComponent<Animator>();

        //transform.position = Vector3.zero;
        stumbleTime = stumbleTolerance;
    }

    private void Update()
    {
        if (GameManager.Instance.CurrentGameState != GameState.InProgress)
            return;

        if (!canInput)
        {
            characterController.Move(Vector3.down * 10.0f * Time.deltaTime);
            return;
        }

        swipeLeft = (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) && canInput;
        swipeRight = (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) && canInput;
        swipeUp = (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) && canInput;
        swipeDown = (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) && canInput;

        if (swipeLeft && !inRoll)
        {
            if (side == SIDE.Mid)
            {
                lastSide = side;
                side = SIDE.Left;

                if (animator)
                {
                    PlayAnimation("dodgeLeft");
                }
            }
            else if (side == SIDE.Right)
            {
                lastSide = side;

                side = SIDE.Mid;

                if (animator)
                {
                    PlayAnimation("dodgeLeft");
                }
            }
            else if (side != lastSide)
            {
                lastSide = side;
                if (animator)
                {
                    PlayAnimation("stumbleOffLeft");
                }
            }
        }
        if (swipeRight && !inRoll)
        {
            if (side == SIDE.Mid)
            {
                lastSide = side;
                side = SIDE.Right;

                if (animator)
                {
                    PlayAnimation("dodgeRight");
                }
            }
            else if (side == SIDE.Left)
            {
                lastSide = side;
                side = SIDE.Mid;

                if (animator)
                {
                    PlayAnimation("dodgeRight");
                }
            }
            else if (side != lastSide)
            {
                lastSide = side;
                if (animator)
                {
                    PlayAnimation("stumbleOffRight");
                }
            }
        }

        if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1)
        {
            //animator.SetLayerWeight(1, 0);

            stopAllState = false;
        }
        stumbleTime = Mathf.MoveTowards(stumbleTime, stumbleTolerance, Time.deltaTime);

        x = Mathf.Lerp(x, (int)side, dodgeSpeed * Time.deltaTime);
        Vector3 moveVector = new Vector3(x - transform.position.x, y * Time.deltaTime, moveSpeed * Time.deltaTime);
        characterController.Move(moveVector);

        Jump();
        Roll();
    }

    public void PlayAnimation(string animName)
    {
        if (stopAllState)
            return;

        animator.Play(animName);
    }

    public IEnumerator PlayDeath(string animName)
    {
        GameManager.Instance.ChangeGameState(GameState.GameOver);

        stopAllState = true;
        animator.Play(animName);

        canInput = false;


        yield return new WaitForSeconds(2.0f);

        canInput = false;
    }

    public void Stumble(string animName)
    {
        animator.ForceStateNormalizedTime(0.0f);
        stopAllState = true;
        animator.Play(animName);

        if (stumbleTime < stumbleTolerance / 2)
        {
            StartCoroutine(PlayDeath("stumble_Low"));
            return;
        }

        stumbleTime -= 6;
        ResetCollision();
    }


    public void Jump()
    {
        if (inRoll)
            return;

        if (characterController.isGrounded)
        {
            if (animator.GetCurrentAnimatorStateInfo(0).IsName("falling"))
            {
                PlayAnimation("landing");
                inJump = false;
            }
            if (swipeUp)
            {
                y = jumpPower;
                if (animator)
                {
                    animator.CrossFadeInFixedTime("jump", 0.1f);
                    inJump = true;
                }
            }
        }
        else
        {
            y -= jumpPower * 2 * Time.deltaTime;
            if (characterController.velocity.y < -0.1f)
                if (animator)
                {
                    PlayAnimation("falling");
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
            animator.CrossFadeInFixedTime("roll", 0.1f);
            inRoll = true;
            inJump = false;
        }
    }

    public void OnCharacterColliderHit(Collider col)
    {
        hitX = GetHitX(col);
        hitY = GetHitY(col);
        hitZ = GetHitZ(col);

        if (hitZ == HitZ.Forward && hitX == HitX.Mid)
        {
            if (hitY == HitY.Low)
            {
                if (animator)
                {
                    Stumble("stumble_low");
                }
            }
            else if (hitY == HitY.Down)
            {
                if (animator)
                {
                    StartCoroutine(PlayDeath("death_lower"));
                }
                ResetCollision();
            }
            else if (hitY == HitY.Mid)
            {
                if (col.tag == "MovingTrain")
                {
                    if (animator)
                    {
                        StartCoroutine(PlayDeath("death_movingTrain"));

                    }
                    ResetCollision();
                }
                if (col.tag == "Ramp")
                {
                    if (animator)
                    {
                        StartCoroutine(PlayDeath("death_bounce"));
                    }
                    ResetCollision();
                }
            }
            else if (hitY == HitY.Up)
            {
                if (animator)
                {
                    StartCoroutine(PlayDeath("death_upper"));
                }
                ResetCollision();
            }
        }
        else if (hitZ == HitZ.Mid)
        {
            if (hitX == HitX.Right)
            {
                if (animator)
                {
                    side = lastSide;
                    Stumble("stumbleSideRight");
                }
            }
            else if (hitX == HitX.Left)
            {
                if (animator)
                {
                    side = lastSide;
                    Stumble("stumbleSideLeft");
                }
            }
        }
        else
        {
            if (hitX == HitX.Right)
            {
                if (animator)
                {
                    //animator.SetLayerWeight(1, 1);
                    Stumble("stumbleCornerRight");
                }
                ResetCollision();
            }
            else if (hitX == HitX.Left)
            {
                if (animator)
                {
                    //animator.SetLayerWeight(1, 1);
                    Stumble("stumbleCornerLeft");
                }
                ResetCollision();
            }
        }
    }

    private void ResetCollision()
    {
        hitX = HitX.None;
        hitY = HitY.None;
        hitZ = HitZ.None;
    }

    public HitX GetHitX(Collider col)
    {
        Bounds charBounds = characterController.bounds;
        Bounds colBounds = col.bounds;
        float minX = Mathf.Max(colBounds.min.x, charBounds.min.x);
        float maxX = Mathf.Min(colBounds.max.x, charBounds.max.x);
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
        float maxY = Mathf.Min(colBounds.max.y, charBounds.max.y);
        float average = ((minY + maxY) / 2.0f - charBounds.min.y) / charBounds.size.y;

        HitY hit;
        if (average < 0.17f)
        {
            hit = HitY.Low;
        }
        else if (average < 0.33f)
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
        float maxZ = Mathf.Min(colBounds.max.z, charBounds.max.z);
        float average = ((minZ + maxZ) / 2.0f - charBounds.min.z) / charBounds.size.z;

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
