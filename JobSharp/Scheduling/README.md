# JobSharp Cron Expression Parser

JobSharp includes a custom cron expression parser that supports the standard 5-field cron format without any external dependencies.

## Supported Format

The cron expression format is: `minute hour day-of-month month day-of-week`

### Field Ranges

- **Minute**: 0-59
- **Hour**: 0-23 (24-hour format)
- **Day of Month**: 1-31
- **Month**: 1-12
- **Day of Week**: 0-7 (0 and 7 both represent Sunday)

## Supported Syntax

### Basic Values

- `*` - Matches any value
- `5` - Matches the specific value 5
- `1,3,5` - Matches values 1, 3, and 5

### Ranges

- `1-5` - Matches values 1 through 5
- `10-15` - Matches values 10 through 15

### Step Values

- `*/5` - Every 5 units (e.g., every 5 minutes)
- `1-10/2` - Every 2 units within the range 1-10
- `0/15` - Starting at 0, then every 15 units

### Combinations

You can combine the above using commas:
- `1,3,5-10,*/15` - Matches 1, 3, 5-10, and every 15 units

## Common Examples

| Expression | Description |
|------------|-------------|
| `0 0 * * *` | Every day at midnight |
| `0 */2 * * *` | Every 2 hours |
| `*/15 * * * *` | Every 15 minutes |
| `0 9 * * 1-5` | Every weekday at 9 AM |
| `0 0 1 * *` | First day of every month at midnight |
| `30 6 * * 0` | Every Sunday at 6:30 AM |
| `0 12 1 1 *` | Every January 1st at noon |
| `*/5 9-17 * * 1-5` | Every 5 minutes during business hours on weekdays |

## Usage in Code

```csharp
using JobSharp.Scheduling;

// Parse a cron expression
var cronExpression = CronExpression.Parse("0 9 * * 1-5");

// Check if a date matches
var matches = cronExpression.IsMatch(DateTime.Now);

// Get the next occurrence
var nextRun = cronExpression.GetNextOccurrence(DateTime.Now);
```

## Error Handling

The parser will throw `ArgumentException` for invalid cron expressions:

- Missing or extra fields
- Invalid field values
- Malformed ranges or step values
- Out-of-range values

## Limitations

This parser focuses on the most commonly used cron features. It does not support:

- Second-level precision (6-field format)
- Special strings like `@yearly`, `@monthly`, etc.
- Complex day-of-week/day-of-month logic beyond basic OR operation
- Time zone handling (uses system local time)

For most job scheduling needs, this parser provides sufficient functionality while keeping the library dependency-free. 