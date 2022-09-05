namespace Entity2Sql.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class EntitySqlAttribute : Attribute
{
  public EntitySqlAttribute(Type entity, string tableName)
  {
  }
}

