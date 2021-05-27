using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

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
        /// 当前会话句柄
        /// </summary>
        public byte[] SessionHandle { get; set; } = { 0x00, 0x00, 0x00, 0x00 };

        /// <summary>
        /// PLC槽号
        /// </summary>
        public byte Slot { get; set; } = 0;

        /// <summary>
        /// 当前连接状态
        /// </summary>
        public bool IsConnected {
            get
            {
                try
                {
                    socket.Send(new byte[0]);
                    return true;
                }
                catch (Exception ex)
                {
                    return ex.Message.Contains("10035");
                }
            }}
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
        /// <param name="tagName">变量标签名</param>
        /// <returns>读取指令</returns>
        private byte[] BuildReadCommand(string tagName)
        {
            try
            {
                byte[] commandSpecificData = CIPHelper.PackCommandSpecificData(Slot, CIPHelper.PackRequsetRead(tagName));
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
        /// <param name="tagName">变量标签名</param>
        /// <param name="typeCode">数据类型代码</param>
        /// <param name="data">数据对应的byte数组</param>
        /// <returns>写入指令</returns>
        private byte[] BuildWriteCommand(string tagName, ushort typeCode, byte[] data)
        {
            try
            {
                byte[] cip = CIPHelper.PackRequestWrite(tagName, typeCode, data);
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
                        int count = socket.Receive(response);
                        byte[] temp = new byte[count];
                        Array.Copy(response, 0, temp, totalCount, count);
                        data.AddRange(temp);
                        totalCount += count;
                    } while (totalCount < 4 || totalCount != response[2] + 24);
                    output = data.ToArray();
                }
                return output;
            }
            catch (Exception)
            {
                return new byte[] { 0 };
            }
        }
        #endregion

        #region Functions
        /// <summary>
        /// 连接PLC并注册会话ID
        /// </summary>
        /// <returns>结果类</returns>
        public CIPResult Connect()
        {
            CIPResult result = new CIPResult();
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(endPoint);
                byte[] received = PlcCommunication(CIPHelper.PackRequest(0x65, new byte[4] , new byte[] { 0x01, 0x00, 0x00, 0x00 }));
                Array.Copy(received, 4, SessionHandle, 0, 4);
            }
            catch (Exception ex)
            {
                result.Message = ex.StackTrace;
                result.IsSuccess = false;
                return result;
            }
            return result;
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
                catch{}
                socket.Close();
                socket = null;
            }
        }

        /// <summary>
        /// 读取PLC标签地址数据
        /// </summary>
        /// <param name="tagName">标签名</param>
        /// <returns>结果类</returns>
        public CIPResult Read(string tagName)
        {
            CIPResult result = new CIPResult();
            try
            {
                byte[] readCMD = BuildReadCommand(tagName);
                byte[] received = PlcCommunication(readCMD);
                result.Content = CIPHelper.UnpackResult(received);
            }
            catch (Exception ex)
            {
                result.Message = ex.StackTrace;
                result.IsSuccess = false;
                return result;
            }
            return result;
        }

        /// <summary>
        /// 向PLC标签地址写入数据
        /// </summary>
        /// <param name="tagName">标签名</param>
        /// <param name="data">写入数据</param>
        /// <param name="type">数据类型</param>
        /// <returns>结果类</returns>
        public CIPResult Write(string tagName, object data, TypeCode type)
        {
            CIPResult result = new CIPResult();
            try
            {
                byte[] writeCMD = BuildWriteCommand(tagName, CIPHelper.GetTypeCode(type), CIPHelper.DataToBytes(data, type));
                byte[] _ = PlcCommunication(writeCMD);
            }
            catch (Exception ex)
            {
                result.Message = ex.StackTrace;
                result.IsSuccess = false;
                return result;
            }
            return result;
        }
        #endregion
    }
}
