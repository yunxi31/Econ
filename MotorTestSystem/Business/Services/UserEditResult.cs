namespace MotorTestSystem.Services
{
    public record UserEditResult(string Account, string Name, string Password, string Role, bool IsEnabled);
}
