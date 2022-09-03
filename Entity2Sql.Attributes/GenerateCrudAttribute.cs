namespace Entity2Sql.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class GenerateCrudAttribute : Attribute
{
  public GenerateCrudAttribute(Type generateFor, string tableName)
  {
  }
}
