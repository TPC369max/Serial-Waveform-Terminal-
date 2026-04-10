using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yell.Utilities
{
    internal class HexConverter
    {
        public static byte[] HexStringToBytes(string hex)
        {
            try
            {
                string cleanHex = hex.Replace(" ", "").Replace("\r", "").Replace("\n", "").ToUpper();
                if (cleanHex.Length % 2 != 0)
                {
                    throw new ArgumentException("十六进制字符串长度无效，必须为偶数。");
                }
                byte[] bytes = new byte[cleanHex.Length / 2];
                for (int i = 0; i < cleanHex.Length; i += 2)
                {
                    bytes[i / 2] = Convert.ToByte(cleanHex.Substring(i, 2), 16);
                }
                return bytes;
            }catch(Exception ex) { throw new Exception($"[HexStringToBytes Error]: 输入包含非法字符或格式错误。详情: {ex.Message}"); }


        }
        public static string BytesToHexString(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;
            try
            {
                StringBuilder sb = new StringBuilder(bytes.Length * 3);
                foreach (byte b in bytes)
                {
                    sb.Append(b.ToString("X2") + " ");

                }
                return sb.ToString().Trim();
            }catch(Exception ex)
            {
                return $"[Error]: 转换失败 {ex.Message}";
            }
        }
    }
}
