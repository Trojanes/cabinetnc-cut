# OmniCam 贴标故障交接（2026-08-19）

## 结论

本次异常发生在打印/取标握手阶段，不是 U/V 贴标坐标导致。

机床执行 `M701` 后进入内部子程序 `P2M701`，画面持续显示 `@I54=0`，并在等待/重试代码间循环。随后标签软件明确报错：

```text
The requested address is not valid in its context
Socket.Bind(localEP)
```

标签软件尝试将本地连接绑定到 `192.168.0.4`，但控制电脑的标签网卡没有该地址（或该地址不可绑定），因此控制器连接断开，完成信号始终无法到达，`M701` 不能结束。

## 当时使用的文件

NC：

```text
C:\Users\azqrv\Documents\Rouge\22'6 Club Lounge\NC Files\22_OHC_Divider_Recut.anc
```

视频：

```text
C:\Users\azqrv\OneDrive\Documents\xwechat_files\citytroy_e50e\msg\video\2026-08\7688e58cc15441dfba58fbe20c07ce07.mp4
C:\Users\azqrv\OneDrive\Documents\xwechat_files\citytroy_e50e\msg\video\2026-08\4e353a273d348cad9a167c0e77b81ad2.mp4
```

这些是机床电脑的本地文件，不在 Git 仓库或当前云端工作区内。

## ANC关键流程

Process 2 中每张标签执行：

```text
LS11='OHC_OH_D0_2'
M701
(GTO,ST01,E41=0)
G90 G0 V218.491 U226.266
M702
(GTO,ST01,E42=0)
```

第二张标签名为：

```text
OHC_OH_D1_2
```

视频中程序卡在机床内部 `P2M701`，尚未返回到ANC，因此当时还没有执行 `G0 V... U...`。这排除了贴标坐标是首次故障触发点。

## 标签软件现场设置

截图显示：

```text
Print picture path:              D:\CNC
Printer IP:                      192.168.0.3
Connect Printer Network Card:    192.168.0.4
Controller Name:                 IP:192.168.0.2
Pass:                            2
```

预期网络分配：

| 设备 | 地址 |
|---|---|
| OSAI控制器 | `192.168.0.2` |
| 标签打印机 | `192.168.0.3` |
| 控制电脑标签网卡 | `192.168.0.4` |

## 首要恢复步骤

1. 停止贴标程序，不要继续无限重试。
2. 在机床控制电脑运行 `ipconfig`，确认连接控制器/打印机的物理网卡。
3. 将该网卡静态IPv4设置为：
   - IP：`192.168.0.4`
   - 子网掩码：`255.255.255.0`
   - 网关：留空
   - DNS：留空
4. 确认 `.4` 没有被其他设备占用。
5. 测试：

   ```text
   ping 192.168.0.2
   ping 192.168.0.3
   ```

6. 重新启动 `Excitech Label Printing V3.0`。
7. 保持图片路径为 `D:\CNC`，点击 `Save Settings`，再点击 `Connect Controller`。
8. 只有在连接错误消失后，才重新运行 Process 2。

## 标签文件要求

由于机器的 `Print picture path` 是 `D:\CNC`，以下文件必须直接存在：

```text
D:\CNC\OHC_OH_D0_2.bmp
D:\CNC\OHC_OH_D1_2.bmp
```

不应放成：

```text
D:\CNC\label\OHC_OH_D0_2.bmp
```

同时检查Windows隐藏扩展名问题，避免实际文件名为 `*.bmp.bmp`。

## OmniCam侧已确认的问题

当前OmniCam：

- 使用 `LS11='<stem>'` 调用标签。
- 将BMP导出到所选目录下的 `label` 子目录。
- UI提示用户平铺复制到机床 `D:\Label`。

但现场标签软件实际配置为 `D:\CNC`。因此现有提示与现场配置不一致，且子目录不能被标签软件递归搜索。

相关代码：

```text
dotnet/src/CabinetNC.Domain/Manufacturing/LabelExport.cs
dotnet/src/CabinetNC.Desktop/LabelBmp.cs
dotnet/src/CabinetNC.Desktop/MainWindow.xaml.cs
```

`MainWindow.xaml.cs` 中 `WriteLabelBmps` 当前创建 `label` 子目录；导出状态文本写死提示 `D:\Label`。

## 建议的软件后续

1. 增加“机床标签图片目录”配置，默认值按现场设为 `D:\CNC`，不要写死 `D:\Label`。
2. 导出时提供一个可直接平铺复制的目录，或明确显示最终目标路径和必需文件列表。
3. 导出前验证ANC中每个 `LS11` 都有同名BMP。
4. 对 `E41/E42` 重试增加次数或超时限制，失败后明确停机报警，避免无期限循环。
5. 在机床联调确认前，不修改 U/V 坐标算法；当前证据未指向坐标错误。

## 复测通过标准

- 标签软件连接控制器时不再出现 `Socket.Bind(localEP)` 错误。
- `.2` 和 `.3` 均可从控制电脑稳定ping通。
- `M701` 执行后 `@I54` 能从 `0` 变为完成状态，程序退出 `P2M701`。
- 控制器随后执行对应的 `G0 V... U...`。
- `M702` 只执行一次并正常返回。
- 两张标签均正确打印、取标和粘贴。

## 安全提示

在 `@I54` 持续为0、控制器显示断开、或打印机构重复异常动作时，应立即停止Process 2。不要通过修改坐标、屏蔽传感器或删除等待条件来强行继续。
