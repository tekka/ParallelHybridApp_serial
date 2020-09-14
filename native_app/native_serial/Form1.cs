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

namespace barcode
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
                    MaxRequestLength = 1024 * 1024 * 10,
                    Security = GetEnabledTlsVersions(),
                    Certificate = new SuperSocket.SocketBase.Config.CertificateConfig
                    {
                        FilePath = ConfigurationManager.AppSettings["cert_file_path"],
                        Password = ConfigurationManager.AppSettings["cert_password"]
                    }
                };

                setup_server(ref server_ssl, server_config_ssl);

                valid_cert();

                this.serialPort1.PortName = ConfigurationManager.AppSettings["port_name"];

            }
            catch (Exception ex)
            {
                reflesh_cert();

                MessageBox.Show("証明書を更新しました。\nアプリケーションを再起動します。");

                Application.Restart();
            }
        }

        public static string GetEnabledTlsVersions()
        {
            var enabledProtocols = SslProtocols.None;

            try
            {
                var protocols = new[]
                {
            (key: @"SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\SSL 2.0\Server", protocol: SslProtocols.Ssl2),
            (key: @"SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\SSL 3.0\Server", protocol: SslProtocols.Ssl3),
            (key: @"SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.0\Server", protocol: SslProtocols.Tls),
            (key: @"SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.1\Server", protocol: SslProtocols.Tls11),
            (key: @"SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Server", protocol: SslProtocols.Tls12)
        };

                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                {
                    foreach (var protocol in protocols)
                    {
                        using (var key = baseKey.OpenSubKey(protocol.key, false))
                        {
                            var value = key?.GetValue("DisabledByDefault") ?? 0;
                            if (value is int disabled && disabled != 0)
                            {
                                continue;
                            }

                            value = key?.GetValue("Enabled") ?? 1;
                            if (value is int enabled && enabled != 0)
                            {
                                enabledProtocols |= protocol.protocol;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // Log Error: "Failed to load enabled SSL protocols"
            }

            if (enabledProtocols == SslProtocols.None)
            {
                enabledProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;
            }

            return enabledProtocols.ToString();
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

        private static Boolean RemoteCertificateValidationCallback(Object sender,
        X509Certificate certificate,
        X509Chain chain,
        SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            return false;
        }

        private void valid_cert()
        {
            String hostName = ConfigurationManager.AppSettings["cert_local_host"];
            Int32 port = Properties.Settings.Default.port;

            using (TcpClient client = new TcpClient())
            {
                //接続先Webサーバー名からIPアドレスをルックアップ    
                IPAddress[] ipAddresses = Dns.GetHostAddresses(hostName);

                //Webサーバーに接続する
                client.Connect(new IPEndPoint(ipAddresses[0], port));

                //SSL通信の開始
                using (SslStream sslStream =
                    new SslStream(client.GetStream(), false, RemoteCertificateValidationCallback))
                {
                    //サーバーの認証を行う
                    //これにより、RemoteCertificateValidationCallbackメソッドが呼ばれる
                    sslStream.AuthenticateAsClient(hostName);
                }
            }
        }

        private void reflesh_cert()
        {
            //証明書の更新

            var cert_file_url = ConfigurationManager.AppSettings["cert_file_url"];
            var cert_file_path = ConfigurationManager.AppSettings["cert_file_path"];

            var wc = new WebClient();
            wc.DownloadFile(cert_file_url, cert_file_path);
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
