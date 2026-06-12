Add-Type -AssemblyName UIAutomationClient
$root = [System.Windows.Automation.AutomationElement]::RootElement
$proc = Get-Process -Name "MotorTestSystem" -ErrorAction SilentlyContinue
if ($proc) {
    Write-Output "Found process MotorTestSystem with ID: $($proc.Id)"
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
    $windows = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
    foreach ($w in $windows) {
        Write-Output "Window Name: '$($w.Current.Name)'"
        # Let's find buttons in this window
        $buttonCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
        $buttons = $w.FindAll([System.Windows.Automation.TreeScope]::Descendants, $buttonCondition)
        foreach ($btn in $buttons) {
            Write-Output "  Button: '$($btn.Current.Name)'"
        }
    }
} else {
    Write-Output "Process MotorTestSystem not found."
}
