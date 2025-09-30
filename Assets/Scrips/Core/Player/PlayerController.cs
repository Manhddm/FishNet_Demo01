using System;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;


public class PlayerController : NetworkBehaviour
{
    [Header("References")] [SerializeField]
    private Rigidbody2D rb;

    [SerializeField] private Animator animator;
    [SerializeField] private Transform visual;
    [Header("Settings")] [SerializeField] private Vector2 clampX = new Vector2(-8f, 8f);
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private bool isFacingRight = true;


    [Header("Networking")]
    [SerializeField]
    [Tooltip("Khoang thoi gian gui input toi server, de tranh gui qua nhieu lan lien tuc")]
    private float inputSendCooldown = 0.05f; // Gui input len server moi 0.05s

    [Tooltip(("Gioi han bu hien thi (m) - tranh visual lh qua xa root"))] [SerializeField]
    private float maxVisualOffset = 0.5f;

    [Tooltip("He so keo visual ve 0 moi frame")] [SerializeField, Range(0f, 1f)]
    private float catchupLerp = 0.2f;


    private float _lastSentTime;
    private int _localTick; // Tick cuc bo cua owner (FX update dem)
    private float _serverAxis;
    private int _serverLastTick; // tick moi nhat da xu ly tren server)


    private struct InputSample
    {
        public int tick;
        public float axis;
    }

    private readonly List<InputSample> _pendingInputs = new List<InputSample>();
    private float _predictedAxis; // vi tri du doan
    private int _lastAckTick; // tick da duoc xac nhan(client da nhan)
    private float _lastServerAxis; // gia tri input cuoi cung da nhan tu server

    private void Awake()
    {
        if (rb == null)
            rb = GetComponentInChildren<Rigidbody2D>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }


    public override void OnStartServer()
    {
        rb.simulated = true;
        rb.isKinematic = false; // Server dieu khien vat ly
    }

    public override void OnStartClient()
    {
        if (!IsServerInitialized)
        {
            rb.simulated = false;
            rb.isKinematic = true; //de phong script khac can thiep vao vat ly
        }

        if (visual == null && transform.childCount > 0)
        {
            visual = transform.GetChild(0);
        }
        
    }

    private void Update()
    {
        if (!IsOwner || !IsClientInitialized) return;

        float axis = Input.GetAxis("Horizontal");

        bool changeDir = Mathf.Sign(axis) != Mathf.Sign(_serverAxis);

        if (changeDir || Time.time - _lastSentTime > inputSendCooldown)
        {
            _lastSentTime = Time.time;
            _localTick++;
            _pendingInputs.Add(new InputSample
            {
                tick = _localTick, axis = axis
            });
            
            // Cập nhật animation và hướng mặt locally cho owner
            UpdateVisualState(axis);
            
            SendInputServerRpc(axis, _localTick);
        }
    }

    private void UpdateVisualState(float axis)
    {
        // Cập nhật hướng mặt
        if (axis > 0 && !isFacingRight)
        {
            Flip();
        }
        else if (axis < 0 && isFacingRight)
        {
            Flip();
        }
        
        // Cập nhật animation
        if (animator != null)
        {
            animator.SetBool("Move", Mathf.Abs(axis) > 0.01f);
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        if (visual != null)
        {
            Vector3 scale = visual.localScale;
            scale.x *= -1;
            visual.localScale = scale;
        }
    }


    [ServerRpc] // Chay tren server, duoc goi tu owner
    private void SendInputServerRpc(float axis, int tick)
    {
        _serverAxis = Mathf.Clamp(axis, -1f, 1f); // Luu tru trang thai input tren server
        _serverLastTick = Mathf.Max(_serverLastTick, tick);
        
        // Cập nhật hướng mặt trên server dựa vào input
        if (axis > 0.01f && !isFacingRight)
        {
            isFacingRight = true;
            if (visual != null)
            {
                Vector3 scale = visual.localScale;
                scale.x = Mathf.Abs(scale.x);
                visual.localScale = scale;
            }
        }
        else if (axis < -0.01f && isFacingRight)
        {
            isFacingRight = false;
            if (visual != null)
            {
                Vector3 scale = visual.localScale;
                scale.x = -Mathf.Abs(scale.x);
                visual.localScale = scale;
            }
        }
    }

    private void FixedUpdate()
    {
        if (IsServerInitialized)
        {
            Vector2 p = rb.position;
            p.x += _serverAxis * moveSpeed * Time.fixedDeltaTime;
            p.x = Mathf.Clamp(p.x, clampX.x, clampX.y);
            rb.MovePosition(p);
            
            // Gửi cả thông tin animation (isMoving) đến clients
            bool isMoving = Mathf.Abs(_serverAxis) > 0.01f;
            SendSnapshotTarget(Owner, rb.position.x, _serverLastTick, isFacingRight, isMoving);
        }
        
        if (IsOwner && IsClientInitialized)
        {
            if (_lastAckTick == 0)
            {
                _predictedAxis = transform.position.x;
                SetVisualOffset(_predictedAxis - transform.position.x, snap: true);
                return;
            }

            float axis = _lastServerAxis;
            float dt = Time.fixedDeltaTime;
            for (int i = 0; i < _pendingInputs.Count; i++)
            {
                axis = Mathf.Clamp(axis+_pendingInputs[i].axis*moveSpeed*dt, clampX.x, clampX.y);
            }

            _predictedAxis = axis;

            float offset = Mathf.Clamp(_predictedAxis - transform.position.x, -maxVisualOffset, maxVisualOffset);
            SetVisualOffset(offset, snap: false);
        }
        else if (!IsOwner && IsClientInitialized)
        {
            // Các observer khác sẽ smooth visual về 0
            if (visual) visual.localPosition = Vector3.Lerp(visual.localPosition, Vector3.zero, 0.5f);
        }
    }
    
    [ObserversRpc]
    private void SendSnapshotTarget(NetworkConnection connection, float serverPosX, int lastProcessedTick, bool faceRight, bool isMoving)
    {
        // Nếu không phải owner, cập nhật animation và hướng từ server
        if (!IsOwner)
        {
            isFacingRight = faceRight;
            if (visual != null)
            {
                Vector3 scale = visual.localScale;
                scale.x = isFacingRight ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
                visual.localScale = scale;
            }
            
            if (animator != null)
            {
                animator.SetBool("Move", isMoving);
            }
        }
        
        // Owner vẫn xử lý reconciliation như bình thường
        if (IsOwner)
        {
            _lastServerAxis = serverPosX;
            _lastAckTick = Mathf.Max(_lastAckTick, lastProcessedTick);
            isFacingRight = faceRight;
            if (visual != null)
            {
                Vector3 scale = visual.localScale;
                scale.x = isFacingRight ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
                visual.localScale = scale;
            }
            
            // Cắt bỏ input đã ack
            int idx = _pendingInputs.FindIndex(s => s.tick > _lastAckTick);
            if (idx <= 0)
            {
                _pendingInputs.Clear();
            }
            else
            {
                _pendingInputs.RemoveRange(0, idx);
            }
            
            // Nếu lệch quá xa: snap hiển thị
            float absError = Mathf.Abs(transform.position.x - _lastServerAxis);
            if (absError > maxVisualOffset * 1.5f)
                SetVisualOffset(0f, snap: true);
        }
    }
    
    private void SetVisualOffset(float offsetX, bool snap)
    {
        if (!visual) return;
        Vector3 target = new Vector3(offsetX, 0f, 0f);
        if (snap)
        {
            visual.localPosition = target;
        }
        else 
        {
            visual.localPosition = Vector3.Lerp(visual.localPosition, target, catchupLerp);
        }
    }
}