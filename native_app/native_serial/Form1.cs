using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using SuperSocket.WebSocket;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Net.Sockets;
using System.Net;
using System.Configuration;
using System.IO;
using System.Drawing.Imaging;
using System.Security.Authentication;
using Microsoft.Win32;

namespace native_serial
{
    public partial class Form1 : Form
    {
        public static Form1 frm;
        public Dictionary<string, WebSocketSession> session_ary = new Dictionary<string, WebSocketSession>();
        SuperSocket.WebSocket.WebSocketServer server_ssl;

        private List<string> log_ary = new List<string>();

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {

            frm = null;

            if(server_ssl != null && server_ssl.State == SuperSocket.SocketBase.ServerState.Running)
            {
                server_ssl.Stop();
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

            frm = this;


            try
            {
                var server_config_ssl = new SuperSocket.SocketBase.Config.ServerConfig()
                {
                    Port = Properties.Settings.Default.port,
                    Ip = "127.0.0.1",
                    MaxConnectionNumber = 100,
                    Mode = SuperSocket.SocketBase.SocketMode.Tcp,
                    Name = "SuperSocket.WebSocket Sample Server",
                    MaxRequestLength = 1024 * 1024 * 10
                };

                setup_server(ref server_ssl, server_config_ssl);

                this.serialPort1.PortName = ConfigurationManager.AppSettings["port_name"];

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());

            }
        }

        private void setup_server(ref WebSocketServer server, SuperSocket.SocketBase.Config.ServerConfig serverConfig)
        {
            var rootConfig = new SuperSocket.SocketBase.Config.RootConfig();

            server = new SuperSocket.WebSocket.WebSocketServer();

            //サーバーオブジェクト作成＆初期化
            server.Setup(rootConfig, serverConfig);

            //イベントハンドラの設定
            //接続
            server.NewSessionConnected += HandleServerNewSessionConnected;
            //メッセージ受信
            server.NewMessageReceived += HandleServerNewMessageReceived;
            //切断        
            server.SessionClosed += HandleServerSessionClosed;

            //サーバー起動
            server.Start();

        }

        static void HandleServerNewSessionConnected(SuperSocket.WebSocket.WebSocketSession session)
        {
            frm.session_ary.Add(session.SessionID, session);

            frm.Invoke((MethodInvoker)delegate ()
            {
                frm.add_log(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), "接続");
            });

        }

        //メッセージ受信
        static void HandleServerNewMessageReceived(SuperSocket.WebSocket.WebSocketSession session,
                                                    string e)
        {
            frm.Invoke((MethodInvoker)delegate ()
            {
                MessageData recv = JsonConvert.DeserializeObject<MessageData>(e);

                switch (recv.command)
                {
                    case "send_command":

                        frm.Invoke((MethodInvoker)delegate ()
                        {
                            var send_txt = recv.message;

                            if (!frm.serialPort1.IsOpen)
                            {
                                frm.serialPort1.Open();
                            }
                            frm.serialPort1.Write(send_txt);
                            frm.add_log(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), "送信:" + send_txt);

                        });

                        break;
                    case "end_capture":

                        break;
                }

            });

        }

        //切断
        static void HandleServerSessionClosed(SuperSocket.WebSocket.WebSocketSession session,
                                                    SuperSocket.SocketBase.CloseReason e)
        {
            if (frm != null)
            {
                frm.session_ary.Remove(session.SessionID);

                frm.Invoke((MethodInvoker)delegate ()
                {
                    frm.add_log(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), "切断");
                });
            }
        }

        public void add_log(string time, string log)
        {
            log_ary.Add("[" + time + "] " + log);

            if( log_ary.Count > 1000)
            {
                log_ary.RemoveAt(0);
            }

            this.txtLog.Lines = log_ary.ToArray();

        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (!this.serialPort1.IsOpen)
            {
                this.serialPort1.Open();
            }
            if (this.serialPort1.IsOpen)
            {
                var send_txt = this.txtSend.Text;

                this.serialPort1.Write(send_txt);

                this.add_log(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), "送信:" + send_txt);
            }
        }

        private void serialPort1_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            var recieved_data = frm.serialPort1.ReadExisting();

            MessageData send = new MessageData();

            send.command = "recieved_command";
            send.message = recieved_data;

            var send_str = JsonConvert.SerializeObject(send);

            foreach (var session in session_ary.Values)
            {
                session.Send(send_str);
            }

            frm.add_log(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), "受信:" + recieved_data);
        }
    }
}
