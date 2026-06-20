# PLC 协议数据完整性支持文档

## Overview
本文档记录各 PLC 协议对历史数据补传（序列号跳跃检测后的数据请求）的支持情况。

## 需求背景（11.4）
PLC 断网重连后，上位机通过序列号跳跃检测发现数据丢失。理想情况下应能从 PLC 请求补传丢失的数据。但受限于不同 PLC 协议和硬件的能力，补传支持情况不同。

## 各协议支持情况

### 1. ModbusTCP
| 维度 | 说明 |
|------|------|
| 序列号读取 | ❌ ModbusTCP 标准协议未定义序列号寄存器约定 |
| 历史数据查询 | ❌ ModbusTCP 无标准历史数据查询接口 |
| 备选方案 | ✅ 可在 PLC 侧自定义实现：在保持寄存器中存储序列号（上位机定期读取），但补传需 PLC 侧额外编程 |
| 推荐方案 | 通过 PLC 编程在固定寄存器区域维护序列号列表，上位机通过 Modbus 读取 |

### 2. MelsecMC（三菱 MC 协议）
| 维度 | 说明 |
|------|------|
| 序列号读取 | ❌ MC 协议标准未定义序列号，但三菱 PLC 可通过标签读写实现 |
| 历史数据查询 | ⚠️ 部分支持。三菱 iQ-R/iQ-F 系列支持通过 MC 协议读取标签数组，可编程实现历史数据缓存 |
| 备选方案 | 在 PLC 侧使用 FIFO 队列（比如 100 条深度），上位机通过批量读取标签获取缓存的历史数据 |
| 推荐方案 | PLC 侧实现循环缓冲区（环形队列），上位机按序列号范围读取 |

### 3. S7 Protocol（西门子 S7-1200/1500）
| 维度 | 说明 |
|------|------|
| 序列号读取 | ⚠️ 需要 PLC 侧编程。S7-1200/1500 可在 OB1 中维护计数器存入 DB 块 |
| 历史数据查询 | ✅ 部分支持。S7 协议支持通过 S7netplus 读取 DB 块数据数组，如果 PLC 侧实现了历史数据暂存区则可行 |
| 备选方案 | 在 PLC 中创建 DB 块作为环形缓冲区（如 DB100 存储最近 100 条记录），上位机通过 `ReadBytesAsync` 批量读取 |
| 推荐方案 | 1. PLC 侧创建 DB 数据块维护序列号计数器 + 最近 N 条记录的缓存数组；2. 上位机使用 S7netplus 的 `ReadBytesAsync` 读取缓存区 |

#### S7 协议的实现指引

```csharp
// 从 S7 PLC 读取历史数据补传（示例伪代码）
public async Task<IReadOnlyList<StationSnapshot>> RequestHistoricalDataAsync(
    IPlcClient client, int lastSequenceNumber, int count)
{
    // S7-1200: DB1.DBB0 起始地址，长度 200 字节 = 10 条记录 × 20 字节
    // S7-1500: DB100.DBB0 起始地址，长度 500 字节 = 25 条记录 × 20 字节
    byte[] buffer = await client.ReadBytesAsync(
        DataType.DataBlock, dbNumber: 100, startByteAdr: 0, count: 500);
    
    var snapshots = new List<StationSnapshot>();
    for (int i = 0; i < buffer.Length / 20; i++)
    {
        int offset = i * 20;
        int seq = BitConverter.ToInt32(buffer, offset);
        if (seq > lastSequenceNumber)
        {
            snapshots.Add(ParseSnapshot(buffer, offset));
        }
    }
    return snapshots;
}
```

## 总结

| 协议 | 序列号支持 | 历史补传支持 | 实现难度 | 推荐度 |
|------|-----------|-------------|---------|-------|
| ModbusTCP | ❌ | ❌ | 高 | ⭐（需 PLC 侧深度编程） |
| MelsecMC | ❌ | ⚠️ | 中 | ⭐⭐（iQ-R 系列推荐） |
| S7 Protocol | ⚠️ | ✅ | 低 | ⭐⭐⭐（推荐首选实现） |

## 结论
- **P3 阶段**：建议优先为 S7 协议（A2/A5 工位）实现历史数据补传
- **前置条件**：PLC 侧需编程实现序列号计数器 + 历史数据环形缓冲区
- **上位机改动**：在 PlcPollingService 中检测到序列号跳跃后，调用 `RequestHistoricalDataAsync` 方法
