using System.Net;
using PrivacyBrowser.App;

var tests = new (string Name, Action Run)[]
{
    ("all feedback states are explicit", AllFeedbackStatesAreExplicit),
    ("idle and successful transition", IdleAndSuccessfulTransition),
    ("one operation at a time", OneOperationAtATime),
    ("per-page feedback is retained", PerPageFeedbackIsRetained),
    ("older completion cannot overwrite newer feedback", OlderCompletionCannotOverwriteNewerFeedback),
    ("session cleanup always releases busy state", SessionCleanupAlwaysReleasesBusyState),
    ("errors are safely classified", ErrorsAreSafelyClassified),
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS: {test.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL: {test.Name}: {exception.Message}");
    }
}

if (failures > 0) return 1;
Console.WriteLine($"PASS: all {tests.Length} operation feedback tests passed.");
return 0;

static void AllFeedbackStatesAreExplicit()
{
    var actual = Enum.GetNames<OperationFeedbackKind>();
    var expected = new[] { "Idle", "Progress", "Success", "RetryableFailure", "BlockingFailure" };
    Equal(string.Join(",", expected), string.Join(",", actual));
}

static void IdleAndSuccessfulTransition()
{
    var store = new OperationFeedbackStore();
    Equal(OperationFeedbackKind.Idle, store.Get(OperationFeedbackArea.Identity).Kind);
    using var operation = Required(store.TryStart(OperationFeedbackArea.Identity, "Unlocking"));
    Equal(OperationFeedbackKind.Progress, store.Get(OperationFeedbackArea.Identity).Kind);
    True(operation.Complete(OperationFeedbackKind.Success, "Unlocked"));
    Equal(OperationFeedbackKind.Success, store.Get(OperationFeedbackArea.Identity).Kind);
}

static void OneOperationAtATime()
{
    var store = new OperationFeedbackStore();
    using var operation = Required(store.TryStart(OperationFeedbackArea.Identity, "Importing"));
    True(store.IsBusy);
    True(store.TryStart(OperationFeedbackArea.Wallet, "Refreshing") is null);
}

static void PerPageFeedbackIsRetained()
{
    var store = new OperationFeedbackStore();
    store.Publish(OperationFeedbackArea.Wallet, OperationFeedbackKind.Success, "Balance refreshed");
    store.Publish(OperationFeedbackArea.Connection, OperationFeedbackKind.RetryableFailure, "Try another provider");
    Equal("Balance refreshed", store.Get(OperationFeedbackArea.Wallet).Message);
    Equal(OperationFeedbackKind.RetryableFailure, store.Get(OperationFeedbackArea.Connection).Kind);
    Equal(OperationFeedbackKind.Idle, store.Get(OperationFeedbackArea.Identity).Kind);
}

static void OlderCompletionCannotOverwriteNewerFeedback()
{
    var store = new OperationFeedbackStore();
    using var older = Required(store.TryStart(OperationFeedbackArea.Identity, "Unlocking"));
    store.Publish(OperationFeedbackArea.Identity, OperationFeedbackKind.Success, "A newer selection won");
    True(!older.Complete(OperationFeedbackKind.BlockingFailure, "Stale failure"));
    Equal("A newer selection won", store.Get(OperationFeedbackArea.Identity).Message);
}

static void SessionCleanupAlwaysReleasesBusyState()
{
    var store = new OperationFeedbackStore();
    try
    {
        using var operation = Required(store.TryStart(OperationFeedbackArea.Identity, "Importing"));
        throw new InvalidOperationException("simulated action failure");
    }
    catch (InvalidOperationException)
    {
    }

    True(!store.IsBusy);
    using var next = Required(store.TryStart(OperationFeedbackArea.Wallet, "Refreshing"));
    True(store.IsBusy);
}

static void ErrorsAreSafelyClassified()
{
    var timeout = BackendErrorTranslator.ToUserError(new TimeoutException());
    Equal(UserErrorKind.Retryable, timeout.Kind);

    var unlock = BackendErrorTranslator.ToUserError(new BackendApiException(
        "The identity could not be unlocked. Check the passphrase and try again.",
        "diagnostic",
        "err_id_unlock",
        HttpStatusCode.BadRequest));
    Equal(UserErrorKind.Retryable, unlock.Kind);

    var prerequisite = BackendErrorTranslator.ToUserError(new BackendApiException(
        "Your Mysterium identity is not registered. Register it before connecting.",
        "diagnostic",
        "err_id_not_registered",
        HttpStatusCode.BadRequest));
    Equal(UserErrorKind.Blocking, prerequisite.Kind);

    const string secret = "sensitive exception detail";
    var unknown = BackendErrorTranslator.ToUserError(new InvalidOperationException(secret));
    Equal(UserErrorKind.Blocking, unknown.Kind);
    True(!unknown.Message.Contains(secret, StringComparison.Ordinal));
}

static T Required<T>(T? value) where T : class =>
    value ?? throw new InvalidOperationException("Expected a value.");

static void True(bool condition)
{
    if (!condition) throw new InvalidOperationException("Condition was false.");
}

static void Equal<T>(T expected, T actual) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}; received {actual}.");
    }
}
