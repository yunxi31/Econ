Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$root = [System.Windows.Automation.AutomationElement]::RootElement
$condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "系统登录")
$window = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
if ($window -ne $null) {
    $buttonCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
    $buttons = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $buttonCondition)
    foreach ($btn in $buttons) {
        Write-Output "Found button: $($btn.Current.Name)"
        if ($btn.Current.Name -like "*登*" -or $btn.Current.Name -like "*Log*") {
            $invokePattern = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
            $invokePattern.Invoke()
            Write-Output "Clicked login button"
            break
        }
    }
} else {
    Write-Output "Login window not found"
}
