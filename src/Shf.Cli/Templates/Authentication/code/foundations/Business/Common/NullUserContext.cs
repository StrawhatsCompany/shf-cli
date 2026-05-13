namespace Business.Common;

public sealed class NullUserContext : IUserContext
{
    public Guid? UserId => null;
}
