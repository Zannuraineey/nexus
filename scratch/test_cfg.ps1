Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class CfgMgrTest {
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Auto)]
    public static extern int CM_Locate_DevNode(out uint dnDevInst, string pDeviceID, int ulFlags);
}
"@

$dn1 = [uint32]0
$dn2 = [uint32]0
$r1 = [CfgMgrTest]::CM_Locate_DevNode([ref]$dn1, 'USB\VID_19D2&PID_1557\1234567890ABCDEF', 0)
$r2 = [CfgMgrTest]::CM_Locate_DevNode([ref]$dn2, 'USB\VID_19D2&PID_1225\1234567890ABCDEF', 0)
Write-Output "Result 1557 (Connected): $r1"
Write-Output "Result 1225 (Disconnected): $r2"
