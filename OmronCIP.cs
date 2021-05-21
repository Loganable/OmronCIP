using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OmronCIP
{
    public sealed class OmronCipNet : IDisposable
    {
        #region Fields
        private Socket socket;
        private IPEndPoint endPoint;

        private readonly object syncLock = new object();
        #endregion

        #region Properties
        /// <summary>
        /// 当前会话句柄，与PLC握手时从PLC获得
        /// </summary>
        public byte[] SessionHandle { get; set; } = { 0x00, 0x00, 0x00, 0x00 };

        /// <summary>
        /// PLC槽号
        /// </summary>
        public byte Slot { get; set; } = 0;

        public bool IsClosed { get { return socket == null; } }
        #endregion

        #region Constructors
        public OmronCipNet(string ipAddress, int port = 44818)
        {
            endPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
        }
        #endregion

        #region Private Functions
        /// <summary>
        /// 生成读取命令
        /// </summary>
        /// <param name="address">标签地址</param>
        /// <returns>Message information that contains the result object </returns>
        private byte[] BuildReadCommand(string address)
        {
            try
            {
                byte[] commandSpecificData = CIPHelper.PackCommandSpecificData(Slot, CIPHelper.PackRequsetRead(address));
                return CIPHelper.PackRequest(0x6F, SessionHandle, commandSpecificData);
            }
            catch (Exception)
            {
                return new byte[0];
            }
        }

        /// <summary>
        /// 生成写入命令
        /// </summary>
        /// <param name="address">The address of the tag name </param>
        /// <param name="typeCode">Data type</param>
        /// <param name="data">Source Data </param>
        /// <param name="length">In the case of arrays, the length of the array </param>
        /// <returns>Message information that contains the result object</returns>
        private byte[] BuildWriteCommand(string address, ushort typeCode, byte[] data, int length = 1)
        {
            try
            {
                byte[] cip = CIPHelper.PackRequestWrite(address, typeCode, data, length);
                byte[] commandSpecificData = CIPHelper.PackCommandSpecificData(Slot, cip);

                return CIPHelper.PackRequest(0x6F, SessionHandle, commandSpecificData);
            }
            catch (Exception)
            {
                return new byte[0];
            }
        }

        /// <summary>
        /// 与PLC数据交互
        /// </summary>
        /// <param name="command">发送报文</param>
        /// <returns>应答报文</returns>
        private byte[] PlcCommunication(byte[] command)
        {
            int count = 0;
            int totalCount = 0;
            byte[] output = null;
            byte[] response = new byte[512];
            List<byte> data = new List<byte>();
            try
            {
                lock (syncLock)
                {
                    data.Clear();
                    Array.Clear(response, 0, 512);
                    socket.Send(command);
                    do
                    {
                        count = socket.Receive(response);
                        byte[] temp = new byte[count];
                        Array.Copy(response, 0, temp, totalCount, count);
                        data.AddRange(temp);
                        totalCount += count;
                    } while (totalCount < 4 || totalCount != response[2] + 24);
                    output = data.ToArray();
                }
                OnLogRecorded?.Invoke(this, output);
                return output;
            }
            catch (Exception ex)
            {
                OnErrorOccured?.Invoke(this, ex.Message);
                return new byte[] { 0 };
            }
        }
        #endregion

        #region Functions
        /// <summary>
        /// 连接PLC并注册会话ID
        /// </summary>
        /// <returns>返回是否成功连接PLC</returns>
        public bool Connect()
        {
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(endPoint);
                byte[] received = PlcCommunication(CIPHelper.PackRequest(0x65, new byte[4] , new byte[] { 0x01, 0x00, 0x00, 0x00 }));
                Array.Copy(received, 4, SessionHandle, 0, 4);
            }
            catch (Exception ex)
            {
                OnErrorOccured?.Invoke(this, ex.Message);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 注销会话并断开与PLC的连接
        /// </summary>
        public void Dispose()
        {
            if (socket != null)
            {
                try
                {
                    PlcCommunication(CIPHelper.PackRequest(0x66, SessionHandle, new byte[0]));
                    socket.Shutdown(SocketShutdown.Both);
                }
                catch (Exception ex)
                {
                    OnErrorOccured?.Invoke(this, ex.Message);
                }
                socket.Close();
                socket = null;
            }
        }

        /// <summary>
        /// 读取PLC标签地址数据
        /// </summary>
        /// <param name="tagName">标签名</param>
        /// <returns>数据内容对象</returns>
        public object Read(string tagName)
        {
            byte[] readCMD = BuildReadCommand(tagName);
            byte[] received = PlcCommunication(readCMD);
            return CIPHelper.UnpackResult(received);
        }

        /// <summary>
        /// 向PLC标签地址写入数据
        /// </summary>
        /// <param name="tagName">标签名</param>
        /// <param name="data">写入数据</param>
        /// <param name="type">数据类型</param>
        /// <param name="length">长度</param>
        public void Write(string tagName, string data, TypeCode type, int length = 1)
        {
            byte[] value;
            switch (type)
            {
                case TypeCode.Boolean:
                    value = new byte[2];
                    value[0] = Convert.ToByte(data);
                    break;
                case TypeCode.Int16:
                    value = new byte[2];
                    value[0] = (byte)(Convert.ToInt16(data) % 256);
                    value[1] = (byte)(Convert.ToInt16(data) / 256);
                    break;
                case TypeCode.Int32:
                    value = BitConverter.GetBytes(Convert.ToInt32(data));
                    break;
                case TypeCode.Single:
                    value = BitConverter.GetBytes(Convert.ToSingle(data));                  
                    break;
                case TypeCode.String:
                    value = Encoding.UTF8.GetBytes(Convert.ToString(data));
                    break;
                default:
                    value = new byte[] { 0x00, 0x00 };
                    break;
            }
            byte[] writeCMD = BuildWriteCommand(tagName, CIPHelper.GetTypeCode(type), value, length);
            byte[] _ = PlcCommunication(writeCMD);
        }
        #endregion

        #region Events
        public event EventHandler<string> OnErrorOccured;
        public event EventHandler<byte[]> OnLogRecorded;
        #endregion
    }
}
