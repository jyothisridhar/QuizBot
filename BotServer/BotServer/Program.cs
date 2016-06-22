using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BotServer 
{
    class Program
    {
        private static byte[] _buffer = new byte[1024];
        private static List<Socket> _clientSockets = new List<Socket>();
        private static Socket _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        static void Main(string[] args)
        {
            Console.Title = "Server";
            SetUpServer();
            Console.ReadLine();
        }

        private static void SetUpServer()
        {
            Console.WriteLine("Setting up server...");
            _serverSocket.Bind(new IPEndPoint(IPAddress.Any, 3500));
            _serverSocket.Listen(5);
            _serverSocket.BeginAccept(new AsyncCallback(AcceptCallBack), null);
        }

        private static void AcceptCallBack(IAsyncResult AR)
        {
            Socket socket = _serverSocket.EndAccept(AR);
            _clientSockets.Add(socket);
            Console.WriteLine("Client connected");
            socket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallBack), socket);
            _serverSocket.BeginAccept(new AsyncCallback(AcceptCallBack), null);
        }

        private static void ReceiveCallBack(IAsyncResult AR)
        {
            Socket socket = (Socket)AR.AsyncState;
            SocketError errorCode;
            int received = socket.EndReceive(AR, out errorCode);
            if (errorCode != SocketError.Success)
            {
                received = 0;
            }
            byte[] dataBuf = new byte[received];
            Array.Copy(_buffer, dataBuf, received);

            string text = Encoding.ASCII.GetString(dataBuf);

            string response = string.Empty;
            byte[] data = new byte[1024];

            if (new Regex("^GET").IsMatch(text))
            {
                Console.WriteLine("Upgrade heaeder received: " + text);
                Console.WriteLine("handshake response:");
                response = "HTTP/1.1 101 Switching Protocols" + Environment.NewLine
                    + "Connection: Upgrade" + Environment.NewLine
                    + "Upgrade: websocket" + Environment.NewLine
                    + "Sec-WebSocket-Accept: " + Convert.ToBase64String(
                        System.Security.Cryptography.SHA1.Create().ComputeHash(
                            Encoding.UTF8.GetBytes(
                                new Regex("Sec-WebSocket-Key: (.*)").Match(text).Groups[1].Value.Trim() + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
                            )
                        )
                    ) + Environment.NewLine
                    + Environment.NewLine;

                Console.WriteLine(response);
                data = Encoding.ASCII.GetBytes(response);
            }
            else
            {
                int offset = 0;
                int len = dataBuf[offset + 1] - 0x80;
                Console.WriteLine("len in decode function : " + len);
                Byte[] decodedMsg = new Byte[len];

                while (offset + 6 < dataBuf.Length)
                {
                    Byte[] key = new Byte[] { dataBuf[offset + 2], dataBuf[offset + 3], dataBuf[offset + 4], dataBuf[offset + 5] };
                    for (int i = 0; i < len; i++)
                    {
                        int realPos = offset + 6 + i;
                        decodedMsg[i] = (Byte)(dataBuf[realPos] ^ key[i % 4]);
                    }
                    offset += 6 + len;
                }

                string decodedData = System.Text.Encoding.ASCII.GetString(decodedMsg);

                Console.WriteLine("Message recieved from client: " + decodedData);

                switch (decodedData.ToLower())
                {
                    case "start quiz" :
                        BotServer.Quiz.loadJson();
                        response = "  Choose category:" + Environment.NewLine + "a) Science" + Environment.NewLine +
                                   "b) Sports" + Environment.NewLine + "c) GK";
                        break;

                    case "science" :
                        break;
                    case "a" :
                    case "b" :
                    case "c" :
                    case "d" :


                    default: response = "  Invalid request"; 
                        break;
                }

                data = System.Text.Encoding.ASCII.GetBytes(response);
                data[0] = 0x81; // denotes this is the final message and it is in text
                data[1] = (byte)(data.Length - 2);

                Console.WriteLine("sending: " + response);
            }

            socket.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(SendCallback), socket);
            socket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallBack), socket);
        }

        private static void SendCallback(IAsyncResult AR)
        {
            Socket socket = (Socket)AR.AsyncState;
            socket.EndSend(AR);
        }

    }
}
