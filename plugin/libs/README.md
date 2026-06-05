# SolidWorks Interop DLL 目录

GitHub Actions 自动编译需要这些 DLL。
请从你的 SolidWorks 安装目录复制到本目录：

```
C:\Program Files\SolidWorks Corp\SolidWorks\api\redist\
```

需要的文件：
- `SolidWorks.Interop.sldworks.dll`
- `SolidWorks.Interop.swcommands.dll`
- `SolidWorks.Interop.swconst.dll`
- `SolidWorks.Interop.swpublished.dll`

复制后推送到 GitHub，Actions 会自动编译插件。
