using System.Data;

namespace DbInvoke
{
    internal sealed class DbTypeAndSize
    {
        public DbType DbType { get; set; }

        public int Size
        {
            get { return _size; }
            set
            {
                _size = value;
                SizeSpecified = true;
            }
        }

        public bool SizeSpecified { get; private set; }

        private int _size;
    }
}