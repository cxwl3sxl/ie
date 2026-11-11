param(
    [Parameter(Mandatory = $true)]
    [string]$Path
)

# 清除 PE 头 DllCharacteristics 中的 NXCOMPAT(0x0100) 位。
#
# 为什么需要：griectl.dll（Cognos GRID 报表控件）使用运行时动态 thunk --
# 把机器码写进 PAGE_READWRITE|MEM_PRIVATE 内存后 jmp 过去执行。
# 进程若启用 DEP（NXCOMPAT），该内存页不可执行，跳转即 c0000005。
# 32 位 IE 自身不带 NXCOMPAT，所以控件在 IE 中工作正常；本程序需要一致。

if (-not (Test-Path -LiteralPath $Path)) {
    Write-Error "文件不存在: $Path"
    exit 1
}

$bytes = [System.IO.File]::ReadAllBytes($Path)

# 校验 MZ 头
if ($bytes[0] -ne 0x4D -or $bytes[1] -ne 0x5A) {
    Write-Error "不是有效的 PE 文件: $Path"
    exit 1
}

$peOffset = [BitConverter]::ToInt32($bytes, 0x3C)
if ($bytes[$peOffset] -ne 0x50 -or $bytes[$peOffset + 1] -ne 0x45) {
    Write-Error "PE 签名无效: $Path"
    exit 1
}

# DllCharacteristics 位于可选头起始 +70 字节
$optHeaderStart = $peOffset + 24
$dllCharOffset = $optHeaderStart + 70

$old = [BitConverter]::ToUInt16($bytes, $dllCharOffset)
if (($old -band 0x0100) -eq 0) {
    Write-Host "已是 NXCOMPAT=False，无需修改: $Path"
    exit 0
}

$new = $old -band (-bnot 0x0100)
$bytes[$dllCharOffset] = [byte]($new -band 0xFF)
$bytes[$dllCharOffset + 1] = [byte](($new -shr 8) -band 0xFF)

# 重算校验和（IMAGE_OPTIONAL_HEADER.CheckSum），否则部分系统会拒绝加载
$checkSumOffset = $optHeaderStart + 64
$bytes[$checkSumOffset] = 0
$bytes[$checkSumOffset + 1] = 0
$bytes[$checkSumOffset + 2] = 0
$bytes[$checkSumOffset + 3] = 0

$sum = [int64]0
$len = $bytes.Length
$i = 0
while ($i + 1 -lt $len) {
    $sum += [BitConverter]::ToUInt16($bytes, $i)
    if ($sum -gt 0xFFFFFFFF) { $sum = ($sum -band 0xFFFFFFFF) + ($sum -shr 32) }
    $i += 2
}
if ($i -lt $len) { $sum += $bytes[$i] }
$sum = ($sum -band 0xFFFF) + ($sum -shr 16)
$sum = ($sum -band 0xFFFF) + ($sum -shr 16)
$sum = $sum + $len

$bytes[$checkSumOffset] = [byte]($sum -band 0xFF)
$bytes[$checkSumOffset + 1] = [byte](($sum -shr 8) -band 0xFF)
$bytes[$checkSumOffset + 2] = [byte](($sum -shr 16) -band 0xFF)
$bytes[$checkSumOffset + 3] = [byte](($sum -shr 24) -band 0xFF)

[System.IO.File]::WriteAllBytes($Path, $bytes)

Write-Host ("已清除 NXCOMPAT: 0x{0:X4} -> 0x{1:X4}  {2}" -f $old, $new, (Split-Path $Path -Leaf))
