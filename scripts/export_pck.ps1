#Requires -Version 5.1
<#
.SYNOPSIS
  使用 Godot 4（.NET 版）命令行导出 SpeechTheSpire.pck。

.DESCRIPTION
  - 必须使用与 project.godot 中 features 一致的 Godot .NET 编辑器（当前为 4.5 + C#）。
  - 首次导出前请在 Godot 编辑器中打开本项目：项目 -> 导出 -> 选择「Windows Desktop」，
    安装/下载导出模板，再运行本脚本（否则 CLI 会报缺少 template）。
  - PCK 主要包含 localization 等资源；游戏逻辑在 SpeechTheSpire.dll，需单独 dotnet build 并放入 mods 目录。

.PARAMETER GodotExe
  Godot .NET 可执行文件路径。也可用环境变量 GODOT_MONO。
  建议用带 console 的启动方式，例如同目录下的 Godot*_console.cmd（便于看到报错）。

.PARAMETER OutPath
  输出的 .pck 完整路径；默认：项目根目录下 export\SpeechTheSpire.pck

.EXAMPLE
  .\scripts\export_pck.ps1 -GodotExe "D:\Godot\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.cmd"

.EXAMPLE
  $env:GODOT_MONO = "D:\Godot\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.cmd"
  .\scripts\export_pck.ps1
#>
param(
	[string] $GodotExe = $env:GODOT_MONO,
	[string] $OutPath = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$PresetName = "Windows Desktop"

if ([string]::IsNullOrWhiteSpace($GodotExe) -or -not (Test-Path -LiteralPath $GodotExe)) {
	Write-Host @"
未找到 Godot .NET。请任选其一：

  1) 临时指定：
     .\scripts\export_pck.ps1 -GodotExe `"你的路径\Godot_v4.5.x-stable_mono_win64_console.cmd`"

  2) 设置用户环境变量 GODOT_MONO 为上述路径后，直接运行本脚本。

请从 https://godotengine.org/download/windows/ 下载「.NET」版，版本号尽量与 project.godot 的 4.5 一致。
"@
	exit 1
}

if ([string]::IsNullOrWhiteSpace($OutPath)) {
	$OutPath = Join-Path $ProjectRoot "export\SpeechTheSpire.pck"
}

$exportDir = Split-Path -Parent $OutPath
New-Item -ItemType Directory -Force -Path $exportDir | Out-Null

Write-Host "dotnet build (Release)..."
dotnet build (Join-Path $ProjectRoot "SpeechTheSpire.csproj") -c Release -v minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Godot --export-pack -> $OutPath"
& $GodotExe --headless --path $ProjectRoot --export-pack $PresetName $OutPath
if ($LASTEXITCODE -ne 0) {
	Write-Host "导出失败（退出码 $LASTEXITCODE）。若提示缺少 export template，请先在编辑器里打开导出窗口并安装 Windows 模板。"
	exit $LASTEXITCODE
}

Write-Host "完成: $OutPath"
Write-Host "将 SpeechTheSpire.dll、SpeechTheSpire.json 与此 pck 一并放入游戏的 mods\SpeechTheSpire\ 目录（与 manifest 中 has_pck 一致）。"
