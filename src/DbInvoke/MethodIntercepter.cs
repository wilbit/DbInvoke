using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using Castle.Core.Internal;
using Castle.DynamicProxy;
using DbInvoke.DataAnnotations;
using DbInvoke.Extensions;

namespace DbInvoke
{
    internal sealed class MethodIntercepter : IInterceptor
    {
        private readonly IDbConnection _connection;

        public MethodIntercepter(IDbConnection connection)
        {
            _connection = connection;
        }

        public void Intercept(IInvocation invocation)
        {
            var declareStrings = new List<string>();
            var initParamsStrings = new List<string>();
            var returningStrings = new List<string>();

            var initParameters = new List<IDbDataParameter>();
            var procedureDataParameters = new List<IDbDataParameter>();
            var returningDataParameters = new List<IDbDataParameter>();

            using (var command = _connection.CreateCommand())
            {
                if (invocation.Method.ReturnType != typeof (void))
                {
                    if (IsSimpleType(invocation.Method.ReturnType))
                    {
                        var returnParameter = command.CreateParameter();
                        returnParameter.ParameterName = "result";
                        returnParameter.Direction = ParameterDirection.Output;
                        var dbTypeAndSize = GetDbTypeAndSizeForType(invocation.Method.ReturnType);
                        returnParameter.DbType = dbTypeAndSize.DbType;
                        if (dbTypeAndSize.SizeSpecified)
                        {
                            returnParameter.Size = dbTypeAndSize.Size;
                        }
                        returningDataParameters.Add(returnParameter);
                    }
                    else if (invocation.Method.ReturnType.IsClass)
                    {
                        var oracleTypeName = invocation.Method.ReturnType.GetAttribute<DbObjectAttribute>().GetFullName();
                        declareStrings.Add(string.Format("  v_result {0};", oracleTypeName));

                        var returnTypeProperties = invocation.Method.ReturnType.GetProperties();
                        foreach (var propertyInfo in returnTypeProperties)
                        {
                            var dbProperty = propertyInfo.GetAttribute<DbPropertyAttribute>();

                            if (propertyInfo.PropertyType == typeof (bool) || propertyInfo.PropertyType == typeof (bool?))
                            {
                                returningStrings.Add(string.Format("  :{0} := sys.diutil.bool_to_int(v_result.{0});", dbProperty.GetFullName()));
                            }
                            else
                            {
                                returningStrings.Add(string.Format("  :{0} := v_result.{0};", dbProperty.GetFullName()));
                            }

                            var parameter = command.CreateParameter();
                            parameter.ParameterName = dbProperty.GetFullName();
                            parameter.Direction = ParameterDirection.Output;
                            var dbTypeAndSize = GetDbTypeAndSizeForProperty(propertyInfo);
                            parameter.DbType = dbTypeAndSize.DbType;
                            if (dbTypeAndSize.SizeSpecified)
                            {
                                parameter.Size = dbTypeAndSize.Size;
                            }
                            returningDataParameters.Add(parameter);
                        }
                    }
                    else
                    {
                        throw new NotImplementedException(string.Format("Not implemented for type {0}", invocation.Method.ReturnType));
                    }
                }

                var methodInfo = invocation.Method;
                var methodName = methodInfo.GetAttribute<DbMethodAttribute>().GetFullName();

                var parameterInfos = methodInfo.GetParameters();

                IList<string> procedureParams = new List<string>(parameterInfos.Length);

                foreach (var parameterInfo in parameterInfos)
                {
                    var parameterValue = invocation.Arguments[parameterInfo.Position];
                    if (IsSimpleType(parameterInfo.ParameterType))
                    {
                        var dbParameter = command.CreateParameter();
                        dbParameter.ParameterName = "par" + parameterInfo.Position;
                        dbParameter.Value = parameterValue;
                        var dbTypeAndSize = GetDbTypeAndSizeForParameter(parameterInfo);
                        dbParameter.DbType = dbTypeAndSize.DbType;
                        procedureParams.Add(":" + dbParameter.ParameterName);
                        procedureDataParameters.Add(dbParameter);
                    }
                    else if (parameterInfo.ParameterType.IsClass)
                    {
                        var oracleTypeName = parameterInfo.ParameterType.GetAttribute<DbObjectAttribute>().GetFullName();
                        declareStrings.Add(string.Format("  par{0} {1};", parameterInfo.Position, oracleTypeName));

                        var properties = parameterInfo.ParameterType.GetProperties();
                        foreach (var propertyInfo in properties)
                        {
                            var dbTypePropery = propertyInfo.GetAttribute<DbPropertyAttribute>();

                            initParamsStrings.Add(string.Format("  par{0}.{1} := :par{0}_{1};", parameterInfo.Position, dbTypePropery.GetFullName()));

                            var parameter = command.CreateParameter();
                            parameter.ParameterName = string.Format("par{0}_{1}", parameterInfo.Position, dbTypePropery.GetFullName());
                            if (parameterValue != null)
                            {
                                parameter.Value = propertyInfo.GetValue(parameterValue, null);
                            }

                            var dbTypeAndSize = GetDbTypeAndSizeForProperty(propertyInfo);
                            parameter.DbType = dbTypeAndSize.DbType;
                            if (dbTypeAndSize.SizeSpecified)
                            {
                                parameter.Size = dbTypeAndSize.Size;
                            }
                            initParameters.Add(parameter);
                        }
                        procedureParams.Add(string.Format("par{0}", parameterInfo.Position));
                    }
                    else
                    {
                        throw new NotImplementedException(string.Format("Not implemented for type {0}", parameterInfo.ParameterType));
                    }
                }


                var sqlBuilder = new StringBuilder();
                if (declareStrings.Count != 0)
                {
                    sqlBuilder.Append("declare\n");
                    sqlBuilder.Append(string.Join("\n", declareStrings) + "\n");
                }

                sqlBuilder.Append("begin\n");

                if (initParamsStrings.Count != 0)
                {
                    sqlBuilder.Append(string.Join("\n", initParamsStrings) + "\n");
                }

                var procedureCallSql = string.Format("{0}({1})", methodName, string.Join(", ", procedureParams));
                if (invocation.Method.ReturnType == typeof (void))
                {
                    sqlBuilder.Append("  " + procedureCallSql + ";\n");
                }
                else
                {
                    if (IsSimpleType(invocation.Method.ReturnType))
                    {
                        if (invocation.Method.ReturnType == typeof (bool) || invocation.Method.ReturnType == typeof (bool?))
                        {
                            sqlBuilder.Append("  :result := sys.diutil.bool_to_int(" + procedureCallSql + ");\n");
                        }
                        else
                        {
                            sqlBuilder.Append("  :result := " + procedureCallSql + ";\n");
                        }
                    }
                    else
                    {
                        sqlBuilder.Append("  v_result := " + procedureCallSql + ";\n");
                    }
                }

                if (returningStrings.Count != 0)
                {
                    sqlBuilder.Append(string.Join("\n", returningStrings) + "\n");
                }

                sqlBuilder.Append("end;");

                command.CommandText = sqlBuilder.ToString();

                command.Parameters.AddRange(initParameters);
                if (returningDataParameters.Count == 1 && returningDataParameters.Any(x => x.ParameterName == "result"))
                {
                    command.Parameters.AddRange(returningDataParameters);
                    command.Parameters.AddRange(procedureDataParameters);
                }
                else
                {
                    command.Parameters.AddRange(procedureDataParameters);
                    command.Parameters.AddRange(returningDataParameters);
                }

                command.ExecuteNonQuery();

                if (invocation.Method.ReturnType == typeof (void))
                {
                    return;
                }

                if (IsSimpleType(invocation.Method.ReturnType))
                {
                    var resultParameter = (IDbDataParameter) command.Parameters["result"];
                    invocation.ReturnValue = ConvertToType(resultParameter.Value, invocation.Method.ReturnType);
                }
                else if (invocation.Method.ReturnType.IsClass)
                {
                    var result = Activator.CreateInstance(invocation.Method.ReturnType);
                    foreach (var propertyInfo in result.GetType().GetProperties())
                    {
                        var name = propertyInfo.GetAttribute<DbPropertyAttribute>().GetFullName();
                        var parameter = command.Parameters[name] as DbParameter;
                        var value = parameter.Value;
                        if (!(value is DBNull))
                        {
                            propertyInfo.SetValue(result, value, null);
                        }
                        else if (propertyInfo.PropertyType == typeof (string))
                        {
                            propertyInfo.SetValue(result, string.Empty, null);
                        }
                    }
                    invocation.ReturnValue = result;
                }
                else
                {
                    throw new NotImplementedException(string.Format("Not implemented for type {0}", invocation.Method.ReturnType));
                }

                //var result = Convert.ToByte(returnParam.Value);
                //switch (result)
                //{
                //    case 1:
                //        invocation.ReturnValue = true;
                //        break;
                //    case 0:
                //        invocation.ReturnValue = false;
                //        break;
                //    default:
                //        throw new NotSupportedException();
                //}
                //invocation.ReturnValue = returnParam.Value is DBNull
                //    ? (returnType == typeof(long) ? default(long) : (long?)null)
                //    : Convert.ToInt64(returnParam.Value);
            }
        }

        private static bool IsSimpleType(Type type)
        {
            if (type == null) throw new ArgumentNullException("type");

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                type = type.GetGenericArguments()[0];
            }

            var simpleTypes = new[] { typeof(string), typeof(bool), typeof(byte), typeof(int), typeof(long), typeof(DateTime) };

            return simpleTypes.Contains(type);
        }

        private static DbTypeAndSize GetDbTypeAndSizeForParameter(ParameterInfo parameterInfo)
        {
            return GetDbTypeAndSizeForType(parameterInfo.ParameterType);
        }

        private static DbTypeAndSize GetDbTypeAndSizeForProperty(PropertyInfo propertyInfo)
        {
            return GetDbTypeAndSizeForType(propertyInfo.PropertyType);
        }

        private static DbTypeAndSize GetDbTypeAndSizeForType(Type type)
        {
            const int maxOracleVarchar2Length = 32767;
            const int maxOracleNumberLength = 38;

            if (type == typeof(string))
            {
                return new DbTypeAndSize
                {
                    DbType = DbType.AnsiString,
                    Size = maxOracleVarchar2Length
                };
            }
            if (type == typeof(bool) || type == typeof(bool?) ||  type == typeof(byte) || type == typeof(byte?))
            {
                return new DbTypeAndSize {DbType = DbType.Byte};
            }
            if (type == typeof(int) || type == typeof(int?))
            {
                return new DbTypeAndSize { DbType = DbType.Int32 };
            }
            if (type == typeof(long) || type == typeof(long?))
            {
                return new DbTypeAndSize
                {
                    DbType = DbType.Int64,
                    Size = maxOracleNumberLength
                };
            }
            if (type == typeof(DateTime) || type == typeof(DateTime?))
            {
                return new DbTypeAndSize { DbType = DbType.Date };
            }

            throw new NotImplementedException(string.Format("Not implemented for type {0}", type));
        }

        private static object ConvertToType(object @object, Type type)
        {
            if (@object is DBNull)
            {
                if (type == typeof(Nullable<>))
                {
                    return null;
                }
                if (type == typeof (string))
                {
                    return string.Empty;
                }
                return Activator.CreateInstance(type);
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof (Nullable<>))
            {
                type = type.GetGenericArguments()[0];
            }

            if (type == typeof(string))
            {
                return @object.ToString();
            }
            if (type == typeof (bool))
            {
                var b = (byte)@object;
                if (b == 1)
                {
                    return true;
                }
                if (b == 0)
                {
                    return false;
                }
                throw new NotSupportedException(string.Format("Not supported value {0} for bool type", b));
            }
            if (type == typeof (byte))
            {
                return (byte) @object;
            }
            if (type == typeof (int))
            {
                return (int) @object;
            }
            if (type == typeof(long))
            {
                return (long)@object;
            }

            throw new NotImplementedException(string.Format("Not implemented for type {0}", type));
        }
    }
}