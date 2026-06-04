using System;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;

namespace MechForge
{
    /// <summary>
    /// MechForge SolidWorks 插件主入口
    /// 
    /// 注册方式（管理员终端）：
    ///   regasm /codebase MechForgeAddin.dll
    ///   或在安装时由 install.ps1 自动注册。
    /// </summary>
    [Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567891")]
    [ComVisible(true)]
    [ProgId("MechForge.Addin")]
    public class MechForgeAddin : ISwAddin
    {
        #region 私有字段

        private SldWorks _swApp;
        private int _addinId;
        private TaskPaneControl _taskPane;
        private object _taskPaneRef;
        private bool _connected = false;

        // 工具栏命令 ID
        private const int CMD_OPEN_PANEL = 1;
        private const int CMD_ABOUT = 2;

        #endregion

        #region ISwAddin 实现

        /// <summary>
        /// 连接插件。在 SolidWorks 装载插件时自动调用。
        /// </summary>
        /// <returns>连接是否成功</returns>
        public bool Connect()
        {
            try
            {
                _swApp = (SldWorks)Application.SldWorks;
                _addinId = _swApp.GetAddInID();

                if (_addinId <= 0)
                {
                    // 尝试从 AddInID 注册表中获取
                    _addinId = _swApp.SetAddinCallbackInfo(0, this, (int)AddInUserCommandConstants.swCommands_Activate);
                }

                // 注册工具栏按钮和菜单
                RegisterToolbar();

                // 创建任务面板
                CreateTaskPane();

                _connected = true;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MechForge] Connect failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 断开插件。在 SolidWorks 卸载插件时自动调用。
        /// </summary>
        /// <returns>断开是否成功</returns>
        public bool Disconnect()
        {
            try
            {
                // 移除任务面板
                RemoveTaskPane();

                // 移除工具栏
                RemoveToolbar();

                // 清理资源
                _taskPane?.Dispose();
                _taskPane = null;
                _swApp = null;
                _connected = false;

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MechForge] Disconnect failed: " + ex.Message);
                return false;
            }
        }

        #endregion

        #region 工具栏注册

        /// <summary>
        /// 注册自定义工具栏按钮和菜单项。
        /// </summary>
        private void RegisterToolbar()
        {
            try
            {
                // 使用位图资源（16x16 和 24x24）：实际应用中需嵌入 .bmp 或 .png）
                string title = "MechForge";
                string tooltip = "MechForge 参数化设计";

                // 注册命令组（cmdGroupIndex, title, tooltip, 
                //              hint, docTypes, DocHasCmd, 
                //              cmdID0, cmdID1, ...）
                int cmdGroupIndex = -1;
                object[] registryIDs = new object[] { CMD_OPEN_PANEL };
                object[] docTypes = new object[] { (int)swDocumentTypes_e.swDocPART,
                                                    (int)swDocumentTypes_e.swDocASSEMBLY };

                bool registered = _swApp.AddCommandGroup(
                    _addinId,
                    title,
                    tooltip,
                    tooltip,
                    -1,                                     // bmp icon index (use -1 for no icon)
                    ref registryIDs,
                    ref registryIDs,
                    ref docTypes,
                    ref cmdGroupIndex
                );

                if (registered)
                {
                    // 注册命令回调
                    _swApp.SetCommandGroupCallback(cmdGroupIndex, this);
                }

                // 添加菜单
                int[] cmdIDs = new int[] { CMD_OPEN_PANEL };
                string[] menuNames = new string[] { "打开 MechForge 面板" };
                string[] menuHints = new string[] { "打开 MechForge 参数化设计面板" };
                bool menuRegistered = _swApp.AddMenu(
                    _addinId,
                    "MechForge(&M)",
                    -1,             // 菜单索引 (-1 = 末尾)
                    ref menuNames,
                    ref menuHints,
                    ref cmdIDs,
                    ref cmdGroupIndex
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MechForge] RegisterToolbar failed: " + ex.Message);
            }
        }

        /// <summary>
        /// 移除自定义工具栏。
        /// </summary>
        private void RemoveToolbar()
        {
            try
            {
                _swApp?.RemoveCommandGroup(_addinId);
            }
            catch
            {
                // 忽略清理时的异常
            }
        }

        #endregion

        #region 任务面板管理

        /// <summary>
        /// 创建任务面板（Task Pane）。
        /// </summary>
        private void CreateTaskPane()
        {
            try
            {
                _taskPane = new TaskPaneControl();
                _taskPaneRef = _taskPane;

                // 将 TaskPane 注册为 COM 可见并添加到 SolidWorks 任务窗格
                int taskPaneId = _swApp.AddTaskpaneView2(
                    _taskPaneRef,
                    "MechForge 🏭",
                    "{A1B2C3D4-E5F6-7890-ABCD-EF1234567892}"
                );

                if (taskPaneId < 0)
                {
                    System.Diagnostics.Debug.WriteLine("[MechForge] TaskPane creation returned negative ID: " + taskPaneId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MechForge] CreateTaskPane failed: " + ex.Message);
            }
        }

        /// <summary>
        /// 移除任务面板。
        /// </summary>
        private void RemoveTaskPane()
        {
            try
            {
                if (_taskPaneRef != null && _swApp != null)
                {
                    _swApp.RemoveTaskpaneView(_taskPaneRef);
                }
            }
            catch
            {
                // 忽略清理时的异常
            }
        }

        #endregion

        #region 命令回调

        /// <summary>
        /// 处理 SolidWorks 命令回调。
        /// </summary>
        public void OnCommand(int command)
        {
            try
            {
                switch (command)
                {
                    case CMD_OPEN_PANEL:
                        // 已有 TaskPane 时无需额外操作
                        System.Diagnostics.Debug.WriteLine("[MechForge] Command: Open Panel");
                        break;

                    case CMD_ABOUT:
                        System.Windows.Forms.MessageBox.Show(
                            "MechForge v1.0.0\n参数化设计插件\n基于 HTTP API (localhost:5757)",
                            "关于 MechForge",
                            System.Windows.Forms.MessageBoxButtons.OK,
                            System.Windows.Forms.MessageBoxIcon.Information
                        );
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MechForge] OnCommand error: " + ex.Message);
            }
        }

        #endregion

        #region COM 注册/注销

        /// <summary>
        /// RegAsm 注册时调用。
        /// </summary>
        [ComRegisterFunction]
        public static void RegisterFunction(Type t)
        {
            // RegAsm 自动处理注册表项
            // 如需自定义注册表，可在此添加
        }

        /// <summary>
        /// RegAsm 注销时调用。
        /// </summary>
        [ComUnregisterFunction]
        public static void UnregisterFunction(Type t)
        {
            // RegAsm 自动处理注册表项
        }

        #endregion
    }
}
