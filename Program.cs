// Server.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Threading;

class Server
{
    private TcpListener tcpListener;
    private List<ClientInfo> clients = new List<ClientInfo>();
    private object lockObject = new object();
    private string logFilePath = "log.txt"; // 日志文件路径
    private string serFilePath = "search.txt"; // 日志文件路径
    private string bannedUserFilePath = "ban.txt"; // 封禁用户文件路径
    private HashSet<string> bannedUsers;

    public Server(int port)
    {
        tcpListener = new TcpListener(IPAddress.Any, port);

        // 创建日志文件，如果不存在的话
        if (!File.Exists(logFilePath))
        {
            using (File.Create(logFilePath)) { }
        }
        // 创建搜索结果文件，如果不存在的话。
        if (!File.Exists(serFilePath))
        {
            using (File.Create(serFilePath)) { }
        }

        // 创建或读取封禁用户文件
        if (!File.Exists(bannedUserFilePath))
        {
            using (File.Create(bannedUserFilePath)) { }
        }
        bannedUsers = File.ReadAllLines(bannedUserFilePath).ToHashSet();
    }

    public void Start()
    {
        // 创建日志文件，如果不存在的话
        if (!File.Exists(logFilePath))
        {
            using (File.Create(logFilePath)) { }
        }
        // 创建搜索结果文件，如果不存在的话。
        if (!File.Exists(serFilePath))
        {
            using (File.Create(serFilePath)) { }
        }

        // 创建或读取封禁用户文件
        if (!File.Exists(bannedUserFilePath))
        {
            using (File.Create(bannedUserFilePath)) { }
        }
        Log("服务器已启动。");

        tcpListener.Start();

        // 启动一个新线程来处理控制台输入
        Thread consoleInputThread = new Thread(new ThreadStart(ReadConsoleInput));
        consoleInputThread.Start();

        while (true)
        {
            TcpClient tcpClient = tcpListener.AcceptTcpClient();

            // 用户连接到服务器的消息
            byte[] connectMessageBytes = new byte[8192];
            int bytesRead = tcpClient.GetStream().Read(connectMessageBytes, 0, 8192);
            string connectMessage = Encoding.UTF8.GetString(connectMessageBytes, 0, bytesRead);

            // 创建 ClientInfo 对象来保存客户端信息
            ClientInfo clientInfo = new ClientInfo { TcpClient = tcpClient, ConnectionMessage = connectMessage };

            // 检查是否在封禁列表中
            if (bannedUsers.Contains(clientInfo.Username))
            {
                Console.WriteLine($"拒绝连接封禁用户 '{clientInfo.Username}'。");
                tcpClient.Close();
                continue;
            }

            // 检查是否有重复用户名
            if (!IsUsernameAvailable(clientInfo.Username))
            {
                SendMessage(clientInfo, "该用户名已被使用，请选择其他用户名。");
                tcpClient.Close();
                continue;
            }

            lock (lockObject)
            {
                clients.Add(clientInfo);
            }

            // 广播新用户加入的消息两次
            BroadcastMessage($"{clientInfo.Username} 加入了服务器");
            BroadcastMessage($"{clientInfo.Username} 加入了服务器");

            // 启动一个新线程来处理客户端通信
            Thread clientThread = new Thread(new ParameterizedThreadStart(HandleClientComm));
            clientThread.Start(clientInfo);
        }
    }

    private bool IsUsernameAvailable(string username)
    {
        // 创建或读取封禁用户文件
        if (!File.Exists(bannedUserFilePath))
        {
            using (File.Create(bannedUserFilePath)) { }
        }
        lock (lockObject)
        {
            return !clients.Exists(c => c.Username == username);
        }
    }

    private void HandleClientComm(object clientInfoObj)
    {
        // 创建或读取封禁用户文件
        if (!File.Exists(bannedUserFilePath))
        {
            using (File.Create(bannedUserFilePath)) { }
        }
        ClientInfo clientInfo = (ClientInfo)clientInfoObj;
        TcpClient tcpClient = clientInfo.TcpClient;
        NetworkStream clientStream = tcpClient.GetStream();

        Log($"用户 '{clientInfo.Username}' 已连接。");

        byte[] message = new byte[8192];
        int bytesRead;

        while (true)
        {
            bytesRead = 0;

            try
            {
                bytesRead = clientStream.Read(message, 0, 8192);
            }
            catch
            {
                break;
            }

            if (bytesRead == 0)
                break;

            string data = Encoding.UTF8.GetString(message, 0, bytesRead);
            Console.WriteLine("接收自 " + clientInfo.Username + ": " + data);

            // 广播消息给所有客户端
            BroadcastMessage($"{clientInfo.Username}: {data}");
        }

        Console.WriteLine("用户 '" + clientInfo.Username + "' 断开了连接。");
        lock (lockObject)
        {
            clients.Remove(clientInfo);
        }
        BroadcastMessage($"{clientInfo.Username} 已离开聊天。");
        tcpClient.Close();
    }

    private void BroadcastMessage(string message)
    {
        // 创建或读取封禁用户文件
        if (!File.Exists(bannedUserFilePath))
        {
            using (File.Create(bannedUserFilePath)) { }
        }
        byte[] broadcastBytes = Encoding.UTF8.GetBytes(message);

        lock (lockObject)
        {
            foreach (var client in clients)
            {
                NetworkStream clientStream = client.TcpClient.GetStream();
                clientStream.Write(broadcastBytes, 0, broadcastBytes.Length);
                clientStream.Flush();
            }
        }

        // 记录广播的消息到日志文件
        Log(message);
    }

    private void SendMessage(ClientInfo clientInfo, string message)
    {
        // 创建或读取封禁用户文件
        if (!File.Exists(bannedUserFilePath))
        {
            using (File.Create(bannedUserFilePath)) { }
        }
        byte[] messageBytes = Encoding.UTF8.GetBytes(message);
        clientInfo.TcpClient.GetStream().Write(messageBytes, 0, messageBytes.Length);
        clientInfo.TcpClient.GetStream().Flush();
    }

    private void BanUser(string targetUsername)
    {
        try
        {
            // 使用 lock 以确保线程安全
            lock (lockObject)
            {
                // 创建或读取封禁用户文件
                if (!File.Exists(bannedUserFilePath))
                {
                    using (File.Create(bannedUserFilePath)) { }
                }

                if (!bannedUsers.Contains(targetUsername))
                {
                    bannedUsers.Add(targetUsername);

                    // 更新 ban.txt 文件
                    File.WriteAllLines(bannedUserFilePath, bannedUsers);

                    Console.WriteLine($"用户 '{targetUsername}' 已被封禁。");

                    // 尝试踢出被封禁用户
                    KickUser(targetUsername);

                    // 记录封禁操作到日志文件
                    Log($"用户 '{targetUsername}' 被管理员封禁。");
                }
                else
                {
                    Console.WriteLine($"用户 '{targetUsername}' 已经被封禁。");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"封禁用户时发生异常：{ex}");
            Log($"封禁用户时发生异常：{ex}");
        }
    }

    private void KickUser(string targetUsername)
    {
        try
        {
            // 使用 lock 以确保线程安全
            lock (lockObject)
            {
                ClientInfo targetClient = clients.FirstOrDefault(c => c.Username == targetUsername);

                if (targetClient != null)
                {
                    SendMessage(targetClient, "你已被管理员踢出服务器。");

                    // 发送被踢出的消息给其他用户
                    BroadcastMessage($"用户 '{targetUsername}' 被管理员踢出服务器。");

                    // 从客户端列表中移除被踢出的用户
                    clients.Remove(targetClient);

                    // 关闭与被踢出用户的连接
                    targetClient.TcpClient.Close();

                    // 记录被踢出的用户到日志文件
                    Log($"用户 '{targetUsername}' 被管理员踢出服务器。");
                }
                else
                {
                    // 如果用户不存在，记录无效用户的消息到日志文件
                    Log($"尝试踢出用户 '{targetUsername}'，但该用户不存在。");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"踢出用户时发生异常：{ex}");
            Log($"踢出用户时发生异常：{ex}");
        }
    }


    private void UnbanUser(string targetUsername)
    {
        // 创建或读取封禁用户文件
        if (!File.Exists(bannedUserFilePath))
        {
            using (File.Create(bannedUserFilePath)) { }
        }
        if (bannedUsers.Contains(targetUsername))
        {
            bannedUsers.Remove(targetUsername);

            // 更新 ban.txt 文件
            File.WriteAllLines(bannedUserFilePath, bannedUsers);

            Console.WriteLine($"用户 '{targetUsername}' 已被解封。");

            // 记录解封操作到日志文件
            Log($"用户 '{targetUsername}' 被管理员解封。");
        }
        else
        {
            Console.WriteLine($"用户 '{targetUsername}' 不在封禁列表中。");
        }
    }

    private void SearchLogs(string searchText, int searchCount)
    {
        // 创建或读取封禁用户文件
        if (!File.Exists(bannedUserFilePath))
        {
            using (File.Create(bannedUserFilePath)) { }
        }
        // 读取日志文件的所有行
        string[] logLines = File.ReadAllLines(logFilePath);

        // 找到包含搜索文本的行
        var matchingLines = logLines.Where(line => line.Contains(searchText)).Take(searchCount);

        // 将匹配的行写入 search.txt 文件
        File.WriteAllLines(serFilePath, matchingLines);
    }

    private void ReadConsoleInput()
    {
        // 创建或读取封禁用户文件
        if (!File.Exists(bannedUserFilePath))
        {
            using (File.Create(bannedUserFilePath)) { }
        }
        while (true)
        {
            string input = Console.ReadLine();

            if (input.StartsWith("/kick "))
            {
                string targetUsername = input.Substring("/kick ".Length);
                KickUser(targetUsername);
            }
            else if (input.StartsWith("/search"))
            {
                // 使用 Split 将输入按空格分割，获取第二和第三个元素作为搜索文本和搜索个数
                string[] inputParts = input.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (inputParts.Length >= 2)
                {
                    string searchText = inputParts[1];
                    int searchCount = 1; // 默认搜索个数为1

                    if (inputParts.Length == 3)
                    {
                        int.TryParse(inputParts[2], out searchCount);
                    }

                    SearchLogs(searchText, searchCount);
                }
                else
                {
                    Console.WriteLine("无效的命令。请使用格式: /search <搜索文本> [<搜索个数>]");
                }
            }
            else if (input.StartsWith("/ban "))
            {
                // 处理 BanUser 方法
                string targetUsername = input.Substring("/ban ".Length);
                BanUser(targetUsername);
            }
            else if (input.StartsWith("/dban "))
            {
                // 处理 UnbanUser 方法
                string targetUsername = input.Substring("/dban ".Length);
                UnbanUser(targetUsername);
            }
            else
            {
                Console.WriteLine("无效的命令。");
            }
        }
    }

    private void Log(string logMessage)
    {
        // 创建或读取封禁用户文件
        if (!File.Exists(bannedUserFilePath))
        {
            using (File.Create(bannedUserFilePath)) { }
        }
        // 记录日期、时间和日志消息到日志文件
        string formattedLog = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {logMessage}";
        File.AppendAllText(logFilePath, formattedLog + Environment.NewLine);

        // 在控制台显示服务器日志
        Console.WriteLine(formattedLog);
    }

    private class ClientInfo
    {

        public TcpClient TcpClient { get; set; }
        public string ConnectionMessage { get; set; }
        public string Username
        {
            get
            {
                // 从连接消息中解析用户名
                return ConnectionMessage.Split(' ')[0];
            }
        }
    }
}

class Program
{
    static void Main()
    {
        Console.Write("请输入服务器端口号: ");
        int port = int.Parse(Console.ReadLine());

        Server server = new Server(port);
        server.Start();
    }
}
