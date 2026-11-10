namespace SmartX.Shared.Models;

public readonly record struct PowerReading
{
    public int Watts { get; }

    public PowerReading(int watts)
    {
        if (watts < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(watts),
                "Power consumption cannot be negative.");
        }

        Watts = watts;
    }

    public static PowerReading operator +(
        PowerReading left,
        PowerReading right)
    {
        return new PowerReading(checked(left.Watts + right.Watts));
    }

    public static PowerReading operator -(
        PowerReading left,
        PowerReading right)
    {
        return new PowerReading(checked(left.Watts - right.Watts));
    }

    public static bool operator >(
        PowerReading left,
        PowerReading right)
    {
        return left.Watts > right.Watts;
    }

    public static bool operator <(
        PowerReading left,
        PowerReading right)
    {
        return left.Watts < right.Watts;
    }

    public static bool operator >=(
        PowerReading left,
        PowerReading right)
    {
        return left.Watts >= right.Watts;
    }

    public static bool operator <=(
        PowerReading left,
        PowerReading right)
    {
        return left.Watts <= right.Watts;
    }
}