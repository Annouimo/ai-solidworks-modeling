# 推荐运行入口

- Run-BatchModels.ps1 + BatchModels.cs：创建 20 个编号原生模型。
- Test-SolidWorks.ps1：只读 COM 连接测试。
- Inspect-Deliverables.ps1：只读复核保存的原生零件副本。
- Make-Overview.ps1：使用 Windows System.Drawing 生成两张总览图。
- Read-AllPages.ps1：可选 PowerPoint 导出工具，需自行在项目根目录提供输入 PPT。

请使用 Windows PowerShell，并修改各脚本中的接口库路径以匹配本机安装位置。重建指定编号会覆盖对应成果。运行步骤与模型限制见根目录 README。

历史宏、早期试验脚本和本机证书诊断脚本仅本地保留，不在公开提交中。

## 独立建模示例

在仓库根目录用 Windows PowerShell 运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\scripts\New-Spring.ps1
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\scripts\New-GearBearingAssembly.ps1
```

- `New-Spring.ps1`：原生螺旋线和圆截面扫描。默认 10 圈、平均圈径 30 mm、线径 3 mm、节距 8 mm。可用 `-Turns`、`-MeanDiameterMm`、`-WireDiameterMm`、`-PitchMm` 修改。输出在 `results/spring/`。
- `New-GearBearingAssembly.ps1` + `GearBearingAssembly.cs`：生成带法兰的滑动轴承、台阶轴、24 齿直齿轮和装配体。轴承固定，轴与轴承之间保留转动，齿轮和轴锁定相对转动。四个配合限制同轴度和轴向位置；脚本保存前检查实体干涉。齿廓采用分段近似。输出在 `results/gear-bearing/`，移动时请把装配体和三个零件放在一起。

两个脚本都可以通过 `-OutputDirectory` 指定输出位置，通过 `-InteropPath` 或 `-PartTemplate` 指定本机接口库及零件模板。装配脚本另支持 `-AssemblyTemplate`。
