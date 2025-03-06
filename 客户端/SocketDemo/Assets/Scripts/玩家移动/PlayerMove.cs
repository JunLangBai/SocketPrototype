using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

public class PlayerMove : MonoBehaviour
{
    public float sendCD;
    [Header("PlayerMove")]
    //矢量运动方向
    Vector3 moveDirection;
    //为方向创建变换
    public Transform orientation;
    
    public float moveSpeed = 10f;
    
   //两个变量用于水平垂直键盘输入
   float horizontalInput;
   float verticalInput;
   
   //检查玩家是否在地面上,才能应用阻力，因为在空中有阻力非常奇怪
   [Header("Ground Check")]
   //玩家的高度
   public float playerHeight;
   //为地面层蒙版
   public LayerMask whatIsGround;
   //是否为地面
   public bool grounded;
   
   //移动施加到刚体
   Rigidbody rb;
   
   string playerName;
    void Start()
    {
        playerName = "Player_" + Random.Range(1000,9999);
        PlayerPool.Instance.localPlayerName = playerName;
        rb = GetComponent<Rigidbody>();
    }
    
    void Update()
    {
        SendPosition();
        grounded = Physics.SphereCast(transform.position, playerHeight / 2, Vector3.down, out RaycastHit hit, 0.1f, whatIsGround);

        // 可视化射线，红色表示射线的路径，持续时间为2秒
        Debug.DrawRay(transform.position, Vector3.down * (playerHeight * 0.5f + 0.02f), Color.red, 0.002f);
        MyInput();
        SpeedControl();
    }

    //fixUpdate里调用移动人物
    private void FixedUpdate()
    {
        MovePlayer();
    }

    public void MyInput()
    {
        //水平
        horizontalInput = Input.GetAxisRaw("Horizontal");
        //垂直
        verticalInput = Input.GetAxisRaw("Vertical");
        /*//网络传输原型
        Vector3 movement = new Vector3(horizontalInput, 0f, verticalInput) * moveSpeed * Time.deltaTime;
        transform.Translate(movement);*/
    }
    
    private void MovePlayer()
    {
        //为人物移动，通过创建新的vector3并设置为orientation.forward（orientation是方向，forward是向前）乘以垂直输入
        //加上orientation.right乘以horizontalInput
          //相当于 赋值垂直的移动和水平的移动来完成人物的移动
        //以这种方式输入，你将始终朝着你现在正在寻找的方向行走
        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;
        
        //给你的玩家施加力量，addforce的参数决定力的作用方式
        //normalized返回向量（重点在方向） 乘以 移动速度 再乘以 10（只是为了移动速度更快，不嫌麻烦自行定义public变量，但是可读性会差）
        //forcemode是力的施加是什么模式，force是单纯的力，更多的力的方式自行参考文档（问度娘也行）
        if (grounded)
        {
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);
        }
    }

    //控制玩家移动速度
    private void SpeedControl()
    {
        //获得玩家刚体的平面速度,只需要x和z
        Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        //如果这个速度大于你的移动速度
        if (flatVel.magnitude > moveSpeed)
        {
            //创建新的向量，他等于flatVel.normalized 乘以 movespeed 让速度变得有限，再赋给人物刚体
            Vector3 limitedVel = flatVel.normalized * moveSpeed;
            rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
        }

    }
    
    //发送消息间隔
    private float ntime = 0f;
    private void SendPosition()
    { 
        ntime += Time.deltaTime;
        if (ntime > sendCD)
        { 
            PlayerInfo p = new PlayerInfo(playerName,transform.position);
            UserCilent.SendMessage(new Message("UpdatePlayerInfo",JsonConvert.SerializeObject(p)));
            ntime = 0f;
        }
    }
    
}
