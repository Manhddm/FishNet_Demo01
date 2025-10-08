using FishNet.Object;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    private static readonly int Move = Animator.StringToHash("Move");

    [Header("References")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;
    [Header("Settings")]
    [SerializeField] private float moveSpeed= 4f;
    [SerializeField] private bool isFacingRight = true;
    private Vector2 previousMoveInput;

    public override void OnStartClient()
    {
        base.OnStartClient();
        if(!IsOwner) return;
        inputReader.MoveEvent += HandleMoveEvent;
        
    }
    public override void OnStopClient()
    {
        base.OnStopClient();
        if(!IsOwner) return;
        inputReader.MoveEvent -= HandleMoveEvent;
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;
        rb.velocity = new Vector2(previousMoveInput.x*moveSpeed, rb.velocity.y);
        animator.SetBool(Move, rb.velocity.x != 0f);
    }

    private void HandleMoveEvent(Vector2 input)
    {
        previousMoveInput = input;
    }
}
