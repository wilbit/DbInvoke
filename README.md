DbInvoke
========

Invoke functions and procedures from the database using a simple interface description. Available for Oracle database.

Getting started
===============

If you have oracle stored package like this:
```sql
create or replace package schema_name.package_name is

  type room_typ is record (
    id   integer,
    name varchar2(100),
  );

  function get_room_info(p_room_id in integer) return room_typ;

end;
/
```

You can create class like this:
```csharp
[DbType("room_typ", Package = "package_name", Schema = "schema_name")]
public class RoomTyp
{
  [DbProperty("id")]
  public long Id { get; set; }

  [DbProperty("name")]
  public string Name { get; set; }
}
```

and interface like this:
```csharp
public interface IPackage
{
  [DbMethod("get_room_info", Package = "package_name", Schema = "schema_name")]
  RoomTyp GetRoom(long id);
}
```

and invoke function get_room_info like this:
```csharp
class Program
{
    static void Main(string[] args)
    {
        IDbConnection connection = ...;
        long roomId = ...;
        var package = DbInvoke.DbInvokeFactory.Create<IPackage>(connection);
        var room = package.GetRoom(roomId);
    }
}
```
