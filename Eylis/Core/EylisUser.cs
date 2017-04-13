﻿
namespace Eylis.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Net.Sockets;
    using System.Net;
    using System.Threading;
    using System.IO;
    using Eylis.Core.Protocol;

    public class EylisUser
    {
        public class ReceiveEventArgs : EventArgs
        {
            internal protected ReceiveEventArgs(EylisMessage message)
                => this.Message = message;
            public EylisMessage Message { get; private set; }
        }

        public override int GetHashCode()
            => this.Uid.GetHashCode();

        public override bool Equals(object obj)
            => (obj is EylisUser) ? (obj as EylisUser).GetHashCode() == this.GetHashCode() : base.Equals(this);

        public delegate void ConnectEventHandler(EylisUser user);
        internal event ConnectEventHandler OnConnecting;

        public delegate void DisconnectEventHandler(EylisUser user);
        internal event DisconnectEventHandler OnDisconnecting;

        public delegate void ReceiveEventHandler(EylisUser sender, ReceiveEventArgs e);
        internal event ReceiveEventHandler OnReceived;

        public static EylisUser Connect(EylisConfig config, ReceiveEventHandler onReceive = null, ConnectEventHandler onConnecting = null , DisconnectEventHandler onDisconnecting = null )
        {
            var tcpClient = new TcpClient();
            tcpClient.Connect(config.Host, config.Port);
            var user = new EylisUser(tcpClient,onReceive,onConnecting,onDisconnecting);
            return user;
        }

        internal EylisUser(TcpClient client, ReceiveEventHandler onReceive = null, ConnectEventHandler onConnecting = null, DisconnectEventHandler onDisconnecting = null)
        {
            this.OnReceived += onReceive;
            this.OnConnecting += onConnecting;
            this.OnDisconnecting += onDisconnecting;
            this.IsConnected = false;
            this.Uid = Guid.NewGuid();
            this.client = client;
            this.ns = client.GetStream();
            this.reader = new StreamReader(this.ns, this.Encoding);
            this.writer = new StreamWriter(this.ns, this.Encoding);
            this.token = new CancellationTokenSource();
        }

        public bool IsConnected { get; private set; }
        public readonly Guid Uid;
        public EndPoint RemoteEndPoint => this.client?.Client?.RemoteEndPoint;
        public Encoding Encoding => Encoding.UTF8;

        private NetworkStream ns;
        private StreamReader reader;
        private StreamWriter writer;
        private CancellationTokenSource token;
        private TcpClient client;

        public void Send(EylisMessage message)
        {
            try
            {
                writer.WriteLine(message);
                writer.Flush();
            }
            catch
            {
                this.Disconnect();
            }
        }
        public void Disconnect()
        {
            this.OnDisconnecting?.Invoke(this);
            if (!this.token.IsCancellationRequested)
                this.token.Cancel(false);
            if (this.client.Connected)
                this.client.Close();
            this.IsConnected = false;
        }
        public void Connect()
        {
            if (!this.IsConnected)
            {
                this.IsConnected = true;
                Task.Run(() =>
                {
                    try
                    {
                        this.OnConnecting?.Invoke(this);
                        while (true)
                        {
                            var data = reader.ReadLine();
                            if (data != null & data.Trim().Equals(string.Empty))
                                this.OnReceived?.Invoke(this, new ReceiveEventArgs(data));
                        }
                    }
                    catch
                    {
                        this.Disconnect();
                    }
                }, this.token.Token);
            }
        }

    }
}

