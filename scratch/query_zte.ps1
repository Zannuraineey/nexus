$js = curl.exe -s --max-time 5 "http://192.168.0.1/js/service.js"
$idx = $js.IndexOf("function em(")
if ($idx -ge 0) {
    Write-Output "--- FUNCTION EM (getDeviceInfo) ---"
    Write-Output $js.Substring($idx, 500)
}

$idxUnlock = $js.IndexOf("function co(")
if ($idxUnlock -ge 0) {
    Write-Output "--- FUNCTION CO (getNetworkUnlockTimes) ---"
    Write-Output $js.Substring($idxUnlock, 500)
}

$idxApply = $js.IndexOf("function dL(")
if ($idxApply -ge 0) {
    Write-Output "--- FUNCTION DL (unlockNetwork) ---"
    Write-Output $js.Substring($idxApply, 500)
}
