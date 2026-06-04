using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MechForge
{
    /// <summary>
    /// MechForge 任务面板控件。
    /// 
    /// 包含：
    /// - 顶部 Logo + 标题
    /// - AI 模式 / 手动模式 Tab 切换
    /// - 日志输出框
    /// - 底部状态栏
    /// </summary>
    public partial class TaskPaneControl : UserControl
    {
        #region 字段

        private readonly ApiClient _apiClient;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 MechForge 任务面板。
        /// </summary>
        public TaskPaneControl()
        {
            InitializeComponent();
            _apiClient = new ApiClient("http://127.0.0.1:5757");

            // 默认选中 AI 模式
            tabControl1.SelectedIndex = 0;

            // 启动时检查后端健康状态
            _ = CheckBackendHealthAsync();
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 「AI 模式」生成按钮点击。
        /// </summary>
        private async void BtnAiGenerate_Click(object sender, EventArgs e)
        {
            string userInput = txtAiInput.Text.Trim();
            if (string.IsNullOrEmpty(userInput))
            {
                AppendLog("⚠️ 请输入设计需求", Color.Orange);
                return;
            }

            btnAiGenerate.Enabled = false;
            AppendLog($"🤖 AI 解析: {userInput}", Color.Gray);

            try
            {
                // Step 1: NLP 解析
                var nlpResult = await _apiClient.NlpParseAsync(userInput);
                if (!nlpResult.IsSuccess)
                {
                    AppendLog($"❌ NLP 解析失败: {nlpResult.Error}", Color.Red);
                    return;
                }

                string partType = nlpResult.Data?.GetValue("type")?.ToString() ?? "";
                AppendLog($"✅ 识别类型: {partType}", Color.Green);

                // Step 2: 设计计算
                AppendLog($"🔧 正在进行设计计算...", Color.Blue);
                object designParams = nlpResult.Data?["params"];

                var designResult = await _apiClient.DesignAsync(partType, designParams);
                if (!designResult.IsSuccess)
                {
                    AppendLog($"❌ 设计失败: {designResult.Error}", Color.Red);
                    return;
                }

                // 显示设计摘要
                string summary = designResult.Data?.GetValue("summary")?.ToString() ?? "";
                if (!string.IsNullOrEmpty(summary))
                {
                    AppendLog($"📐 设计结果:\n{summary}", Color.Black);
                }

                // Step 3: 生成 VBA 宏
                AppendLog($"📜 正在生成 VBA 宏...", Color.Blue);
                var macroResult = await _apiClient.GenerateMacroAsync(partType, designParams);
                if (macroResult.IsSuccess)
                {
                    string macroName = macroResult.Data?.GetValue("name")?.ToString() ?? "Unknown";
                    int? lines = (int?)macroResult.Data?.GetValue("lines");
                    AppendLog($"✅ VBA 宏生成: {macroName} ({lines ?? 0} 行)", Color.Green);

                    // 提示用户可以运行宏
                    AppendLog("  在 SolidWorks 中: 工具 → 宏 → 运行 → 选择 .bas 文件", Color.Gray);
                }
                else
                {
                    AppendLog($"⚠️ 宏生成失败: {macroResult.Error}", Color.Orange);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"❌ 错误: {ex.Message}", Color.Red);
            }
            finally
            {
                btnAiGenerate.Enabled = true;
            }
        }

        /// <summary>
        /// 「手动模式」生成按钮点击。
        /// </summary>
        private async void BtnManualGenerate_Click(object sender, EventArgs e)
        {
            string partType = cmbPartType.SelectedItem?.ToString() ?? "flange";
            btnManualGenerate.Enabled = false;

            try
            {
                object designParams = null;

                if (partType == "flange" || partType == "法兰")
                {
                    int dn = (int)numDn.Value;
                    int pn = (int)numPn.Value;
                    string flangeType = cmbFlangeType.SelectedItem?.ToString() ?? "plate";
                    string material = txtMaterial.Text.Trim();
                    if (string.IsNullOrEmpty(material)) material = "Q235B";

                    designParams = new
                    {
                        dn,
                        pn,
                        flange_type = flangeType,
                        material
                    };
                }
                else if (partType == "impeller" || partType == "离心风机")
                {
                    double Q = (double)numFanQ.Value;
                    double P = (double)numFanP.Value;
                    double n = (double)numFanN.Value;

                    designParams = new
                    {
                        Q,
                        P,
                        n,
                        blade_type = cmbBladeType.SelectedItem?.ToString() ?? "backward",
                        material = txtMaterial.Text.Trim() == "" ? "Q235B" : txtMaterial.Text.Trim()
                    };
                }
                else if (partType == "axial" || partType == "轴流风机")
                {
                    double Q = (double)numFanQ.Value;
                    double P = (double)numFanP.Value;
                    double n = (double)numFanN.Value;

                    designParams = new
                    {
                        Q,
                        P,
                        n,
                        airfoil = cmbAirfoil.SelectedItem?.ToString() ?? "clark_y",
                        material = txtMaterial.Text.Trim() == "" ? "Q235B" : txtMaterial.Text.Trim()
                    };
                }
                else
                {
                    AppendLog("⚠️ 未知零件类型", Color.Orange);
                    return;
                }

                AppendLog($"🔧 手动模式: {partType}", Color.Gray);

                // 设计计算
                var designResult = await _apiClient.DesignAsync(partType, designParams);
                if (!designResult.IsSuccess)
                {
                    AppendLog($"❌ 设计失败: {designResult.Error}", Color.Red);
                    return;
                }

                string summary = designResult.Data?.GetValue("summary")?.ToString() ?? "";
                if (!string.IsNullOrEmpty(summary))
                {
                    AppendLog($"📐 设计结果:\n{summary}", Color.Black);
                }

                // 生成 VBA 宏
                AppendLog($"📜 正在生成 VBA 宏...", Color.Blue);
                var macroResult = await _apiClient.GenerateMacroAsync(partType, designParams);
                if (macroResult.IsSuccess)
                {
                    string macroName = macroResult.Data?.GetValue("name")?.ToString() ?? "Unknown";
                    int? lines = (int?)macroResult.Data?.GetValue("lines");
                    AppendLog($"✅ VBA 宏生成: {macroName} ({lines ?? 0} 行)", Color.Green);
                }
                else
                {
                    AppendLog($"⚠️ 宏生成失败: {macroResult.Error}", Color.Orange);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"❌ 错误: {ex.Message}", Color.Red);
            }
            finally
            {
                btnManualGenerate.Enabled = true;
            }
        }

        /// <summary>
        /// 零件类型下拉选择变更。
        /// </summary>
        private void CmbPartType_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selected = cmbPartType.SelectedItem?.ToString() ?? "";
            UpdateParameterPanel(selected);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 根据选择的零件类型更新参数面板显示。
        /// </summary>
        private void UpdateParameterPanel(string partType)
        {
            // 默认隐藏所有参数组
            pnlFlangeParams.Visible = false;
            pnlFanParams.Visible = false;
            pnlCommonParams.Visible = true;

            if (partType == "flange" || partType == "法兰")
            {
                pnlFlangeParams.Visible = true;
                pnlFanParams.Visible = false;
            }
            else if (partType == "impeller" || partType == "离心风机" ||
                     partType == "axial" || partType == "轴流风机")
            {
                pnlFlangeParams.Visible = false;
                pnlFanParams.Visible = true;
            }
            else
            {
                pnlCommonParams.Visible = true;
            }
        }

        /// <summary>
        /// 异步检查后端健康状况。
        /// </summary>
        private async Task CheckBackendHealthAsync()
        {
            try
            {
                var healthResult = await _apiClient.HealthCheckAsync();
                if (healthResult.IsSuccess)
                {
                    string version = healthResult.Data?.GetValue("version")?.ToString() ?? "?";
                    AppendLog($"✅ 后端连接成功 (v{version})", Color.Green);
                }
                else
                {
                    AppendLog("⚠️ 后端未响应，请确保 API 服务器已启动", Color.Orange);
                    AppendLog("   运行: python api.py --port 5757", Color.Gray);
                }
            }
            catch (Exception ex)
            {
                AppendLog("⚠️ 后端连接失败: " + ex.Message, Color.Orange);
                AppendLog("   运行: python api.py --port 5757", Color.Gray);
            }
        }

        /// <summary>
        /// 在日志框中追加文本，支持颜色。
        /// </summary>
        private void AppendLog(string text, Color color)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => AppendLog(text, color)));
                return;
            }

            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionLength = 0;
            txtLog.SelectionColor = color;
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}\n");
            txtLog.ScrollToCaret();
        }

        #endregion
    }
}
