using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UnderAvatar
{
    // Length-prefixed UTF-8 frames, bounded buffers and nonblocking I/O on the Unity thread.
    public sealed class LanFrameDecoder
    {
        public const int MaxFrame=16*1024*1024;
        readonly byte[] header=new byte[4];
        int headerCount,bodyCount;
        byte[] body;
        public readonly Queue<string> Messages=new Queue<string>();
        public void Push(byte[] bytes,int count)
        {
            int offset=0;
            while(offset<count)
            {
                if(body==null)
                {
                    int copy=Math.Min(4-headerCount,count-offset);
                    Array.Copy(bytes,offset,header,headerCount,copy);offset+=copy;headerCount+=copy;
                    if(headerCount<4) continue;
                    int size=(header[0]<<24)|(header[1]<<16)|(header[2]<<8)|header[3];
                    if(size<=0||size>MaxFrame) throw new InvalidOperationException("网络数据长度无效。");
                    body=new byte[size];bodyCount=0;
                }
                int take=Math.Min(body.Length-bodyCount,count-offset);
                Array.Copy(bytes,offset,body,bodyCount,take);offset+=take;bodyCount+=take;
                if(bodyCount==body.Length)
                {
                    if(Messages.Count>=256) throw new InvalidOperationException("网络消息积压过多。");
                    Messages.Enqueue(new UTF8Encoding(false,true).GetString(body));
                    body=null;headerCount=0;
                }
            }
        }
        public static byte[] Frame(string text)
        {
            var bytes=Encoding.UTF8.GetBytes(text);
            if(bytes.Length==0||bytes.Length>MaxFrame) throw new InvalidOperationException("关卡或网络数据过大。");
            var frame=new byte[bytes.Length+4];int size=bytes.Length;
            frame[0]=(byte)(size>>24);frame[1]=(byte)(size>>16);frame[2]=(byte)(size>>8);frame[3]=(byte)size;
            Array.Copy(bytes,0,frame,4,size);return frame;
        }
    }
    public sealed class LanTransport : IDisposable
    {
        TcpListener listener;
        Socket socket;
        IAsyncResult connecting;
        readonly Queue<byte[]> outgoing=new Queue<byte[]>();
        readonly LanFrameDecoder decoder=new LanFrameDecoder();
        readonly byte[] readBuffer=new byte[32768];
        int sendOffset,queuedBytes;
        public bool Connected { get; private set; }
        public bool Connecting => connecting!=null;
        public int Port => listener==null?0:((IPEndPoint)listener.LocalEndpoint).Port;
        public string Error { get; private set; }
        public int ReceivedFrames { get; private set; }
        public int PendingSendBytes => queuedBytes;
        public event Action ConnectedNow;
        public event Action<string> Message;
        public void Listen(int port)
        {
            listener=new TcpListener(IPAddress.Any,port);listener.Start(2);
        }
        public void Connect(string address,int port)
        {
            if(!IPAddress.TryParse(address,out var ip)||ip.AddressFamily!=AddressFamily.InterNetwork)
                throw new ArgumentException("请输入房主的 IPv4 地址，例如 192.168.1.10。");
            socket=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
            connecting=socket.BeginConnect(ip,port,null,null);
        }
        void Activate(Socket value)
        {
            socket=value;socket.Blocking=false;socket.NoDelay=true;socket.SendBufferSize=16384;Connected=true;ConnectedNow?.Invoke();
        }
        public void Send(string text)
        {
            if(!Connected) return;
            byte[] packet=LanFrameDecoder.Frame(text);
            if(queuedBytes+packet.Length>LanFrameDecoder.MaxFrame*2) throw new InvalidOperationException("网络发送积压，请检查连接。");
            queuedBytes+=packet.Length;outgoing.Enqueue(packet);
        }
        public void Poll()
        {
            try
            {
                if(connecting!=null && connecting.IsCompleted)
                { socket.EndConnect(connecting);connecting=null;Activate(socket); }
                if(listener!=null && listener.Pending())
                {
                    var accepted=listener.AcceptSocket();
                    if(socket!=null)
                    {
                        byte[] full=LanFrameDecoder.Frame("{\"type\":\"reject\",\"protocol\":2,\"text\":\"房间已满，只支持两名玩家。\"}");
                        accepted.SendTimeout=100;accepted.Send(full);accepted.Close();
                    }
                    else Activate(accepted);
                }
                if(!Connected) return;
                int budget=512*1024;bool ended=false;
                while(budget>0 && socket.Poll(0,SelectMode.SelectRead))
                {
                    int count=socket.Receive(readBuffer,0,Math.Min(budget,readBuffer.Length),SocketFlags.None);
                    if(count==0) { ended=true;break; }
                    decoder.Push(readBuffer,count);budget-=count;
                }
                int messages=0;
                while(decoder.Messages.Count>0 && messages++<128)
                {
                    string value=decoder.Messages.Dequeue();ReceivedFrames++;Message?.Invoke(value);
                    if(!Connected) return;
                }
                if(ended) throw new InvalidOperationException("对方已断开连接。");
                Flush();
            }
            catch(SocketException e) when(e.SocketErrorCode==SocketError.WouldBlock) { }
            catch(Exception e) { Error=e.Message;Dispose(); }
        }
        public void Flush()
        {
            if(!Connected) return;
            int budget=512*1024;
            while(outgoing.Count>0 && budget>0 && socket.Poll(0,SelectMode.SelectWrite))
            {
                var bytes=outgoing.Peek();
                int sent=socket.Send(bytes,sendOffset,Math.Min(budget,bytes.Length-sendOffset),SocketFlags.None);
                if(sent<=0) break;
                sendOffset+=sent;queuedBytes-=sent;budget-=sent;
                if(sendOffset==bytes.Length) { outgoing.Dequeue();sendOffset=0; }
            }
        }
        public void Dispose()
        {
            Connected=false;connecting=null;
            try { socket?.Close(); } catch { }
            try { listener?.Stop(); } catch { }
            socket=null;listener=null;outgoing.Clear();queuedBytes=sendOffset=0;
        }
    }
}
