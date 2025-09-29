using Grpc.Core;

namespace TraceLens.Model;

public class ComponentModel
{
    public ComponentModel(TraceLensModel model, IList<ComponentMetadata> metadata)
    {
        metadata ??= new List<ComponentMetadata>();
        Model = model;
        Components = new Dictionary<string, Component>();
        Groups = new Dictionary<string, Group>();
        Calls = new HashSet<Call>();
        MetaData = metadata.ToDictionary(m => m.NamePath, m => m);

        foreach (var span in Model.All)
        {
            BuildComponents(span);
        }

        BuildCalls(Model.Root, new Call(Model.Root.GetDescription(Model).Id, "", CallKind.Sync, "", null));
    }

    public IDictionary<string,ComponentMetadata> MetaData { get; }

    public TraceLensModel Model { get; }
    public Dictionary<string, Component> Components { get; }
    public Dictionary<string, Group> Groups { get; }
    public HashSet<Call> Calls { get; }


    public Group? GetGroupForId(string id)
    {
        if (Groups.TryGetValue(id, out var group))
        {
            return group;
        }

        return null;
    }

    public string GetMetadataKeyForId(string id)
    {
        if (Components.TryGetValue(id, out var component))
        {
            var g = GetGroupForId(component.GroupId);
            var key = $"{g?.Name}:{component.Name}";

            return key;
        }
        
        if (Groups.TryGetValue(id, out var group))
        {
            var key = $"{group.Name}:";
            return key;
        }

        return "";
    }
    public ComponentMetadata? GetMetadataForId(string id)
    {
        var key = GetMetadataKeyForId(id);
        return MetaData.TryGetValue(key, out var metadata) ? metadata : null;
    }
    
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

                Components.Add(span.GetDescription(Model).Id, f.Component);
            }
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
                var c = new Call(child.GetDescription(Model).Id, ed.Operation, ed.CallKind, parentCall!.ToId, parentCall);
            
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