using System;
namespace InventoryManagementSystem
{
    internal abstract class User
    {
        public string Username {get;}
        public Privilege PrivilegeLevel {get; set;}
        public string FirstName {get;}
        public string LastName {get;}

        protected User(string firstName, string lastName)
        {
            FirstName = firstName;
            LastName = lastName;
            Username = FirstName[0] + LastName;
        }
    }

    internal class Administrator : User
    {
        public Administrator(string firstName, string lastName) : base (firstName, lastName)
        {   
            PrivilegeLevel = Privilege.Administrator;
        }
    }

    internal class Staff : User
    {
        public Staff(string firstName, string lastName) : base(firstName, lastName)
        {
            PrivilegeLevel = Privilege.Staff;
        }
    }

    enum Privilege
    {
        Administrator,
        Staff,
        Guest
    }
}