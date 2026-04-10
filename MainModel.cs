using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yell
{

    public record LogEntry(
        string Time,
        string Direction, // 发送还是接收
        string RawData,   // 原始 HEX 字符串
        string Value      // 解析后的数值（如果是指令则为空）
    );

}
