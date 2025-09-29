namespace TraceLens.Model;

public class ServiceModel
{
      public ServiceModel(TraceLensModel model)
    {
        Model = model;
        Components = new Dictionary<string, Component>();
        Groups = new Dictionary<string, Group>();
        Calls = new HashSet<Call>();

        BuildComponents(Model.Root);
        BuildCalls(Model.Root, new Call(Model.Root.GetDescription(model).Id, "", CallKind.Sync, "", null));
    }
    
    public TraceLensModel Model { get; }
    public Dictionary<string, Component> Components { get; }
    public Dictionary<string, Group> Groups { get; }
    public HashSet<Call> Calls { get; }
    
    private void BuildComponents(Span span)
    {

        if (span.GetDescription(Model).IsClient && Model.Flatten)
        {
            //pass
        }
        else 
        {
            if (!Components.TryGetValue(span.GetDescription(Model).Id, out _))
            {
                var f = span.GetDescription(Model);
                if (f.Group.Name != "")
                {
                    Groups.TryAdd(f.Group.Id, f.Group);
                }
                else
                {
                    Components.Add(span.GetDescription(Model).Id, f.Component);    
                }
            }
        }

        foreach (var child in span.Children)
        {
            BuildComponents(child);
        }
    }
    
    private void BuildCalls(Span? parent, Call? parentCall)
    {
        if (parent == null) return;

        foreach (var child in parent.Children)
        {
            if (child.GetDescription(Model).IsClient && Model.Flatten)
            {
                BuildCalls(child, parentCall);
            }
            else
            {
                var ed = child.GetDescription(Model);
                var childId =  Groups.ContainsKey(child.GetDescription(Model).Group.Id) ? child.GetDescription(Model).Group.Id : child.GetDescription(Model).Id;
                var parentId = parentCall!.ToId;
                
                var c = new Call(childId, ed.Operation, ed.CallKind, parentId, parentCall);
            
                if (Calls.TryGetValue(c, out var existingCall))
                {
                    existingCall.CallCount++;
                }
                else
                {
                    c.CallCount = 1;
                    Calls.Add(c);
                }

                BuildCalls(child, c);
            }
        }
    }
}