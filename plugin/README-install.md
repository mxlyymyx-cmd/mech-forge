# MechForge 🏭 SolidWorks 插件安装说明

MechForge 是一个 SolidWorks 参数化设计插件，通过本地 HTTP API (localhost:5757) 与 Python 设计引擎通信，实现从自然语言到 3D 模型的自动化。

## 系统要求

| 组件 | 要求 |
|------|------|
| SolidWorks | 2022+ (x64) |
| .NET Framework | 4.8 Runtime + SDK (或 VS 2022 Build Tools) |
| Python | 3.10+ |
| 操作系统 | Windows 10/11 x64 |

## 文件结构

```
plugin/
├── MechForgeAddin.csproj          # C# 项目文件 (.NET 4.8)
├── MechForgeAddin.cs              # 插件主入口 (ISwAddin)
├── TaskPaneControl.cs             # 任务面板逻辑
├── TaskPaneControl.Designer.cs    # 任务面板布局
├── ApiClient.cs                   # HTTP API 客户端
├── SwApiHelper.cs                 # SolidWorks API 辅助
├── install.ps1                    # 安装脚本
└── README-install.md              # 本文件
```

## 快速安装

### Step 1: 启动 Python API 服务器

```bash
cd projects/solidworks-parametric

# 安装依赖
pip install -r requirements-plugin.txt

# 启动 API 服务器
python api.py --port 5757
```

终端应显示：
```
MechForge API Server 🏭
=======================================================
API Base:  http://127.0.0.1:5757/api
```

### Step 2: 编译并注册插件

**方法 A：使用安装脚本 (推荐)**

以 **管理员身份** 打开 PowerShell：

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
cd projects\solidworks-parametric\plugin
.\install.ps1
```

脚本将自动：
1. 检查 Python 后端健康状态
2. 使用 MSBuild 编译 C# 项目
3. 通过 RegAsm 注册 COM

**方法 B：手动编译注册**

以 **管理员身份** 打开 Developer Command Prompt for VS 2022：

```cmd
cd projects\solidworks-parametric\plugin
msbuild MechForgeAddin.csproj /p:Configuration=Release /p:Platform=x64
regasm /codebase bin\x64\Release\MechForgeAddin.dll
```

### Step 3: 在 SolidWorks 中加载插件

1. 启动/重启 SolidWorks
2. 菜单栏 → **工具** → **插件**
3. 弹出窗口中勾选 **MechForge Addin**
4. SolidWorks 工具栏出现 **MechForge** 菜单

### Step 4: 使用

1. 点击菜单栏 **MechForge** → **打开 MechForge 面板**
2. 或者点击工具栏的 MechForge 图标
3. 在任务面板中：
   - **AI 模式**：输入自然语言点「生成」
   - **手动模式**：选择零件类型，填写参数，点「生成」

## 卸载

### 注销 COM

以管理员身份运行：

```cmd
regasm /unregister bin\x64\Release\MechForgeAddin.dll
```

### 清理 SolidWorks 注册项

```cmd
reg delete "HKLM\SOFTWARE\SolidWorks\AddIns\{A1B2C3D4-E5F6-7890-ABCD-EF1234567891}" /f
```

### 删除文件

直接删除 `plugin/` 目录即可。

## 常见问题

### Q: 面板显示 "后端未响应"

确保 Python API 服务器正在运行：

```bash
# 检查
curl http://127.0.0.1:5757/api/health
# 预期: {"success":true,"data":{"status":"ok","version":"1.0.0"}}
```

### Q: 宏执行失败

1. SolidWorks → **工具** → **宏** → **安全性**
2. 将安全级别设为 **中** 或 **低**
3. 如果已有数字签名，可设为 **高** 并添加受信任发布者

### Q: RegAsm 权限不足

确保以 **管理员身份** 运行命令行。

### Q: 编译错误 "SolidWorks.Interop 引用找不到"

检查 SolidWorks Interop DLL 路径：

```
C:\Program Files\SolidWorks Corp\SolidWorks\api\redist\
```

如果路径不同，编辑 `MechForgeAddin.csproj` 中的 HintPath。

### Q: 任务面板内容滚动/显示不全

面板高度默认 494px，可拖拽 SolidWorks 任务窗格边框调整宽度（≥320px）。

## 调试

查看 SolidWorks 输出日志：

```csharp
// 在代码中:
System.Diagnostics.Debug.WriteLine("[MechForge] 调试信息");
// 用 DebugView (Sysinternals) 查看
```

查看 API 服务器日志：

```bash
python api.py --port 5757 --debug
```
