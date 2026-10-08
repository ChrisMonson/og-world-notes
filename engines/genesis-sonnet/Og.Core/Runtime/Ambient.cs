namespace Og.Core.Runtime;

/// <summary>
/// A single line of ambient light, appended to every room description. Driven
/// by wall-clock time alone — nothing a player does and nothing a caretaker
/// does changes it. It is the one thing in this world that is different when
/// you return than when you left, whether or not anyone was here to see it
/// change in between.
/// </summary>
public static class Ambient
{
    public static string DescribeLight(DateTimeOffset now)
    {
        var hour = now.UtcDateTime.Hour;

        return hour switch
        {
            >= 5 and < 8 => "The light is grey and early. Morning hasn't fully broken.",
            >= 8 and < 17 => "Full daylight sits overhead, flat and shadowless.",
            >= 17 and < 20 => "The light is going long and gold, shadows stretching further than they did an hour ago.",
            _ => "No light overhead but a scatter of stars. The dark makes everything feel smaller."
        };
    }
}
