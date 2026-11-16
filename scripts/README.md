# 当前推荐入口

- Run-BatchModels.ps1 + BatchModels.cs：PPT 第二页至末页的 20 个编号模型。
- Inspect-Deliverables.ps1：只读检查保存的原生文件副本。
- Make-Overview.ps1：生成模型总览；使用 Windows 自带 System.Drawing。
- Read-AllPages.ps1：只读导出 PPT 页面。
- Test-SolidWorks.ps1：只读 COM 连接测试。

New-*.ps1、Inspect-Demos.ps1、Read-Page2.ps1 属于前期试验脚本；Make-Overview.py 是未采用的 Pillow 版本，本机缺少该依赖，请使用同名 PowerShell 脚本。

“历史_”开头的脚本及宏仅供追溯，其中手写 STEP 导入路线失败且已停止。Macro1.swp 为用户此前已有的宏，内容未检查。

统一成果目录与页码对应见 ../deliverables/README.md。
