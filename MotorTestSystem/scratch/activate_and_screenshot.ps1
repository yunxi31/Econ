Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WindowAPI {
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
"@

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$process = Get-Process -Name "MotorTestSystem" -ErrorAction SilentlyContinue
if ($process) {
    $hWnd = $process.MainWindowHandle
    if ($hWnd -ne [IntPtr]::Zero) {
        # 3 = ShowMaximized, 5 = Show, 9 = Restore
        [WindowAPI]::ShowWindow($hWnd, 9)
        [WindowAPI]::SetForegroundWindow($hWnd)
        Start-Sleep -Milliseconds 800
    }
}

$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap $screen.Width, $screen.Height
$graphics = [System.Drawing.Graphics]::FromImage($bmp)
$graphics.CopyFromScreen($screen.X, $screen.Y, 0, 0, $screen.Size)
$bmp.Save("C:\Users\Yunxi\Desktop\Econ\MotorTestSystem\screenshot.png", [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bmp.Dispose()
write-output "Activated and screenshot saved successfully."
