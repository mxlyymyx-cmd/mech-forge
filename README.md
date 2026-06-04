# SolidWorks 参数化设计 — 机械行业 AI 自动化基座 🏗️

> 事业部一（机械+AI）Phase 1 技术架子
>
> 「你说规格，AI 画图」

## 架构

```
用户输入（自然语言）
    │
    ▼
┌─────────────────────┐
│  AI 参数提取 Pipeline │   ← LLM 从自然语言提取结构化参数
└────────┬────────────┘
         │
         ▼
┌─────────────────────┐
│  法兰参数模型        │   ← dataclass 标准化参数
│  + 国标参数数据库     │   ← GB/T 9112-2010 系列标准尺寸
└────────┬────────────┘
         │
         ▼
┌─────────────────────┐
│  SolidWorks 生成器    │   ← COM API 自动建模
└─────────────────────┘
         │
         ▼
      法兰 3D 模型 + 工程图
```

## 模块

| 模块 | 文件 | 职责 |
|------|------|------|
| 数据模型 | `flange/params.py` | 法兰参数 dataclass，输入输出标准化 |
| 国标数据库 | `flange/gb_standards.py` | GB/T 911X 系列标准参数 |
| AI 提取器 | `flange/ai_extractor.py` | LLM 自然语言 → 结构化参数 |
| SW 生成器 | `flange/generator.py` | SolidWorks COM API 驱动建模 |
| Pipeline | `flange/pipeline.py` | 完整流程编排 |
| CLI | `main.py` | 命令行入口 |

## 快速开始

```bash
# 安装依赖
pip install -r requirements.txt

# 查询法兰参数（不需要 SolidWorks）
python main.py query DN100 PN16

# AI 提取参数（需要配置 LLM API）
python main.py extract "给我一个DN100 PN16的平焊法兰，4个螺栓孔"

# 生成 SolidWorks 模型（需要 Windows + SolidWorks）
python main.py generate DN100 PN16 --type plate
```

## 支持的标准

- GB/T 9119-2010 板式平焊钢制管法兰
- GB/T 9116-2010 带颈平焊钢制管法兰
- GB/T 9115-2010 对焊钢制管法兰
