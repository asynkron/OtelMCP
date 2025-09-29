namespace TraceLens.Model;

//Why are Component and SpanDescription not the same?
//Component lacks CallKind, and is unique for the fields it does have
//SpanDescription describes a span only.
//The Id of the SpanDescription is strange as it is unique for group and component name
//TODO: fix this... sometime..

// Span             = each span in OpenTelemetry
// SpanDescription  = a unique representation of each operation, e.g. "Sql Server XYZ - Select" vs. "Sql Server XYZ - Update"
// Component        = a unique representation of detected components, e.g. "Sql Server XYZ"
public record SpanDescription
{
    public SpanDescription(TraceLensModel model, string groupName, string componentName, string operation, ComponentKind componentKind,
        CallKind callKind, string response = "", string componentStack = "", bool isClient = false)
    {
        if (componentKind == ComponentKind.Service && groupName == "")
        {
            groupName = componentName;
            //TODO: what is a good name here
            componentName = "Internal";
        }
        
        Id = StringKey.Get($"{groupName}:{componentName}",model);

        Group = new Group(StringKey.Get(groupName,model), groupName);
        if (string.IsNullOrEmpty(componentName)) componentName = "Unknown";
        Component = new Component(Id, componentName, Group.Id, componentKind, componentStack);

        IsClient = isClient;
        Operation = operation;
        Response = response;
        CallKind = callKind;
    }

    public string Id { get; }
    public Group Group { get; }
    public string Operation { get; }
    public string Response { get; }
    public CallKind CallKind { get; }
    public Component Component { get; }
    public bool IsClient { get; }
}