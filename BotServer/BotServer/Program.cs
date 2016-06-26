using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace BotServer
{
    public class StateObject
    {
        public Socket workSocket = null;
        public const int bufferSize = 1024;
        public byte[] buffer = new byte[bufferSize];
        public StringBuilder sb = new StringBuilder();
    }

    public class AsynchronousSocketListener
    {
        public AsynchronousSocketListener()
        { }

        //private static byte[] _buffer = new byte[1024];
        public static ManualResetEvent allDone = new ManualResetEvent(false);
        private static List<Socket> _clientSockets = new List<Socket>();
        public static bool _isRunning = true;

        public static void Main(string[] args)
        {
            Console.Title = "Server";
            StartListening();
            Console.ReadLine();
        }

        private static void StartListening()
        {
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                serverSocket.Bind(new IPEndPoint(IPAddress.Any, 3500));

                while (_isRunning)
                {
                    allDone.Reset();
                    serverSocket.Listen(100);
                    Console.WriteLine("Waiting for a connection...");
                    serverSocket.BeginAccept(new AsyncCallback(AcceptCallBack), serverSocket);
                    allDone.WaitOne();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

        }

        private static void AcceptCallBack(IAsyncResult AR)
        {
            Socket listener = (Socket)AR.AsyncState;
            Socket socket = listener.EndAccept(AR);
            allDone.Set();
            _clientSockets.Add(socket);

            StateObject state = new StateObject();
            state.workSocket = socket;
            Console.WriteLine("Client connected");
            socket.BeginReceive(state.buffer, 0, state.buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallBack), state);
        }

        private static void ReceiveCallBack(IAsyncResult AR)
        {
            String text = String.Empty;

            StateObject state = (StateObject)AR.AsyncState;
            Socket socket = state.workSocket;

            int received = socket.EndReceive(AR);
            byte[] dataBuf = new byte[received];

            string response = string.Empty;
            byte[] data = new byte[1024];

            if (received > 0)
            {
                state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, received));
                Array.Copy(state.buffer, dataBuf, received);
                //text = Encoding.ASCII.GetString(dataBuf);
                text = state.sb.ToString();
                if (new Regex("^GET").IsMatch(text))
                {
                    Console.WriteLine("Upgrade header received: " + text);
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
                    socket.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(SendCallback), state);
                }

                else
                {
                    List<Byte[]> decodedMsg = decodeWebsocketFrame(dataBuf);
                    string decodedData = System.Text.Encoding.ASCII.GetString(decodedMsg[0]);

                    if (decodedData.Contains("<EOF>"))
                    {

                        char[] trim = { '<', 'E', 'O', 'F', '>' };
                        if (decodedData.Contains("<EOF>"))
                            decodedData = decodedData.TrimEnd(trim);
                        Console.WriteLine("Message recieved from client: " + decodedData);
                        string userInput = decodedData.ToLower();

                        switch (userInput)
                        {
                            case "start quiz":
                                response = "  Choose category:" + Environment.NewLine + "a) Science" + Environment.NewLine +
                                           "b) Sports" + Environment.NewLine + "c) GK";
                                break;

                            case "science":
                            case "gk":
                            case "sports":
                                Quiz.loadJson(userInput + ".js");
                                Quiz._newQuiz = true;
                                response = Quiz.GetQuestion(Quiz._question_no++);
                                break;

                            case "a":
                            case "b":
                            case "c":
                            case "d":
                                //response = Quiz.ValidateAnswer(userInput);
                                response = Quiz.GetQuestion(Quiz._question_no++);
                                break;

                            case "end":
                                response = Quiz.ShowResult();
                                break;

                            default: response = "  Sorry I could not understand.";
                                break;
                        }

                        data = EncodeMessageToSend(response);

                        Console.WriteLine("sending: " + response);
                        socket.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(SendCallback), state);
                    }
                    else
                    {
                        socket.BeginReceive(state.buffer, 0, state.buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallBack), state);
                    }
                }
            }
            else
            {
                socket.Close();
            }
        }

        private static List<byte[]> decodeWebsocketFrame(Byte[] bytes)
        {
            List<Byte[]> ret = new List<Byte[]>();
            int offset = 0;
            while (offset + 6 < bytes.Length)
            {
                // format: 0==ascii/binary 1=length-0x80, byte 2,3,4,5=key, 6+len=message, repeat with offset for next...
                int len = bytes[offset + 1] - 0x80;

                Byte[] key = new Byte[] { bytes[offset + 2], bytes[offset + 3], bytes[offset + 4], bytes[offset + 5] };
                Byte[] decoded = new Byte[len];
                for (int i = 0; i < len; i++)
                {
                    int realPos = offset + 6 + i;
                    decoded[i] = (Byte)(bytes[realPos] ^ key[i % 4]);
                }
                offset += 6 + len;
                ret.Add(decoded);
            }
            return ret;
        }

        private static Byte[] EncodeMessageToSend(String message)
        {
            Byte[] response;
            Byte[] bytesRaw = Encoding.UTF8.GetBytes(message);
            Byte[] frame = new Byte[10];

            Int32 indexStartRawData = -1;
            Int32 length = bytesRaw.Length;

            frame[0] = (Byte)129;
            if (length <= 125)
            {
                frame[1] = (Byte)length;
                indexStartRawData = 2;
            }
            else if (length >= 126 && length <= 65535)
            {
                frame[1] = (Byte)126;
                frame[2] = (Byte)((length >> 8) & 255);
                frame[3] = (Byte)(length & 255);
                indexStartRawData = 4;
            }
            else
            {
                frame[1] = (Byte)127;
                frame[2] = (Byte)((length >> 56) & 255);
                frame[3] = (Byte)((length >> 48) & 255);
                frame[4] = (Byte)((length >> 40) & 255);
                frame[5] = (Byte)((length >> 32) & 255);
                frame[6] = (Byte)((length >> 24) & 255);
                frame[7] = (Byte)((length >> 16) & 255);
                frame[8] = (Byte)((length >> 8) & 255);
                frame[9] = (Byte)(length & 255);

                indexStartRawData = 10;
            }

            response = new Byte[indexStartRawData + length];

            Int32 i, reponseIdx = 0;

            //Add the frame bytes to the reponse
            for (i = 0; i < indexStartRawData; i++)
            {
                response[reponseIdx] = frame[i];
                reponseIdx++;
            }

            //Add the data bytes to the response
            for (i = 0; i < length; i++)
            {
                response[reponseIdx] = bytesRaw[i];
                reponseIdx++;
            }

            return response;
        }

        private static void SendCallback(IAsyncResult AR)
        {
            try
            {
                StateObject state = (StateObject)AR.AsyncState;
                Socket socket = state.workSocket;
                int bytesSent = socket.EndSend(AR);

                StateObject newstate = new StateObject();
                newstate.workSocket = socket;
                socket.BeginReceive(newstate.buffer, 0, StateObject.bufferSize, SocketFlags.None, new AsyncCallback(ReceiveCallBack), newstate);
                //socket.BeginReceive(state.buffer, 0, StateObject.bufferSize, SocketFlags.None, new AsyncCallback(ReceiveCallBack), state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

    }
}