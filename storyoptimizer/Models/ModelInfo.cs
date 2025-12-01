namespace StoryOptimizer.Models;

public class ModelInfo
{
    public int MaxContext { get; set; } = 0;  // 0 = not detected, makes failures obvious
    public double ModelSizeInBillions { get; set; } = 7.0;
    public double QuantizationBits { get; set; } = 4.5;
}
