using System;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using UnityEngine;
using FishNet.Object;
public class PlayerController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [Header("Settings")]
    [SerializeField] private Vector2 clampX = new Vector2(-8f, 8f);
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private int sendInterval = 1;

    [Header("Networking")] [SerializeField]
    private float inputSendCooldown = 0.05f;// Gui input len server moi 0.05s


    private float _lastSentTime;
    private float _serverAxis;

    private void Awake()
    {
        if(rb == null)
            rb = GetComponent<Rigidbody2D>();
    }


    public override void OnStartServer()
    {
        rb.simulated = true;
        rb.isKinematic = false; // Server dieu khien vat ly
        Debug.Log($"[Server] ServerInit={base.IsServerInitialized}");
    }

    public override void OnStartClient()
    {
        if (IsServerInitialized) return;
        rb.simulated = false;
        rb.isKinematic = true;//de phong script khac can thiep vao vat ly
        Debug.Log($"[Client] Owner={base.IsOwner}  Ctrl={base.IsController}  " +
                  $"ClientInit={base.IsClientInitialized}  ClientOnlyInit={InstanceFinder.NetworkManager.IsClientOnlyStarted}");
    }

    private void Update()
    {
        if (!IsOwner || !IsClientInitialized) return;
        
        float axis = Input.GetAxis("Horizontal");

        bool changeDir = Mathf.Sign(axis) != Mathf.Sign(_serverAxis);
        if (changeDir || Time.time - _lastSentTime > inputSendCooldown)
        {
            _lastSentTime = Time.time;
            SendInputServerRpc(axis);
        }
    }
    
    
    [ServerRpc]// Chay tren server, duoc goi tu owner
    private void SendInputServerRpc(float axis)
    {
        _serverAxis = Mathf.Clamp(axis, -1f, 1f);
    }
    private void FixedUpdate()
    {
        //Chi server moi cap nhat vi tri
        if (!IsServerInitialized) return;

        Vector2 p = rb.position;
        p.x = Mathf.Clamp(p.x + _serverAxis * moveSpeed*Time.fixedDeltaTime, clampX.x, clampX.y);
        rb.MovePosition(p);
    }
}
