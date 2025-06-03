public abstract class User
{
    public string UserId { get; protected set; } // Can be set by derived class constructors
    public string Name { get; set; }

    protected User(string userId, string name)
    {
        UserId = userId;
        Name = name;
    }

    public override string ToString()
    {
        return $"ID: {UserId}, Name: {Name}";
    }
}