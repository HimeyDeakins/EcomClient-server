
namespace ShopSystem
{
    public class Manager : User // Inherits from User
    {
        // UserId and Name are inherited from User

        public Manager(string managerId, string name) : base(managerId, name) // Call base constructor
        {

        }

        // ToString() is inherited. specific Manager info.
        public override string ToString()
        {
            return base.ToString() + " (Manager)"; // Example
        }
    }
}