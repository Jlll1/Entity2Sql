using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Entity2Sql.Generator;

[Generator]
public class Generator : ISourceGenerator
{
  public void Execute(GeneratorExecutionContext context)
  {
    if(context.SyntaxContextReceiver is not MainSyntaxReceiver syntaxReceiver) return;

    foreach (var tg in syntaxReceiver.EntitySqls)
    {
      var columns = tg.EntityClass.GetMembers()
        .OfType<IPropertySymbol>()
        .Select(c => c.Name);

      var sb = new StringBuilder();
      sb.AppendLine(
$@"namespace {tg.EntitySqlClass.ContainingNamespace.ToDisplayString()}
{{
    partial class {tg.EntitySqlClass.Name.ToString()}
    {{
        public string SelectAll => @""
SELECT
    {String.Join(",\n    ", columns)}
FROM {tg.TableName}
"";

        public string SelectById => @""
SELECT
    {String.Join(",\n    ", columns)}
FROM {tg.TableName}
WHERE Id = @Id
"";

        public string Insert => @""
INSERT INTO {tg.TableName}
    ({String.Join(",\n    ", columns)})
VALUES
    ({String.Join(",\n    ", columns.Select(c => $"@{c}"))})
"";

        public string UpdateById => @""
UPDATE {tg.TableName}
SET {String.Join(",\n    ", columns.Where(c => c != "Id").Select(c => $"{c} = @{c}"))}
WHERE Id = @Id
"";

        public string DeleteById => @""
DELETE FROM {tg.TableName}
WHERE Id = @Id
"";
    }}
}}
");

      context.AddSource($"{tg.EntitySqlClass.Name.ToString()}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }
  }

  public void Initialize(GeneratorInitializationContext context)
  {
    context.RegisterForSyntaxNotifications(() => new MainSyntaxReceiver());
  }
}

public class MainSyntaxReceiver : ISyntaxContextReceiver
{
  private readonly List<EntitySql> _entitySqls = new();
  public IEnumerable<EntitySql> EntitySqls => _entitySqls;

  public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
  {
    if (context.Node is not ClassDeclarationSyntax classDeclarationSyntax)
      return;

    INamedTypeSymbol entitySqlClassDeclaration = context.SemanticModel
      .GetDeclaredSymbol(classDeclarationSyntax) as INamedTypeSymbol
      ?? throw new Exception();
    var classAttributes = entitySqlClassDeclaration.GetAttributes();

    {
      var entitySqlAttribute = classAttributes.FirstOrDefault(
        a => a.AttributeClass?.ToDisplayString() == "Entity2Sql.Attributes.EntitySqlAttribute");
      if (entitySqlAttribute is not null)
      {
        INamedTypeSymbol entity = entitySqlAttribute.ConstructorArguments[0].Value as INamedTypeSymbol
          ?? throw new Exception();
        string tableName = entitySqlAttribute.ConstructorArguments[1].Value as string
          ?? throw new Exception();

        _entitySqls.Add(new EntitySql(entitySqlClassDeclaration, entity, tableName));
      }
    }

  }

  public record EntitySql(INamedTypeSymbol EntitySqlClass, INamedTypeSymbol EntityClass, string TableName);
}
