# Code Style

## Event handler subscriptions

Use named private methods as event handlers, not lambdas stored in fields.

**Why:** named methods make constructor and `Dispose` symmetrical, keep the constructor focused on wiring, and give each handler a readable name at the call site.

```csharp
// ✅ preferred
public MyClass(IFooService foo)
{
    _foo = foo;
    foo.SomethingHappened += OnSomethingHappened;
}

private void OnSomethingHappened(SomeMessage msg) { ... }

public void Dispose() => _foo.SomethingHappened -= OnSomethingHappened;

// ❌ avoid
private readonly Action<SomeMessage> _onSomethingHappened;

public MyClass(IFooService foo)
{
    _onSomethingHappened = msg => { ... };
    foo.SomethingHappened += _onSomethingHappened;
}

public void Dispose() => _foo.SomethingHappened -= _onSomethingHappened;
```

If the handler needs access to constructor parameters, store those as fields rather than capturing them in a lambda.
