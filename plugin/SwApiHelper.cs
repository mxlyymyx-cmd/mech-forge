using System;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace MechForge
{
    /// <summary>
    /// SolidWorks COM API 辅助类。
    /// 
    /// 封装常用的 SolidWorks 操作：
    /// - 连接/断开 SW 实例
    /// - 执行 VBA 宏
    /// - 获取/创建文档
    /// - 错误处理
    /// 
    /// 注意：所有方法需要在 SolidWorks 主线程中调用（通过控件事件）。
    /// </summary>
    public static class SwApiHelper
    {
        private static SldWorks _swApp;

        #region 连接管理

        /// <summary>
        /// 获取当前 SolidWorks 应用程序实例。
        /// 如果尚未连接或已断开，尝试重新获取。
        /// </summary>
        /// <returns>SolidWorks 应用实例，或 null</returns>
        public static SldWorks GetSwApp()
        {
            if (_swApp == null)
            {
                try
                {
                    _swApp = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[SwApiHelper] GetSwApp failed: " + ex.Message);
                    return null;
                }
            }
            return _swApp;
        }

        /// <summary>
        /// 确保 SolidWorks 实例可见。
        /// </summary>
        public static void EnsureVisible()
        {
            var app = GetSwApp();
            if (app != null)
            {
                app.Visible = true;
            }
        }

        /// <summary>
        /// 释放对 SolidWorks 实例的引用。
        /// </summary>
        public static void Release()
        {
            try
            {
                if (_swApp != null)
                {
                    Marshal.ReleaseComObject(_swApp);
                    _swApp = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[SwApiHelper] Release failed: " + ex.Message);
            }
        }

        #endregion

        #region 文档操作

        /// <summary>
        /// 获取当前激活的文档。
        /// </summary>
        /// <returns>当前文档，或 null</returns>
        public static ModelDoc2 GetActiveDoc()
        {
            try
            {
                var app = GetSwApp();
                if (app == null) return null;

                return (ModelDoc2)app.ActiveDoc;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[SwApiHelper] GetActiveDoc failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 创建新的零件文档。
        /// </summary>
        /// <returns>新零件文档，或 null</returns>
        public static ModelDoc2 CreateNewPart()
        {
            try
            {
                var app = GetSwApp();
                if (app == null) return null;

                int docType = (int)swDocumentTypes_e.swDocPART;
                string template = app.GetDocumentTemplate(docType, 0, 0, 0);

                if (string.IsNullOrEmpty(template))
                {
                    // 尝试默认模板路径
                    string swPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    template = Path.Combine(swPath,
                        "SolidWorks Corp", "SolidWorks",
                        "lang", "chinese-simplified",
                        "Templates", "gb_part.prtdot");
                }

                return (ModelDoc2)app.NewDocument(template, 0, 0.0, 0.0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[SwApiHelper] CreateNewPart failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 保存当前文档。
        /// </summary>
        /// <param name="filePath">保存路径，为 null 时使用原路径</param>
        /// <returns>是否保存成功</returns>
        public static bool SaveDocument(string filePath = null)
        {
            try
            {
                var doc = GetActiveDoc();
                if (doc == null) return false;

                int errors = 0;
                int warnings = 0;

                if (!string.IsNullOrEmpty(filePath))
                {
                    string dir = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    return doc.SaveAs(filePath, (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                        (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);
                }
                else
                {
                    return doc.Save(ref errors, ref warnings);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[SwApiHelper] SaveDocument failed: " + ex.Message);
                return false;
            }
        }

        #endregion

        #region VBA 宏执行

        /// <summary>
        /// 在 SolidWorks 中执行 VBA 宏字符串。
        /// 
        /// 将宏代码保存为临时文件，然后通过 SW API 运行。
        /// </summary>
        /// <param name="code">VBA 宏代码</param>
        /// <param name="macroPath">可选：指定宏文件路径（不传则使用临时文件）</param>
        /// <returns>是否执行成功</returns>
        public static bool RunMacro(string code, string macroPath = null)
        {
            try
            {
                var app = GetSwApp();
                if (app == null)
                {
                    System.Diagnostics.Debug.WriteLine("[SwApiHelper] RunMacro: SolidWorks not connected");
                    return false;
                }

                // 写入临时文件
                string tempPath = macroPath;
                if (string.IsNullOrEmpty(tempPath))
                {
                    tempPath = Path.Combine(Path.GetTempPath(), "MechForge_TempMacro.bas");
                }

                File.WriteAllText(tempPath, code, System.Text.Encoding.UTF8);

                // 执行宏
                // 注意: SolidWorks 对宏的执行可能受宏安全设置影响
                bool result = app.RunMacro2(tempPath, "Module1", "main",
                    (int)swRunMacroOption_e.swRunMacroOptionUnloadAfterRun);

                if (!result)
                {
                    System.Diagnostics.Debug.WriteLine("[SwApiHelper] RunMacro returned false. " +
                        "Check SW macro security settings (Tools → Macro → Security)");
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[SwApiHelper] RunMacro failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 从已有 .bas 文件运行宏。
        /// </summary>
        /// <param name="macroFilePath">宏文件路径</param>
        /// <returns>是否执行成功</returns>
        public static bool RunMacroFile(string macroFilePath)
        {
            if (!File.Exists(macroFilePath))
            {
                System.Diagnostics.Debug.WriteLine("[SwApiHelper] Macro file not found: " + macroFilePath);
                return false;
            }

            string code = File.ReadAllText(macroFilePath, System.Text.Encoding.UTF8);
            return RunMacro(code, macroFilePath);
        }

        #endregion

        #region 错误处理

        /// <summary>
        /// 获取 SolidWorks 最后一次错误信息。
        /// </summary>
        public static string GetLastError()
        {
            try
            {
                var app = GetSwApp();
                if (app == null) return "SolidWorks not available";
                return app.GetLastError;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        #endregion
    }
}
