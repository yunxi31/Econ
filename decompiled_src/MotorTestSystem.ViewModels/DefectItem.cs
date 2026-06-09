using System.Windows.Media;

namespace MotorTestSystem.ViewModels;

public class DefectItem
{
	public string Name { get; set; } = string.Empty;

	public double Percentage { get; set; }

	public string Color { get; set; } = "#8E9AA7";

	public Brush ColorBrush => (Brush)new BrushConverter().ConvertFromString(Color);
}
