using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
//引入线程
using System.Threading;
using Newtonsoft.Json;


//服务端
namespace SocketServer
{
    public class PlayerInfo
    {
        public string name;
        public float x, y, z;
    }

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

    class Program
    {
        //保存所有连接
        static List<Socket> userOnline = new List<Socket>();
        //开一个list保存在线玩家信息
        static List<PlayerInfo> userInfo = new List<PlayerInfo>();
        
        //注意一点，多个子线程并行的时候，如果在子线程里同时调用变量等操作会导致冲突，会有很大的资源竞争问题，所以处理消息最好不要在子线程里处理，我们可以把消息全部保存起来
        //我们在子线程里只需要负责收听，把它存起来扔到主线程去执行
        //所以弄一个队列，里面保存message保存消息，在监听线程里面每当接收到消息就把他塞到队列里
        static Queue<Message> toDoList = new Queue<Message>();
        
        //到这里并没有完全解决问题，因为我们是每接触到一个客户端连接就开一个listenClient，这个线程会开很多个，在 toDoList.Enqueue(msg)还是有可能同时对toDoList队列进行操作
        //因为这个操作不是原子操作，里面还是可以细分的，如果两个子线程同时进行还是可能会出错，因此需要引入一个简单的封锁机制
        //定义私有静态只读的object类型
        private static readonly object toDoListLock = new object();
        // 新增：Socket到玩家名称的映射
        static Dictionary<Socket, string> socketToName = new Dictionary<Socket, string>();

        //打包服务端发送消息，需要传给谁的
        static void SendMessage(Socket sclient, Message msg)
        {
            try
            {
                if (sclient != null && sclient.Connected)
                {
                    string json = JsonConvert.SerializeObject(msg);
                    byte[] bytes = Encoding.UTF8.GetBytes(json + "&");
                    sclient.Send(bytes);
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[服务端] 发送失败: {ex.Message}");
                // 强制关闭并清理
                CleanupClient(sclient);
            }
        }

        //监听接收消息也需要创建一个子线程,需要把Socket传进来，但是开了子线程，所以传一个object再强类型转换
        static void listenClient(object obj)
        {
            Socket sclient = (Socket)obj;
            //服务端连接成功后也开始接收消息，同样是bytes
            byte[] buffer = new byte[1024];
            
            //应对客户端强行断开连接会导致崩掉，所以要异常处理
            try
            {
                while (true)
                {
                    //recive的返回值表示接收到的消息的长度
                    int len = sclient.Receive(buffer);
                    //把byte字符串转化为字符串,buffer的第0位开始到len
                    string str = Encoding.Default.GetString(buffer, 0, len);
                    
                    //这个地方改成和客户端一样的Message
                    foreach (string s in str.Split('&'))
                    {
                        if (string.IsNullOrEmpty(s)) continue;
                        Message msg = JsonConvert.DeserializeObject<Message>(s);
                        
                        //需要对toDoList队列进行操作的时候进行lock，含义就是说执行这句话之前先检查toDoListLock有没有被锁上，如果被锁上会一直等到他解锁，并且执行这个lock的时候主动把toDoListLock锁上
                        //也就是说被锁上的这个代码块他不会同时进行，会有序进行，这样就避免了那个资源冲突的问题
                        lock (toDoListLock)
                        {
                            //在监听线程里面每当接收到消息就把他塞到队列里
                            toDoList.Enqueue(msg);
                        }
                        // 更新Socket到名称的映射
                        if (msg.type == "UpdatePlayerInfo")
                        {
                            PlayerInfo p = JsonConvert.DeserializeObject<PlayerInfo>(msg.info);
                            lock (socketToName)
                            {
                                socketToName[sclient] = p.name;
                            }
                        }
                    }
                }
            }
            catch (SocketException ex) when (ex.ErrorCode == 10054)
            {
                Console.WriteLine($"[服务端] 客户端断开: {ex.Message}");
                CleanupClient(sclient);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[服务端] 异常: {ex.Message}");
                CleanupClient(sclient);
            }
        }

        // 新增：统一清理客户端资源
        static void CleanupClient(Socket sclient)
        {
            try
            {
                if (sclient.Connected)
                    sclient.Shutdown(SocketShutdown.Both);
                sclient.Close();
            }
            catch { }

            lock (userOnline)
            {
                userOnline.Remove(sclient);
            }

            string name = null;
            lock (socketToName)
            {
                if (socketToName.TryGetValue(sclient, out name))
                    socketToName.Remove(sclient);
            }

            if (name != null)
            {
                lock (userInfo)
                {
                    userInfo.RemoveAll(p => p.name == name);
                }
                Console.WriteLine($"[服务端] 已清理玩家: {name}");
            }
        }

        //这个子线程不断的允许新客户的登录
        static void waitClient()
        {
            //保存服务端地址
            IPEndPoint pos = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 54535);
            //端口0~65535，0~1024有特殊含义不建议用
            using (Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                //绑定地址
                listener.Bind(pos);
                //监听
                listener.Listen(10);
                Console.WriteLine("Listening...");
                //阻塞当前线程，等待，直到客户端连接，在客户端连接的时候返回一个新的socket

                //Accept和Receve这两个都是阻塞方法，执行到这个地方的时候都会等待，不会继续往下执行下去，直到客户端来连接或发消息才会继续往下执行，因此要用到多线程
                //Accept是等待客户端的连接，然后创建一个和这个客户端通信的Socket,因为不只是和一个客户端连接所以让他循环监听
                while (true)
                {
                    //这个socket才是包含双方地址，可以通过这个进行收发操作
                    Socket sclient = listener.Accept();
                    lock (userOnline)
                    {
                        //每监听到一个客户端的连接都需要把客户端保存下来，并且为这个客户创建子线程
                        userOnline.Add(sclient);
                    }
                    //每当有一个客户登录时还需要监听他的消息,不能只监听一条，也需要循环监听，同样需要开子线程,带参数的线程和普通线程不一样，带参数ParameterizedThreadStart，不带参数ThreadStart
                    Thread listen = new Thread(new ParameterizedThreadStart(listenClient));
                    //传入刚才用于创建连接的Socket
                    listen.Start(sclient);
                }
            }
        }

        //单独开一个子线程，自动发送
        static void autoSend()
        {
            //在这里是循环
            while (true)
            {
                lock (toDoListLock)
                {
                    if (userOnline.Count > 0)
                        toDoList.Enqueue(new Message("AllPlayerInfo", ""));
                }
                
                //设置发送周期,每五十毫秒也就是20帧
                Thread.Sleep(50);
            }
        }

        static void Main(string[] args)
        {
            //开启一个线程,在new ThreadStart（）里直接写方法名
            Thread wait = new Thread(new ThreadStart(waitClient));
            wait.Start();
            Thread auto = new Thread(new ThreadStart(autoSend));
            auto.Start();

            //主线程处理收到的消息也是不断进行
            while (true)
            {
                lock (toDoListLock)
                {
                    //如果队列里不是空，有东西
                    if (toDoList.Count > 0)
                    {
                        //就把消息拿出来
                        Message msg = toDoList.Dequeue();
                        //拿出来后开始执行，执行的时候根据消息类型去执行
                        switch (msg.type)
                        {
                            //接收这个消息类型的时候更新玩家信息
                            case "UpdatePlayerInfo":
                                //首先拿出玩家具体位置信息
                                PlayerInfo p = JsonConvert.DeserializeObject<PlayerInfo>(msg.info);
                                //标记一下没有找到，如果没找到就说明是新的玩家，新建一个
                                bool exists = false;
                                lock (userInfo)
                                {
                                    //在在线玩家信息列表里找
                                    for (int i = 0; i < userInfo.Count; i++)
                                    {
                                        //找到和刚发来信息的这个人同名的记录然后更新他
                                        if (userInfo[i].name == p.name)
                                        {
                                            userInfo[i] = p;
                                            exists = true;
                                            break;
                                        }
                                    }
                                    if (!exists) userInfo.Add(p);
                                }
                                //实现玩家位置信息的同步完成
                                break;
                            case "AllPlayerInfo":
                                //动全局变量的事情全部丢在主线程，容易出问题，不要在子线程做
                                //连接中给每一名玩家发送一条
                                List<PlayerInfo> currentPlayers;
                                lock (userInfo)
                                {
                                    currentPlayers = new List<PlayerInfo>(userInfo);
                                }
                                string json = JsonConvert.SerializeObject(currentPlayers);
                                List<Socket> clients;
                                lock (userOnline)
                                {
                                    clients = new List<Socket>(userOnline);
                                }
                                foreach (Socket sc in clients)
                                {
                                    //发送所有玩家userinfo列表
                                    SendMessage(sc, new Message("AllPlayerInfo", json));
                                }
                                break;
                        }
                    }
                }
            }
        }
    }
}