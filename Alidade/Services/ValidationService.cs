using System.Threading.Channels;
using Alidade.Osm.Validators.Error;
using Alidade.Osm.Validators.Info;
using Alidade.Osm.Validators.Warning;

namespace Alidade.Services;

/// <summary>
///   Runs all registered <see cref="ValidatorBase"/> checks on a background thread via a
///   bounded <see cref="Channel{T}"/> with capacity one and a drop-oldest overflow policy.
///   Results are pushed back to the UI via <see cref="IMediator"/>. Validation is
///   debounced 300 ms after each call to <see cref="ScheduleValidation"/> to avoid
///   thrashing during rapid consecutive edits.
/// </summary>
public sealed class ValidationService : IAsyncDisposable
{
    private static readonly ValidatorBase[] _validators = [
        // Error
        new RelationSizeValidator(),
        new MissingRoleValidator(),
        // Warning
        new MissingTagsValidator(),
        new DisconnectedWayValidator(),
        new CrossingWaysValidator(),
        new DuplicateNodeValidator(),
        new ShortSegmentValidator(),
        new BuildingSquarenessValidator(),
        new MutuallyExclusiveTagsValidator(),
        new GeometryMismatchValidator(),
        new TagLengthValidator(),
        new DeprecatedTagValidator(),
        // Info
        new SuspiciousNameValidator(),
    ];

    private readonly IMediator _mediator;
    private readonly PresetService _presets;
    private readonly Channel<EditBufferSnapshot> _channel;
    private readonly CancellationTokenSource _cts;
    private readonly Task _workerTask;

    private Timer? _debounceTimer;

    /// <summary>
    ///   Initializes the service, creates the background channel, and starts the worker task.
    /// </summary>
    /// <param name="mediator">The mediator used to publish validation results.</param>
    /// <param name="presets">
    ///   The preset service passed through to each validator for tag-schema lookups.
    /// </param>
    public ValidationService(IMediator mediator, PresetService presets)
    {
        _mediator = mediator;
        _presets = presets;

        _channel = Channel.CreateBounded<EditBufferSnapshot>(
            new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleWriter = true,
                SingleReader = true
            });

        _cts = new CancellationTokenSource();
        _workerTask = Task.Run(WorkerAsync);
    }

    /// <summary>
    ///   Schedules a validation pass to run after a 300 ms debounce period. Any previously
    ///   pending pass is cancelled and replaced by this call.
    /// </summary>
    /// <param name="state">The current edit buffer state to validate.</param>
    public void ScheduleValidation(EditBufferState state)
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(_ =>
        {
            EditBufferSnapshot snapshot = new(
                state.Nodes, state.Ways, state.Relations, state.EditStates);
            _channel.Writer.TryWrite(snapshot);
        }, null, 300, Timeout.Infinite);
    }

    #region IAsyncDisposable
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _debounceTimer?.Dispose();
        _channel.Writer.TryComplete();
        await _cts.CancelAsync();

        try
        {
            await _workerTask;
        }
        catch (OperationCanceledException)
        {
            // ignore
        }

        _cts.Dispose();
    }
    #endregion

    private async Task WorkerAsync()
    {
        await foreach (EditBufferSnapshot snapshot in _channel.Reader.ReadAllAsync(_cts.Token))
        {
            await _mediator.Publish(new Handlers.Validation.ValidationStarted.Notification());
            List<ValidationIssue> issues = [];

            foreach (ValidatorBase validator in _validators)
            {
                try
                {
                    issues.AddRange(validator.Check(snapshot, _presets));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Validator {validator.GetType().Name} failed: {ex.Message}");
                }
            }

            await _mediator.Publish(new Handlers.Validation.ValidationResults.Notification(issues));
        }
    }
}
