using System;

namespace DbInvoke.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class DbPropertyAttribute : Attribute
    {
        private readonly string _name;

        public DbPropertyAttribute(string name)
        {
            _name = name;
        }

        internal string GetFullName()
        {
            var fullName = _name ?? string.Empty;
            return fullName;
        }
    }
}