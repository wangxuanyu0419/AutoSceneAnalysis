$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $taskRoot
if (-not (Test-Path external\moge)) { git clone https://github.com/microsoft/MoGe.git external\moge }
if (-not (Test-Path external\utils3d-moge)) { git clone https://github.com/EasternJournalist/utils3d-moge.git external\utils3d-moge }
git -C external\moge fetch origin 74fbce054ebed49800de42d0ad0e83495065719a
git -C external\moge checkout --detach 74fbce054ebed49800de42d0ad0e83495065719a
git -C external\utils3d-moge checkout --detach 62f09d58509485564e24d5d9f6aac9ee9ebc0c37
.\.venv\Scripts\python.exe -m pip install scipy==1.15.3 opencv-python-headless==4.11.0.86
# Import only MoGe-2 inference from the pinned source trees. This preserves the
# existing NumPy/PyTorch stack and avoids MoGe-3's unused sparse-CUDA dependencies.
