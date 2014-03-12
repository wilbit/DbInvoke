using System;
using DbInvoke.Helpers;

namespace DbInvoke.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DbTypeAttribute : Attribute
    {
        private readonly string _name;

        public DbTypeAttribute(string name)
        {
            _name = name;
        }

        public string Package { get; set; }
        public string Schema { get; set; }

        internal string GetFullName()
        {
            var fullName = StringHelper.JoinNotEmptyStrings(".", Schema, Package, _name);
            return fullName;
        }
    }
}