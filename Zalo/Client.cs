using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Windows.Forms;

namespace Zalo
{
    public partial class Client : Form
    {
        private IPEndPoint IP;
        private Socket client;
        private string UName;
        public Client(string name)
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
            Connect();
            lblWelcomeMessage.Text = $"Xin chào {name}!";
            UName = name;
            string onlineMessage = $"Connect:{name}";
            client.Send(Serialize(onlineMessage));

        }

        private void Connect()
        {
            IP = new IPEndPoint(IPAddress.Parse("192.168.141.158"), 9999);
            client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            try
            {
                client.Connect(IP);
            }
            catch
            {
                MessageBox.Show("Không thể kết nối đến Server!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Thread listen = new Thread(Receive);
            listen.IsBackground = true;
            listen.Start();
        }

        private void Send(string message)
        {
            string messageWithPrefix;

            if (message.Contains("pr:"))
            {
                messageWithPrefix = "PrivateChat:" + message;
                client.Send(Serialize(messageWithPrefix));
            }
            else if(message.Contains("ad:"))
            {
                messageWithPrefix = "AdminChat:" + message;
                client.Send(Serialize(messageWithPrefix));
            }
            else
            {
                messageWithPrefix = "PublicChat:" + message;
                client.Send(Serialize(messageWithPrefix));
            }
        }

        private void Receive()
        {
            try
            {
                Dictionary<string, bool> onlineList = null;

                while (true)
                {
                    byte[] data = new byte[1024 * 5000];
                    client.Receive(data);
                    string message = (string)Deserialize(data);
                    if (message.StartsWith("OnlineList:"))
                    {
                        string json = message.Substring(11);
                        onlineList = JsonConvert.DeserializeObject<Dictionary<string, bool>>(json);
                        UpdateOnlineList(onlineList);
                    }
                    else if (message.StartsWith("PrivateChat:"))
                    {
                        string[] parts = message.Split(':');
                        string senderName = parts[1]; 
                        string recipientname = parts[3];
                        string content = parts[4];
                        AddMessage(senderName + "(🕵 to) " + recipientname + ": " + content);       
                    }
                    else
                    {
                        AddMessage(message);
                    }

                    if (onlineList != null)
                    {
                        UpdateOnlineList(onlineList);
                    }
                }
            }
            catch
            {
                client.Close();
            }
        }

        void UpdateOnlineList(Dictionary<string, bool> onlineList)
        {
            // Xóa tất cả các dòng hiện tại
            this.Invoke((MethodInvoker)delegate {
                dgvOnlineList.Rows.Clear();

                // Thêm mỗi người dùng đang online vào DataGridView
                foreach (KeyValuePair<string, bool> entry in onlineList)
                {
                    string userName = entry.Key;
                    DataGridViewRow row = new DataGridViewRow();
                    row.CreateCells(dgvOnlineList); // Tạo cells cho row với cấu trúc giống DataGridView
                    row.Cells[0].Value = userName; // Tên người dùng
                    row.Cells[1].Value = "Online";
                    row.Cells[1].Style.ForeColor = Color.Green; // Màu xanh lá cây cho trạng thái online

                    // Thêm row vào DataGridView
                    dgvOnlineList.Rows.Add(row);
                }
            });
        }

        private void AddMessage(string s)
        {
            lsvMessage.Items.Add(new ListViewItem() { Text = s });
            txtMessage.Clear();
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

        private void btnSend_Click(object sender, EventArgs e)
        {
            string message = $"{UName}: {txtMessage.Text}";
            Send(message);
            if (message.Contains("pr:")){
                string[] parts = message.Split(':');
                string sendername = parts[0];
                string recipientname = parts[2];
                string content = parts[3];
                AddMessage(sendername + "(🕵 to) " + recipientname + ": " + content);
            }
            else if (message.Contains("ad:"))
            {
                string[] parts = message.Split(':');
                string username = parts[0];
                string content = parts[2];
                AddMessage(username + "(🕵 to) Admin: " + content);
            }
            else
                AddMessage(message); 
        }

        private void Client_FormClosed(object sender, FormClosedEventArgs e)
        {
            client.Close();
            Application.Exit();
        }
    }
}