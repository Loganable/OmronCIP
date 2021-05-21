using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OmronCIP
{
    public class CIPHelper
    {
        #region 服务代码
        /// <summary>
        /// CIP命令中的读取数据的服务
        /// </summary>
        public const byte CIP_READ_DATA = 0x4C;

        /// <summary>
        /// CIP命令中的写入数据的服务
        /// </summary>
        public const byte CIP_WRITE_DATA = 0x4D;
        #endregion

        #region 数据类型代码
        /// <summary>
        /// bool型数据，一个字节长度
        /// </summary>
        public const byte CIP_Type_Bool = 0xC1;

        /// <summary>
        /// 短整型，两个字节长度
        /// </summary>
        public const byte CIP_Type_Short = 0xC3;

        /// <summary>
        /// 整型，四个字节长度
        /// </summary>
        public const byte CIP_Type_Int = 0xC4;

        /// <summary>
        /// 浮点型数据，四个字节长度
        /// </summary>
        public const byte CIP_Type_Float = 0xCA;

        /// <summary>
        /// 字符串类型数据
        /// </summary>
        public const byte CIP_Type_String = 0xD0;
        #endregion

        #region 辅助生成指令的方法
        /// <summary>
        /// 将标签名转换为Byte数组
        /// </summary>
        /// <param name="tagName">标签名</param>
        /// <returns></returns>
        public static byte[] BuildRequestPathCommand(string tagName)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                string[] tagNames = tagName.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < tagNames.Length; i++)
                {
                    string strIndex = string.Empty;
                    int indexFirst = tagNames[i].IndexOf('[');
                    int indexSecond = tagNames[i].IndexOf(']');
                    if (indexFirst > 0 && indexSecond > 0 && indexSecond > indexFirst)
                    {
                        strIndex = tagNames[i].Substring(indexFirst + 1, indexSecond - indexFirst - 1);
                        tagNames[i] = tagNames[i].Substring(0, indexFirst);
                    }

                    ms.WriteByte(0x91);                        // 固定
                    byte[] nameBytes = Encoding.UTF8.GetBytes(tagNames[i]);
                    ms.WriteByte((byte)nameBytes.Length);    // 节点的长度值
                    ms.Write(nameBytes, 0, nameBytes.Length);
                    if (nameBytes.Length % 2 == 1) ms.WriteByte(0x00);

                    if (!string.IsNullOrEmpty(strIndex))
                    {
                        string[] indexs = strIndex.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int j = 0; j < indexs.Length; j++)
                        {
                            int index = Convert.ToInt32(indexs[j]);
                            if (index < 256)
                            {
                                ms.WriteByte(0x28);
                                ms.WriteByte((byte)index);
                            }
                            else
                            {
                                ms.WriteByte(0x29);
                                ms.WriteByte(0x00);
                                ms.WriteByte(BitConverter.GetBytes(index)[0]);
                                ms.WriteByte(BitConverter.GetBytes(index)[1]);
                            }
                        }
                    }
                }

                return ms.ToArray();
            }
        }        

        /// <summary>
        /// 打包生成一个请求读取数据的节点信息，CIP指令信息
        /// </summary>
        /// <param name="tagName">地址</param>
        /// <param name="length">指代数组的长度</param>
        /// <returns>CIP的指令信息</returns>
        public static byte[] PackRequsetRead(string tagName, int length = 1)
        {
            byte[] buffer = new byte[1024];
            int offset = 0;
            buffer[offset++] = CIP_READ_DATA;
            offset++;

            byte[] requestPath = BuildRequestPathCommand(tagName);
            requestPath.CopyTo(buffer, offset);
            offset += requestPath.Length;

            buffer[1] = (byte)((offset - 2) / 2);
            buffer[offset++] = BitConverter.GetBytes(length)[0];
            buffer[offset++] = BitConverter.GetBytes(length)[1];

            byte[] data = new byte[offset];
            Array.Copy(buffer, 0, data, 0, offset);
            return data;
        }

        /// <summary>
        /// 根据指定的数据和类型，生成对应的数据
        /// </summary>
        /// <param name="address">地址信息</param>
        /// <param name="typeCode">数据类型</param>
        /// <param name="value">字节值</param>
        /// <param name="length">如果节点为数组，就是数组长度</param>
        /// <returns>CIP的指令信息</returns>
        public static byte[] PackRequestWrite(string tagName, ushort typeCode, byte[] value, int length = 1)
        {
            byte[] buffer = new byte[1024];
            int offset = 0;
            buffer[offset++] = CIP_WRITE_DATA;
            offset++;

            byte[] requestPath = BuildRequestPathCommand(tagName);
            requestPath.CopyTo(buffer, offset);
            offset += requestPath.Length;

            buffer[1] = (byte)((offset - 2) / 2);

            buffer[offset++] = BitConverter.GetBytes(typeCode)[0];     // 数据类型
            buffer[offset++] = BitConverter.GetBytes(typeCode)[1];

            buffer[offset++] = BitConverter.GetBytes(length)[0];       // 固定
            buffer[offset++] = BitConverter.GetBytes(length)[1];

            value.CopyTo(buffer, offset);                              // 数值
            offset += value.Length;

            byte[] data = new byte[offset];
            Array.Copy(buffer, 0, data, 0, offset);
            return data;
        }

        /// <summary>
        /// 生成读取直接节点数据信息的内容
        /// </summary>
        /// <param name="slot">PLC所在的槽号</param>
        /// <param name="cips">cip指令内容</param>
        /// <returns>最终的指令值</returns>
        public static byte[] PackCommandSpecificData(byte slot, params byte[][] cips)
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x00);
            ms.WriteByte(0x00);
            ms.WriteByte(0x00);
            ms.WriteByte(0x00);
            ms.WriteByte(0x01);     // 超时
            ms.WriteByte(0x00);
            ms.WriteByte(0x02);     // 项数
            ms.WriteByte(0x00);
            ms.WriteByte(0x00);     // 连接的地址项
            ms.WriteByte(0x00);
            ms.WriteByte(0x00);     // 长度
            ms.WriteByte(0x00);
            ms.WriteByte(0xB2);     // 连接的项数
            ms.WriteByte(0x00);
            ms.WriteByte(0x00);     // 后面数据包的长度，等全部生成后在赋值
            ms.WriteByte(0x00);

            ms.WriteByte(0x52);     // 服务
            ms.WriteByte(0x02);     // 请求路径大小
            ms.WriteByte(0x20);     // 请求路径
            ms.WriteByte(0x06);
            ms.WriteByte(0x24);
            ms.WriteByte(0x01);
            ms.WriteByte(0x0A);     // 超时时间
            ms.WriteByte(0xF0);
            ms.WriteByte(0x00);     // CIP指令长度
            ms.WriteByte(0x00);

            int count = 0;
            if (cips.Length == 1)
            {
                ms.Write(cips[0], 0, cips[0].Length);
                count += cips[0].Length;
            }
            else
            {
                ms.WriteByte(0x0A);   // 固定
                ms.WriteByte(0x02);
                ms.WriteByte(0x20);
                ms.WriteByte(0x02);
                ms.WriteByte(0x24);
                ms.WriteByte(0x01);
                count += 8;

                ms.Write(BitConverter.GetBytes((ushort)cips.Length), 0, 2);  // 写入项数
                ushort offset = (ushort)(0x02 + 2 * cips.Length);
                count += 2 * cips.Length;

                for (int i = 0; i < cips.Length; i++)
                {
                    ms.Write(BitConverter.GetBytes(offset), 0, 2);
                    offset = (ushort)(offset + cips[i].Length);
                }

                for (int i = 0; i < cips.Length; i++)
                {
                    ms.Write(cips[i], 0, cips[i].Length);
                    count += cips[i].Length;
                }
            }
            ms.WriteByte(0x01);     // Path Size
            ms.WriteByte(0x00);
            ms.WriteByte(0x01);     // port
            ms.WriteByte(slot);

            byte[] data = ms.ToArray();
            ms.Dispose();
            BitConverter.GetBytes((short)count).CopyTo(data, 24);
            data[14] = BitConverter.GetBytes((short)(data.Length - 16))[0];
            data[15] = BitConverter.GetBytes((short)(data.Length - 16))[1];
            return data;
        }

        /// <summary>
        /// 将CommandSpecificData的命令，打包成可发送的数据指令
        /// </summary>
        /// <param name="msgType">报文类型代码</param>
        /// <param name="sessionID">会话id</param>
        /// <param name="commandSpecificData">CommandSpecificData命令</param>
        /// <returns>最终可发送的数据命令</returns>
        public static byte[] PackRequest(byte msgType, byte[] sessionID, byte[] commandSpecificData)
        {
            byte[] buffer = new byte[commandSpecificData.Length + 24];
            Array.Copy(commandSpecificData, 0, buffer, 24, commandSpecificData.Length);
            BitConverter.GetBytes(msgType).CopyTo(buffer, 0);
            sessionID.CopyTo(buffer, 4);
            BitConverter.GetBytes((ushort)commandSpecificData.Length).CopyTo(buffer, 2);
            return buffer;
        }
        #endregion

        #region 自定义方法
        /// <summary>
        /// 解析应答报文
        /// </summary>
        /// <param name="response">PLC应答报文</param>
        /// <returns>返回解析后的数据内容对象</returns>
        public static object UnpackResult(byte[] response)
        {
            object result = null;
            int dataLength;
            try
            {
                if (response.Length < 44) throw new Exception("报文长度异常！");
                if (response[42] != 0x00 || response[43] != 0x00) throw new Exception("报文状态异常");

                if (response[40] == 0xCC)//Read应答报文
                {
                    switch (response[44])
                    {
                        case CIP_Type_Bool://Bool
                            dataLength = response.Length - 46;
                            if (dataLength == 2 && response[46] <= 1 && response[47] == 0)
                            {
                                result = Convert.ToInt16((response[46] & 0x00ff) | (response[47] << 8));
                            }
                            else
                            {
                                List<char> charLiat = new List<char>();
                                for (int j = 0; j < dataLength; j++)
                                {
                                    result += Convert.ToString(CIPHelper.Reverse(response[46 + j]), 2).PadLeft(8, '0');
                                }
                            }
                            break;
                        case CIP_Type_Short://Short
                            dataLength = (response.Length - 46) / 2;
                            if (dataLength == 1)
                            {
                                result = (response[46] & 0x00ff) | (response[47] << 8);
                            }
                            else
                            {
                                List<int> sList = new List<int>();
                                for (int j = 0; j < dataLength; j++)
                                {
                                    sList.Add((response[46 + j * 2] & 0x00ff) | (response[47 + j * 2] << 8));
                                }
                                result = $"[{String.Join(",", sList)}]";
                            }
                            break;
                        case CIP_Type_Int://Int
                            dataLength = (response.Length - 46) / 4;
                            if (dataLength == 1)
                            {
                                int intLow = (response[46] & 0x00ff) | (response[47] << 8);
                                int intHigh = (response[48] & 0x00ff) | (response[49] << 8);
                                result = (intLow & 0x0000ffff) | (intHigh << 16);
                            }
                            else
                            {
                                List<int> intList = new List<int>();
                                for (int j = 0; j < dataLength; j++)
                                {
                                    int intLow = (response[46 + j * 4] & 0x00ff) | (response[47 + j * 4] << 8);
                                    int intHigh = (response[48 + j * 4] & 0x00ff) | (response[49 + j * 4] << 8);
                                    int intResult = (intLow & 0x0000ffff) | (intHigh << 16);
                                    intList.Add(intResult);
                                }
                                result = $"[{String.Join(",", intList)}]";
                            }

                            break;
                        case CIP_Type_Float://Float
                            dataLength = (response.Length - 46) / 4;
                            if (dataLength == 1)
                            {
                                int floatLow = (response[46] & 0x00ff) | (response[47] << 8);
                                int floatHigh = (response[48] & 0x00ff) | (response[49] << 8);
                                int floatResult = (floatLow & 0x0000ffff) | (floatHigh << 16);
                                result = BitConverter.ToSingle(BitConverter.GetBytes(floatResult), 0);
                            }
                            else
                            {
                                List<float> floatList = new List<float>();
                                for (int j = 0; j < dataLength; j++)
                                {
                                    int floatLow = (response[46 + j * 4] & 0x00ff) | (response[47 + j * 4] << 8);
                                    int floatHigh = (response[48 + j * 4] & 0x00ff) | (response[49 + j * 4] << 8);
                                    int floatResult = (floatLow & 0x0000ffff) | (floatHigh << 16);
                                    floatList.Add(BitConverter.ToSingle(BitConverter.GetBytes(floatResult), 0));
                                }
                                result = $"[{String.Join(",", floatList)}]";
                            }
                            break;
                        case CIP_Type_String://String
                            dataLength = (response[46] & 0x00ff) | (response[47] << 8);
                            byte[] temp = new byte[dataLength];
                            Array.Copy(response, 48, temp, 0, dataLength);
                            result = Encoding.UTF8.GetString(temp);
                            break;
                        default:
                            throw new Exception("数据类型异常");
                    }
                }
                else
                    throw new Exception("报文格式错误");
            }
            catch (Exception)
            {

            }
            return result;
        }

        /// <summary>
        /// 获取数据类型代码
        /// </summary>
        /// <param name="type">数据类型</param>
        /// <returns>对应类型代码</returns>
        public static byte GetTypeCode(TypeCode type)
        {
            byte typeCode = 0;
            switch (type)
            {
                case TypeCode.Boolean:
                    typeCode = CIP_Type_Bool;
                    break;
                case TypeCode.Int16:
                    typeCode = CIP_Type_Short;
                    break;
                case TypeCode.Int32:
                    typeCode = CIP_Type_Int;
                    break;
                case TypeCode.Single:
                    typeCode = CIP_Type_Float;
                    break;
                case TypeCode.String:
                    typeCode = CIP_Type_String;
                    break;
                default:
                    break;
            }
            return typeCode;
        }

        /// <summary>
        /// 逆置Byte(8位)数据的比特位，例如 11001000 -> 00010011
        /// </summary>
        /// <param name="data">需逆置的Byte数据</param>
        /// <returns>逆置后的数据</returns>
        public static byte Reverse(byte data)
        {
            data = (byte)(((data >> 1) & 0x55) | ((data & 0x55) << 1));
            data = (byte)(((data >> 2) & 0x33) | ((data & 0x33) << 2));
            data = (byte)(((data >> 4) & 0x0F) | ((data & 0x0F) << 4));
            return data;
        }
        #endregion
    }
}
