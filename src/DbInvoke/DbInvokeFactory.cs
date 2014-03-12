using System;
using System.Data;
using Castle.DynamicProxy;

namespace DbInvoke
{
    public static class DbInvokeFactory
    {
        public static TInterface Create<TInterface>(IDbConnection dbConnection)
            where TInterface : class
        {
            if (dbConnection == null) throw new ArgumentNullException("dbConnection");

            var generator = new ProxyGenerator();
            var proxy = generator.CreateInterfaceProxyWithoutTarget<TInterface>(new MethodIntercepter(dbConnection));
            return proxy;
        }
    }
}