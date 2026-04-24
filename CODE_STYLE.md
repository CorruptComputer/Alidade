# Code Style Guidelines

## C#

### Declarations

#### 'var' Usage
When possible, use explicit types instead of `var`.

No:
```csharp
var a = 1;
var n = new Name("Beepsky");
var queue = new Queue<(ulong GuildId, string Track)>();
var tracks = audioQueue.Where(t => t.IsReady).ToList();
```

Yes:
```csharp
int a = 1;
Name n = new("Beepsky");
Queue<(ulong GuildId, string Track)> queue = new();
List<bool> tracks = audioQueue.Where(t => t.IsReady).ToList();
```

There are cases where `var` is required, such as with anonymous types:

Yes:
```csharp
var foo = new
{
    bar
};
```

#### Nullability
Nullability must be explicitly annotated using `?` where a value may be null.

Avoid suppressing nullability warnings with `!` unless justified with a comment.

No:
```csharp
// Where GetValue may return null
SomeObject result = GetValue();
Console.WriteLine(result.Id);

// Where SomeObject.Something is nullable
Something result = someList.First().Something!;
```

Yes:
```csharp
// Where GetValue may return null
SomeObject? result = GetValue();
if (result is not null)
{
    Console.WriteLine(value.Id);
}

// Where SomeObject.Something is nullable
IEnumerable<SomeObject> someList = someOtherList.Where(so => so.Something is not null);
Something result = someList.First().Something!; // Will not be null, see above check
```

#### Primary Constructors
Prefer primary constructors when the constructor body would only assign parameters to fields.

No:
```csharp
public class PresetService
{
    private readonly TaggingData _taggingData;
    private readonly ILogger<PresetService> _logger;

    public PresetService(TaggingData taggingData, ILogger<PresetService> logger)
    {
        _taggingData = taggingData;
        _logger = logger;
    }

    public void SomeMethod()
    {
        _taggingData.DoSomething();
        _logger.LogSomething();
    }
}
```

Yes:
```csharp
public class PresetService(TaggingData taggingData, ILogger<PresetService> logger)
{
    public void SomeMethod()
    {
        taggingData.DoSomething();
        logger.LogSomething();
    }
}
```

Use a traditional constructor only when the constructor body performs logic beyond simple assignment.

---

### Formatting

#### Bracing Style
Braces should be used in nearly all cases, including single-line blocks.

The only exception is simple lambda expressions where braces are unnecessary.

No:
```csharp
if (condition) DoSomething();
for (int i = 0; i < 10; i++) DoSomething();

list.Select(item =>
{
    return item.Id;
});
```

Yes:
```csharp
if (condition)
{
    DoSomething();
}

for (int i = 0; i < 10; i++)
{
    DoSomething();
}

list.Select(item => item.Id);

list.Select(item =>
{
    if (item.IsActive)
    {
        return item.Id;
    }
    else
    {
        return null;
    }
});
```

#### Comparisons
When comparing to `null`, always use `is` or `is not`. When comparing values, always use `==` or `!=`.

No:
```csharp
if (obj == null) { }
if (obj != null) { }
if (i is 0) { }
```

Yes:
```csharp
if (obj is null) { }
if (obj is not null) { }
if (i == 0) { }
```

#### Multi-line Statements

**Boolean conditions**: Place boolean operators at the beginning of the continuation line.

No:
```csharp
if (something1 &&
    something2)
{
}
```

Yes:
```csharp
if (something1
    && something2)
{
}
```

**Expression-bodied members**: Prefer them when possible; place `=>` on a new line for methods.

No:
```csharp
public static ActionResult PerformAction(Action action)
{
    return action.Perform();
}

public static ActionResult PerformAction(Action action) =>
    action.Perform();
```

Yes:
```csharp
public static ActionResult PerformAction(Action action)
    => action.Perform();
```

**Field and property initializers**: Place `=` on a new line when the initializer spans multiple lines.

No:
```csharp
public static readonly IReadOnlySet<string> Names =
    new HashSet<string> { "Alice", "Bob" };
```

Yes:
```csharp
public static readonly IReadOnlySet<string> Names
    = new HashSet<string> { "Alice", "Bob" };
```

#### Strings
When concatenating strings, prefer string interpolation instead of the `+` operator.

When building strings in loops or constructing large strings, prefer `StringBuilder`.

No:
```csharp
string result = "Hello " + name + "!";
```

Yes:
```csharp
string result = $"Hello {name}!";
```

---

### Naming

#### Class to File Ratio
Classes should generally be in their own file, and the file name should match the class name.

Exception: generic and non-generic versions may share a file.

Yes:
```csharp
// File: Something.cs
public class Something { }

public class Something<T> { }

// File: SomethingElse.cs
public class SomethingElse : Something<Foo> { }
```

#### Enums
Enums should be plural.

No:
```csharp
public enum Feature { }
```

Yes:
```csharp
public enum Features { }
```

---

### Types & Members

#### Member Ordering
Members should be in the following order:
- Consts
- Public Fields
- Protected Fields
- Internal Fields
- Private Fields
- Constructors
- Public Methods (interface/base implementations, grouped in `#region`)
- Public Methods (self)
- Protected Methods (interface/base implementations, grouped in `#region`)
- Protected Methods (self)
- Internal Methods
- Private Methods

#### Member Mutability
Use `init` or `readonly` when a member should not change after initialization.

No:
```csharp
public long Id { get; set; }
```

Yes:
```csharp
public long Id { get; init; }
```

#### Required Members
Use `required` when a member must be initialized.

Yes:
```csharp
public required string Name { get; init; }
```

---

### Documentation
Doc comments are required for all public members, including but not limited to methods, properties, fields, and classes. This will fail the build if not followed.

#### Summary
The `<summary>` must not be a single line. The content inside `<summary>` must be indented by two spaces.

No:
```csharp
/// <summary>Performs some kind of action.</summary>
public static void PerformAction();
```

Yes:
```csharp
/// <summary>
///   Performs some kind of action.
/// </summary>
public static void PerformAction();
```

#### Parameters
All parameters must be documented.

Yes:
```csharp
/// <summary>
///   Performs some kind of action.
/// </summary>
/// <param name="action">The action being performed.</param>
/// <param name="skipSomeCheck">Default false. Flag to skip a validation step.</param>
public static void PerformAction(Action action, bool skipSomeCheck = false);
```

#### Return Values
Methods returning values must include a `<returns>` section.

Yes:
```csharp
/// <summary>
///   Performs some kind of action.
/// </summary>
/// <param name="action">The action being performed.</param>
/// <param name="skipSomeCheck">Default false. Flag to skip some check.</param>
/// <returns>The result of the action. If some check is skipped, this will always be successful.</returns>
public static ActionResult PerformAction(Action action, bool skipSomeCheck = false);
```

#### Exceptions
Document all exceptions that may propagate to the caller.

Do not document:
- Exceptions that are caught and handled internally
- `NullReferenceException` or `ArgumentNullException` caused by improper usage

Yes:
```csharp
/// <summary>
///   Performs some kind of action.
/// </summary>
/// <param name="action">The action being performed.</param>
/// <param name="skipSomeCheck">Default false. Flag to skip a validation step.</param>
/// <returns>The result of the action. If some check is skipped, this will always be successful.</returns>
/// <exception cref="NotImplementedException">The type of action attempted is not supported.</exception>
public static ActionResult PerformAction(Action action, bool skipSomeCheck = false)
{
    ArgumentNullException.ThrowIfNull(action);

    return action.Type switch
    {
        _ => throw new NotImplementedException("This type of action is not supported")
    };
}
```

---

### Error Handling
Returning failure states is preferred over throwing exceptions when appropriate.

#### Rethrowing
Preserve stack traces when rethrowing.

No:
```csharp
catch (ArithmeticException e)
{
    throw e;
}
```

Yes:
```csharp
catch (ArithmeticException)
{
    throw;
}
```

#### Built-in Helper Methods
Prefer built-in exception helpers when available.

Yes:
```csharp
ArgumentNullException.ThrowIfNull(value);
ArgumentException.ThrowIfNullOrEmpty(name);
ArgumentOutOfRangeException.ThrowIfNegative(count);
```

---

## Handler Pattern (Questy)
Handlers are the only place business logic should live. Components and services dispatch requests; handlers execute them.

### Structure Rules
- The outer holder class must be `sealed` as handlers are never subclassed, and `sealed` enables JIT/AOT devirtualization.
- The holder class declaration and its `Handle()` method use `/// <inheritdoc/>`. Do not write a standalone doc comment on either.
- Only the inner request record (Command, Query, or Notification) carries a full XML doc comment.
- Use a primary constructor on the holder class to capture injected services.
- Commands should be used when the caller needs to know if it succeeded, Queries should be used when some data needs to be returned to the caller, and Notifications should be used when its just a fire-and-forget call (if something like `_ = Task.Run([...])` is done any exceptions that happen during this will never be logged, always use Notifications for these).
- The request model should be above the `Handle()` method.

### Command Example
A Command mutates state and returns `CommandResult`.

```csharp
/// <inheritdoc/>
public sealed class UpdateTags(EditBufferService editBuffer)
    : IRequestHandler<UpdateTags.Command, CommandResult>
{
    /// <summary>
    ///   Replaces the tag set on an OSM element in the edit buffer.
    /// </summary>
    /// <param name="ElementId">The ID of the element to update.</param>
    /// <param name="Tags">The complete replacement tag set.</param>
    public record Command(long ElementId, IReadOnlyDictionary<string, string> Tags)
        : IRequest<CommandResult>;

    /// <inheritdoc/>
    public Task<CommandResult> Handle(UpdateTags.Command request, CancellationToken cancellationToken)
        => editBuffer.UpdateTagsAsync(request.ElementId, request.Tags);
}
```

### Query Example
A Query reads state and returns `QueryResult<TData>`.

```csharp
/// <inheritdoc/>
public sealed class GetSelectedElement(EditBufferStateService state)
    : IRequestHandler<GetSelectedElement.Query, QueryResult<OsmElement?>>
{
    /// <summary>
    ///   Returns the currently selected OSM element, or <see langword="null"/> if nothing is selected.
    /// </summary>
    public record Query : IRequest<QueryResult<OsmElement?>>;

    /// <inheritdoc/>
    public Task<QueryResult<OsmElement?>> Handle(GetSelectedElement.Query request, CancellationToken cancellationToken)
        => Task.FromResult(QueryResult<OsmElement?>.Success(state.SelectedElement));
}
```

### Notification Example
A Notification is a fire-and-forget event; no return value.

```csharp
/// <inheritdoc/>
public sealed class ValidationStarted : INotificationHandler<ValidationStarted.Notification>
{
    /// <summary>
    ///   Published when the background validation pass begins.
    /// </summary>
    public record Notification : NotificationBase;

    /// <inheritdoc/>
    public Task Handle(ValidationStarted.Notification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
```

---

## JavaScript

### Bracing Style
Braces are required on all control-flow blocks, even single-line bodies.

No:
```js
if (condition) doSomething();
for (let i = 0; i < 10; i++) doSomething();
```

Yes:
```js
if (condition) {
    doSomething();
}

for (let i = 0; i < 10; i++) {
    doSomething();
}
```

### Variable Declarations
Prefer `const` for every binding that is never reassigned. Use `let` when reassignment is necessary. Do not use `var`.

No:
```js
var name = 'alidade';
let MAX_RETRIES = 3;
```

Yes:
```js
const name = 'alidade';
const MAX_RETRIES = 3;

let attempt = 0;
while (attempt < MAX_RETRIES) {
    attempt++;
}
```

### Console Logging
`console.log` and `console.debug` are acceptable while debugging an issue but must be removed before committing. General application logging belongs on the C# side, not in JS interop shims.
