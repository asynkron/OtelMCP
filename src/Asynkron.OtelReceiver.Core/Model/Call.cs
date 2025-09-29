using System.Text;

namespace TraceLens.Model;

public record Call(string ToId, string Operation, CallKind Kind, string FromId, Call? ParentCall)
{
    public int CallCount;

    public bool BelongsToComponent(string targetId)
    {
        return ToId == targetId;
    }

    public virtual bool Equals(Call? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return ToId == other.ToId && Operation == other.Operation && Kind == other.Kind && FromId == other.FromId && Equals(ParentCall, other.ParentCall);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ToId, Operation, (int)Kind, FromId, ParentCall);
    }
}