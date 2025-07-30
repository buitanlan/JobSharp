namespace JobSharp.Scheduling;

/// <summary>
/// Represents a cron expression and provides methods to calculate next occurrence times.
/// Supports the standard 5-field cron format: minute hour day-of-month month day-of-week
/// </summary>
public class CronExpression
{
    private readonly int[] _minutes;
    private readonly int[] _hours;
    private readonly int[] _daysOfMonth;
    private readonly int[] _months;
    private readonly int[] _daysOfWeek;

    private CronExpression(int[] minutes, int[] hours, int[] daysOfMonth, int[] months, int[] daysOfWeek)
    {
        _minutes = minutes;
        _hours = hours;
        _daysOfMonth = daysOfMonth;
        _months = months;
        _daysOfWeek = daysOfWeek;
    }

    /// <summary>
    /// Parses a cron expression string into a CronExpression object.
    /// </summary>
    /// <param name="cronExpression">The cron expression string (5 fields: minute hour day-of-month month day-of-week)</param>
    /// <returns>A CronExpression object</returns>
    /// <exception cref="ArgumentException">Thrown when the cron expression is invalid</exception>
    public static CronExpression Parse(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            throw new ArgumentException("Cron expression cannot be null or empty", nameof(cronExpression));

        var fields = cronExpression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5)
            throw new ArgumentException("Cron expression must have exactly 5 fields: minute hour day-of-month month day-of-week", nameof(cronExpression));

        try
        {
            var minutes = ParseField(fields[0], 0, 59);
            var hours = ParseField(fields[1], 0, 23);
            var daysOfMonth = ParseField(fields[2], 1, 31);
            var months = ParseField(fields[3], 1, 12);
            var daysOfWeek = ParseField(fields[4], 0, 7); // 0 and 7 both represent Sunday

            // Convert Sunday from 7 to 0 for consistency
            for (int i = 0; i < daysOfWeek.Length; i++)
            {
                if (daysOfWeek[i] == 7)
                    daysOfWeek[i] = 0;
            }

            return new CronExpression(minutes, hours, daysOfMonth, months, daysOfWeek);
        }
        catch (Exception ex) when (!(ex is ArgumentException))
        {
            throw new ArgumentException($"Invalid cron expression: {cronExpression}", nameof(cronExpression), ex);
        }
    }

    /// <summary>
    /// Gets the next occurrence of this cron expression after the specified date.
    /// </summary>
    /// <param name="afterTime">The date after which to find the next occurrence</param>
    /// <returns>The next occurrence date</returns>
    public DateTime GetNextOccurrence(DateTime afterTime)
    {
        var next = afterTime.AddMinutes(1);
        next = new DateTime(next.Year, next.Month, next.Day, next.Hour, next.Minute, 0, DateTimeKind.Unspecified);

        // Limit search to prevent infinite loops (search up to 4 years)
        var maxSearchTime = afterTime.AddYears(4);

        while (next <= maxSearchTime)
        {
            if (IsMatch(next))
                return next;

            next = next.AddMinutes(1);
        }

        throw new InvalidOperationException("Could not find next occurrence within reasonable time range");
    }

    /// <summary>
    /// Checks if the given date matches this cron expression.
    /// </summary>
    /// <param name="dateTime">The date to check</param>
    /// <returns>True if the date matches the cron expression</returns>
    public bool IsMatch(DateTime dateTime)
    {
        return _minutes.Contains(dateTime.Minute) &&
               _hours.Contains(dateTime.Hour) &&
               _months.Contains(dateTime.Month) &&
               (IsDateMatch(dateTime) || IsDayOfWeekMatch(dateTime));
    }

    private bool IsDateMatch(DateTime dateTime)
    {
        return _daysOfMonth.Contains(dateTime.Day);
    }

    private bool IsDayOfWeekMatch(DateTime dateTime)
    {
        var dayOfWeek = (int)dateTime.DayOfWeek;
        return _daysOfWeek.Contains(dayOfWeek);
    }

    private static int[] ParseField(string field, int min, int max)
    {
        if (field == "*")
        {
            return Enumerable.Range(min, max - min + 1).ToArray();
        }

        var values = new List<int>();

        var parts = field.Split(',');
        foreach (var part in parts)
        {
            if (part.Contains('/'))
            {
                // Handle step values like */5 or 1-10/2
                var stepParts = part.Split('/');
                if (stepParts.Length != 2 || !int.TryParse(stepParts[1], out var step) || step <= 0)
                    throw new ArgumentException($"Invalid step value in field: {field}");

                int[] baseValues;
                if (stepParts[0] == "*")
                {
                    baseValues = Enumerable.Range(min, max - min + 1).ToArray();
                }
                else if (stepParts[0].Contains('-'))
                {
                    baseValues = ParseRange(stepParts[0], min, max);
                }
                else
                {
                    if (!int.TryParse(stepParts[0], out var start) || start < min || start > max)
                        throw new ArgumentException($"Invalid start value in step field: {field}");
                    baseValues = [start];
                }

                for (int i = 0; i < baseValues.Length; i += step)
                {
                    values.Add(baseValues[i]);
                }
            }
            else if (part.Contains('-'))
            {
                // Handle ranges like 1-5
                values.AddRange(ParseRange(part, min, max));
            }
            else
            {
                // Handle single values
                if (!int.TryParse(part, out var value) || value < min || value > max)
                    throw new ArgumentException($"Invalid value in field: {field}");
                values.Add(value);
            }
        }

        return values.Distinct().OrderBy(x => x).ToArray();
    }

    private static int[] ParseRange(string range, int min, int max)
    {
        var rangeParts = range.Split('-');
        if (rangeParts.Length != 2 ||
            !int.TryParse(rangeParts[0], out var start) ||
            !int.TryParse(rangeParts[1], out var end) ||
            start < min || start > max ||
            end < min || end > max ||
            start > end)
        {
            throw new ArgumentException($"Invalid range: {range}");
        }

        return Enumerable.Range(start, end - start + 1).ToArray();
    }
}