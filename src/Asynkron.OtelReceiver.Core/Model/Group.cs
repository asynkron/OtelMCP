namespace TraceLens.Model;

public record Group(string Id, string Name)
{
    public virtual bool Equals(Group? other)
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