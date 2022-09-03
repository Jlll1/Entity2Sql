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

    foreach (var tg in syntaxReceiver.ToGenerate)
    {
      var columns = tg.GenerateFor.GetMembers().OfType<IPropertySymbol>()
        .Select(c => c.Name);

      var sb = new StringBuilder();
      sb.AppendLine(
$@"namespace {tg.ParentClass.ContainingNamespace.ToDisplayString()}
{{
    partial class {tg.ParentClass.Name.ToString()}
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

      context.AddSource($"{tg.ParentClass.Name.ToString()}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }
  }

  public void Initialize(GeneratorInitializationContext context)
  {
    context.RegisterForSyntaxNotifications(() => new MainSyntaxReceiver());
  }
}

public class MainSyntaxReceiver : ISyntaxContextReceiver
{
  private readonly List<GenerateInfo> _toGenerate = new();

  public List<GenerateInfo> ToGenerate => _toGenerate;

  public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
  {
    if (context.Node is not ClassDeclarationSyntax classDeclarationSyntax) return;

    INamedTypeSymbol classDeclaration = context.SemanticModel
      .GetDeclaredSymbol(classDeclarationSyntax) as INamedTypeSymbol
      ?? throw new Exception();

    var generateCrudAttribute = classDeclaration.GetAttributes()
      .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Entity2Sql.Attributes.GenerateCrudAttribute");
    if (generateCrudAttribute is not null)
    {
      INamedTypeSymbol classToGenerateFor = generateCrudAttribute.ConstructorArguments[0].Value as INamedTypeSymbol
        ?? throw new Exception();
      string tableName = generateCrudAttribute.ConstructorArguments[1].Value as string
        ?? throw new Exception();

      _toGenerate.Add(new GenerateInfo(classDeclaration, classToGenerateFor, tableName));
    }
  }

  public record GenerateInfo(INamedTypeSymbol ParentClass, INamedTypeSymbol GenerateFor, string TableName);
}
