using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Messages;

namespace SocketBus
{
    public class SocketServiceBusClient : SocketServiceBus, ISocketServiceBusClient
    {
        private static readonly ManualResetEvent ConnectDone = new ManualResetEvent(false);
        private static readonly ManualResetEvent ReceiveDone = new ManualResetEvent(false);

        private Socket _client;
        private readonly string _remoteEndpoint;

        public SocketServiceBusClient(string remoteEndpoint)
        {
            _remoteEndpoint = remoteEndpoint;
        }

        public void Start()
        {
            ConsoleInfo("Starting client, connecting to " + _remoteEndpoint);

            var index = _remoteEndpoint.IndexOf(":", StringComparison.Ordinal);
            var hostname = _remoteEndpoint.Substring(0, index);
            var port = Convert.ToInt32(_remoteEndpoint.Substring(index + 1));

            var ipHostInfo = Dns.GetHostEntry(hostname);

            var ipAddress = ipHostInfo.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            if (ipAddress == null)
            {
                throw new Exception("Could not get ip4 address!");
            }
            var remoteEp = new IPEndPoint(ipAddress, port);

            _client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _client.BeginConnect(remoteEp, ConnectCallback, _client);
            ConnectDone.WaitOne();

            Task.Factory.StartNew(ReceiveHandler);
        }

        public void Stop()
        {
            _client.Shutdown(SocketShutdown.Both);
            _client.Close();
        }

        public void Send<T>(Message<T> message)
        {
            Send(_client, message);
        }

        private void ReceiveHandler()
        {
            while (true)
            {
                ReceiveDone.Reset();
                Receive(_client);
                ReceiveDone.WaitOne();
            }
            // ReSharper disable once FunctionNeverReturns
        }


        private static void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                var client = (Socket) ar.AsyncState;

                // Complete the connection.
                client.EndConnect(ar);

                ConsoleInfo("Connected to " + client.RemoteEndPoint);

                // Signal that the connection has been made.
                ConnectDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void Receive(Socket client)
        {
            try
            {
                // Create the state object.
                var state = new StateObject {WorkSocket = client};

                // Begin receiving the data from the remote device.
                client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, ReceiveCallback, state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        protected override void HandleLostConnection(Socket socket, SocketException se)
        {
            ConsoleError("Client lost connection with server! " + se.Message);
            Thread.Sleep(1000);
            ConsoleInfo("Reconnecting...");
            Start();
        }
    }
}