# 推荐运行入口

- Run-BatchModels.ps1 + BatchModels.cs：创建 20 个编号原生模型。
- Test-SolidWorks.ps1：只读 COM 连接测试。
- Inspect-Deliverables.ps1：只读复核保存的原生零件副本。
- Make-Overview.ps1：使用 Windows System.Drawing 生成两张总览图。
- Read-AllPages.ps1：可选 PowerPoint 导出工具，需自行在项目根目录提供输入 PPT。

请使用 Windows PowerShell，并修改各脚本中的接口库路径以匹配本机安装位置。重建指定编号会覆盖对应成果。运行步骤与模型限制见根目录 README。

历史宏、早期试验脚本和本机证书诊断脚本仅本地保留，不在公开提交中。
