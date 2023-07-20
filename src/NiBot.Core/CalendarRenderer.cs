namespace Aiursoft.NiBot.Core;

public class CalendarRenderer
{
    private readonly Random _rand = new();

    public void Render()
    {
        // 获取当前日期
        DateTime now = DateTime.Now;

        // 获取当前月份的第一天
        DateTime firstDayOfMonth = new DateTime(now.Year, now.Month, 1);

        // 获取当前月份的最后一天
        DateTime lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

        // 输出表头
        Console.WriteLine("| {0,-16} | {1,-16} | {2,-16} | {3,-16} | {4,-16} | {5,-16} | {6,-16} |", "Sunday", "Monday",
            "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday");

        // 输出分隔线
        Console.WriteLine("|{0}|{1}|{2}|{3}|{4}|{5}|{6}|", new string('-', 18), new string('-', 18),
            new string('-', 18), new string('-', 18), new string('-', 18), new string('-', 18), new string('-', 18));

        // 获取当前月份的第一周的日期
        DateTime firstWeekDay = firstDayOfMonth.AddDays(-(int)firstDayOfMonth.DayOfWeek);

        // 获取当前月份的最后一周的日期
        DateTime lastWeekDay = lastDayOfMonth.AddDays(6 - (int)lastDayOfMonth.DayOfWeek);

        // 循环输出日期
        for (DateTime date = firstWeekDay; date <= lastWeekDay; date = date.AddDays(1))
        {
            // 输出日期
            Console.Write("| {0,-4} ", date.Day);
            Console.Write(" {0,-10} ", $"({GetWeather()})");

            if (date.DayOfWeek == DayOfWeek.Saturday)
            {
                Console.WriteLine("|");
            }
        }

        Console.WriteLine();
    }

    private string GetWeather()
    {
        string[] weathers = { "Sunny", "Cloudy", "Rainy", "Snowy", "Foggy", "Windy", "Stormy" };

        return weathers[_rand.Next(0, weathers.Length)];
    }
}