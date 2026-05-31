using WinDeskReminder.Models;
using WinDeskReminder.Services;

var tests = new (string Name, Action Body)[]
{
    ("Primary action starts and completes a reminder", PrimaryActionStartsAndCompletesReminder),
    ("Applying unchanged settings preserves reminder state", ApplySettingsPreservesReminderState),
    ("Quiet hours update the system state", QuietHoursUpdateSystemState)
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine(exception);
    }
}

return failures == 0 ? 0 : 1;

static void PrimaryActionStartsAndCompletesReminder()
{
    using var controller = CreateController();
    var item = controller.Items[0];
    var completions = 0;
    controller.ReminderCompleted += completed =>
    {
        AssertSame(item, completed);
        completions++;
    };

    item.Phase = ReminderPhase.AwaitingConfirmation;
    item.Remaining = TimeSpan.Zero;

    controller.PrimaryAction(item);
    AssertEqual(ReminderPhase.Resting, item.Phase);
    AssertEqual(item.ActionDuration, item.Remaining);

    controller.PrimaryAction(item);
    AssertEqual(ReminderPhase.Working, item.Phase);
    AssertEqual(item.WorkDuration, item.Remaining);
    AssertEqual(1, completions);
}

static void ApplySettingsPreservesReminderState()
{
    var settings = CreateSettings();
    using var controller = new ReminderController(settings);
    var item = controller.Items[0];
    item.Phase = ReminderPhase.Resting;
    item.Remaining = TimeSpan.FromMinutes(3);

    var updatedSettings = settings.Clone();
    updatedSettings.Reminders[0].Name = "站立一下";
    updatedSettings.Reminders[0].IconKind = "water";
    updatedSettings.Reminders[0].SoundEnabled = false;

    controller.ApplySettings(updatedSettings);

    var updatedItem = controller.Items[0];
    AssertSame(item, updatedItem);
    AssertEqual(ReminderPhase.Resting, updatedItem.Phase);
    AssertEqual(TimeSpan.FromMinutes(3), updatedItem.Remaining);
    AssertEqual("站立一下", updatedItem.Name);
    AssertEqual("water", updatedItem.IconKind);
    AssertFalse(updatedItem.SoundEnabled);
}

static void QuietHoursUpdateSystemState()
{
    var settings = CreateSettings();
    settings.QuietHoursEnabled = true;
    settings.QuietHoursStart = "00:00";
    settings.QuietHoursEnd = "23:59";

    using var controller = new ReminderController(settings);
    controller.Start();

    AssertContains("勿扰时段", controller.SystemStateText);
}

static ReminderController CreateController()
{
    return new ReminderController(CreateSettings());
}

static AppSettings CreateSettings()
{
    return new AppSettings
    {
        Reminders =
        [
            new ReminderDefinition
            {
                Id = "stand",
                Name = "站立",
                IconKind = "stand",
                WorkMinutes = 45,
                ActionMinutes = 5,
                IsEnabled = true,
                SoundEnabled = true
            }
        ]
    };
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, actual {actual}.");
    }
}

static void AssertSame(object expected, object actual)
{
    if (!ReferenceEquals(expected, actual))
    {
        throw new InvalidOperationException("Expected both references to point to the same object.");
    }
}

static void AssertFalse(bool value)
{
    if (value)
    {
        throw new InvalidOperationException("Expected false.");
    }
}

static void AssertContains(string expected, string actual)
{
    if (!actual.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected '{actual}' to contain '{expected}'.");
    }
}
