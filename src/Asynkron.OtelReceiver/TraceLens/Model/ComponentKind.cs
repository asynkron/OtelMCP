namespace TraceLens.Model;

public enum ComponentKind
{
    Start,
    Service,
    Endpoint,
    Database,
    DatabaseStatement,
    Queue,
    QueueConsumer,
    Actor,
    Workflow,
    Activity
}

public static class ComponentKindExtensions
{
    public static string Name(this ComponentKind self)
    {
        return self.ToString().ToLowerInvariant();
    }
}