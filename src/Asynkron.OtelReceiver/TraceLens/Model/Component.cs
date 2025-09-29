namespace TraceLens.Model;

/*
 * Id is a unique identifier for the component
 */
public record Component(string Id, string Name, string GroupId, ComponentKind Kind, string ComponentStack)
{
    public virtual bool Equals(Component? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}