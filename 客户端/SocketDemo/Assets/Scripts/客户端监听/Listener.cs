using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using System.Threading;

//客户端消息监听，和服务端差不多
public class Listener : MonoBehaviour
{
    public static void startListen(Socket sc)
    {
        //开启子线程，带参
        Thread listen = new Thread(new ParameterizedThreadStart(listenServer));
        //传入套接字Socket
        listen.Start(sc);
    }
    
    //收集到消息后和服务端一样需要把得到的message传到主线程运行，虽然这里只有一个子线程，但是unity本身就是一个单线程，他限制你不允许在子线程里面修改任何东西，所有还是需要一个toDoList
    static Queue<Message> toDoList = new Queue<Message>();

    //写一个listenr，同样要把Socket传进来，用obj的方式，循环来接听这个东西
    static void listenServer(object obj)
    {
        Socket sclient = (Socket)obj;
        byte[] buffer = new byte[1024];
        while (true)
        {
            //服务端强行关闭检查
            try
            {
                int len = sclient.Receive(buffer);
                string str = Encoding.Default.GetString(buffer, 0, len);
                Debug.Log($"收到原始数据: {str}");
                //获得字符串之后把他变成message
                foreach (string s in str.Split('&'))
                {
                    if (s.Length > 0)
                    {
                        //这里不需要lock，因为只有这一个子线程
                        Message msg = JsonConvert.DeserializeObject<Message>(s);
                        toDoList.Enqueue(msg);
                    }
                }
            }
            catch (SocketException ex)
            {
                Debug.LogError($"连接异常: {ex.Message}");
                //服务端关闭时也break
                break;
                //也可以弄一个弹回标题界面
            }
        }
    }

    private void Update()
    {
        //在主线程里去不断处理这个东西
        if (toDoList.Count > 0)
        {
            Message msg = toDoList.Dequeue();
            Debug.Log($"[客户端] 处理消息: Type={msg.type}");
            //同样根据消息类型执行,现在只是简单发送玩家信息，如果还要弄更改场景什么的肯定是要把消息区分
            switch (msg.type)
            {
                case "AllPlayerInfo" :
                    //接收到AllPlayerInfo是列表
                    List<PlayerInfo> list = JsonConvert.DeserializeObject<List<PlayerInfo>>(msg.info);
                    //收到这个信息的时候执行一下方法
                    PlayerPool.Instance.UpdatePlayer(list); 
                    Debug.Log(list.Count);
                    break;
            }
        }
    }
}
