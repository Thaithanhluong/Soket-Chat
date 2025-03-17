using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Zalo_Server
{
    public partial class Server : Form
    {
        private IPEndPoint IP;
        private Socket server;
        private List<Socket> clientList;
        public Server()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
            Connect();
        }
        void Connect()
        {
            clientList = new List<Socket>();
            IP = new IPEndPoint(IPAddress.Any, 9999);
            server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            server.Bind(IP);

            Thread Listen = new Thread(() =>
            {
                try
                {
                    while (true)
                    {
                        server.Listen(100);
                        Socket client = server.Accept();
                        clientList.Add(client);
                        Thread receive = new Thread(Receive);
                        receive.IsBackground = true;
                        receive.Start(client);
                    }
                }
                catch
                {
                    IP = new IPEndPoint(IPAddress.Any, 9999);
                    server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                }
            });
            Listen.IsBackground = true;
            Listen.Start();
        }

        private Socket FindClientByName(string name)
        {
            if (userSocketMap.ContainsKey(name))
            {
                return userSocketMap[name];
            }
            else
            {
                return null;
            }
        }
        // Khởi tạo userSocketMap
        private Dictionary<string, Socket> userSocketMap = new Dictionary<string, Socket>();

        // Khi một người dùng kết nối, thêm họ vào userSocketMap
        private void OnUserConnected(string userName, Socket userSocket)
        {
            userSocketMap[userName] = userSocket;
            UpdateDataGridView(); // Cập nhật DataGridView
            SendOnlineList();
        }

        // Khi một người dùng ngắt kết nối, xóa họ khỏi userSocketMap
        private void OnUserDisconnected(string userName)
        {
            userSocketMap.Remove(userName);
            UpdateDataGridView(); // Cập nhật DataGridView
            SendOnlineList();
        }
        private void Receive(object obj)
        {
            Socket client = obj as Socket;
            string userName = "";
            try
            {
                while (true)
                {
                    byte[] data = new byte[1024 * 5000];
                    client.Receive(data);
                    string message = (string)Deserialize(data);
                    if (message.StartsWith("Connect:"))
                    {
                        userName = message.Substring("Connect:".Length);
                        OnUserConnected(userName, client);
                    }
                    else if (message.StartsWith("PrivateChat:"))
                    {
                        // Gửi tin nhắn đến một client cụ thể
                        string[] parts = message.Split(':');
                        string sendername = parts[1];
                        string recipientName = parts[3]; // Người nhận

                        Socket recipient = FindClientByName(recipientName);
                        if (recipient != null)
                        {
                            recipient.Send(Serialize(message));
                        }
                        else
                        {
                            Socket sender = FindClientByName(sendername);
                            sender.Send(Serialize(recipientName + " KHÔNG ONLINE!"));
                        }
                    }
                    else if (message.StartsWith("AdminChat:"))
                    {
                        string[] parts = message.Split(':');
                        string sendername = parts[1];
                        string content = parts[3];
                        AddMessage(sendername + "(🕵 to) Admin: " + content);
                    }
                    else
                    {
                        string publicMessage = message.Substring("PublicChat:".Length);
                        foreach (Socket item in clientList)
                        {
                            if (item != null && item != client)
                                item.Send(Serialize(publicMessage));
                        }
                        AddMessage(publicMessage);
                    }
                }
            }
            catch
            {
                clientList.Remove(client);
                client.Close();
                OnUserDisconnected(userName);
            }
        }

        private void UpdateDataGridView()
        {
            // Xóa tất cả các dòng hiện tại
            dgvOnlineList.Rows.Clear();

            // Thêm mỗi người dùng đang online vào DataGridView
            foreach (var entry in userSocketMap)
            {
                string userName = entry.Key;
                DataGridViewRow row = new DataGridViewRow();
                row.CreateCells(dgvOnlineList); // Tạo cells cho row với cấu trúc giống DataGridView
                row.Cells[0].Value = userName;
                row.Cells[1].Value = "Online";
                row.Cells[1].Style.ForeColor = Color.Green;

                // Thêm row vào DataGridView
                dgvOnlineList.Rows.Add(row);
            }
        }
        private void AddMessage(string s)
        {
            lsvMessage.Items.Add(new ListViewItem() { Text = s });
            txbMessage.Clear();
        }
        private void SendOnlineList()
        {
            if (dgvOnlineList.Rows.Count > 0)
            {
                Dictionary<string, bool> onlineList = new Dictionary<string, bool>();
                foreach (DataGridViewRow row in dgvOnlineList.Rows)
                {
                    string userName = row.Cells["UserName"].Value.ToString();
                    bool isSender = true;
                    onlineList.Add(userName, isSender);
                }
                string onlineListMessage = $"OnlineList: {JsonConvert.SerializeObject(onlineList)}";
                foreach (Socket client in clientList)
                {
                    client.Send(Serialize(onlineListMessage));
                }
            }
        }

        private byte[] Serialize(object obj)
        {
            MemoryStream stream = new MemoryStream();
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(stream, obj);
            return stream.ToArray();
        }

        private object Deserialize(byte[] obj)
        {
            using (MemoryStream stream = new MemoryStream(obj))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                return formatter.Deserialize(stream);
            }
        }

        private void Server_FormClosing(object sender, FormClosingEventArgs e)
        {
            string closeMessage = "Server đã đóng...";
            SendToAllClients(closeMessage);
            foreach (Socket client in clientList)
            {
                // Tìm tên người dùng tương ứng với socket client
                string userName = userSocketMap.FirstOrDefault(x => x.Value == client).Key;
                if (userName != null)
                {
                    OnUserDisconnected(userName); // Gọi hàm OnUserDisconnected khi server đóng
                }
            }
        }

        private void SendToAllClients(string message)
        {

            if (message.Contains("pr:"))
            {
                string[] parts = message.Split(':');
                string recipientName = parts[2];
                string content = parts[3];
                Socket recipient = FindClientByName(recipientName);
                if (recipient != null)
                {
                    recipient.Send(Serialize("Admin: " + "(🕵): " + content));
                }
            }
            else
            {
                foreach (Socket item in clientList)
                {
                    item.Send(Serialize(message));
                }
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            string message = $"Admin: {txbMessage.Text}";
            SendToAllClients(message);
            if (message.Contains("pr:"))
            {
                string[] parts = message.Split(':');
                string content = parts[3];
                AddMessage("Admin: " + "(🕵): " + content);
            }
            else
                AddMessage(message);
        }

        private void Server_FormClosed(object sender, FormClosedEventArgs e)
        {
            server.Close();
            Application.Exit();
        }
    }
}
