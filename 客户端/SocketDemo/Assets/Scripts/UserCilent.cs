using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Net.Sockets;
using Newtonsoft.Json;

//客户端

//定义一个类保存玩家位置信息
public class PlayerInfo
{
    public string name;
    public float x, y, z;

    public PlayerInfo(string name, Vector3 v)
    {
        this.name = name;
        this.x = v.x;
        this.y = v.y;
        this.z = v.z;
    }
}
//定义一个类作为消息传输的基本格式，不管我i发上面我都是把一个msseage转成json发过去
public class Message
{
    //存一个type，说明这条消息是干什么的
    public string type;
    //这条消息的具体内容
    public string info;

    public Message(string type, string info)
    {
        this.type = type;
        this.info = info;
    }
}

public class UserCilent : MonoBehaviour
{
    static Socket socket;

    private void Start()
    {
        Connect("1",1);
    }

    //从start改成输入ip和端口
    public static void Connect(string ip,int port)
    {
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        //让socket连接,连接的是本地的
        socket.Connect("127.0.0.1", 54535);
        //成功连接后调用listener东西哦，把Socket传进去
        Listener.startListen(socket);
        
        //成功连接后通过Socket进行收发操作
        /*//send传的是byte数组，把要传输的内容先写出来再转化为byte数组
        string str = "66666";
        byte[] bytes = Encoding.Default.GetBytes(str);
        //再发送转化后的byte
        socket.Send(bytes);
        
        //客户端给服务端发消息
        byte[] buffer = new byte[1024];
        //recive的返回值表示接收到的消息的长度
        int len =  socket.Receive(buffer);
        //把byte字符串转化为字符串,buffer的第0位开始到len
        string s = Encoding.Default.GetString(buffer, 0, len);
        Debug.Log(s);*/

    }

    //打包发消息这件事情
    public static void SendMessage(Message msg)
    {
        if (socket != null && socket.Connected)
        { 
            //对p进行序列化会返回一个字符串
            string str = JsonConvert.SerializeObject(msg);
            byte[] buffer = Encoding.Default.GetBytes(str + "&");
            socket.Send(buffer);
        }
       
    }

}
