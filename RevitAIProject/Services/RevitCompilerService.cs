using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using RevitAIProject.Logic; // Убедитесь, что здесь лежит IRevitContext

public class RevitCompilerService
{
    // Добавляем параметр targetAiName в метод Compile
    public Action<IRevitContext> Compile(string code, string targetAiName, string assingAiName)
    {
        // 1. Очистка и авто-замена $переменных на 'ids' + обертка в проверку null
        string fixedCode = RepairAiCodeSyntax(code, targetAiName);

        string targetValue = string.IsNullOrEmpty(targetAiName) ? "null" : $"\"{targetAiName}\"";
        string assingValue = string.IsNullOrEmpty(assingAiName) ? "null" : $"\"{assingAiName}\"";

        var providerOptions = new Dictionary<string, string> { { "CompilerVersion", "v4.0" } };
        using (var provider = new Microsoft.CSharp.CSharpCodeProvider(providerOptions))
        {
            var parameters = new System.CodeDom.Compiler.CompilerParameters { GenerateInMemory = true };

            // Ссылки
            AddReference(parameters, typeof(object).Assembly);
            AddReference(parameters, typeof(System.Linq.Enumerable).Assembly);
            AddReference(parameters, typeof(Autodesk.Revit.DB.Element).Assembly);
            AddReference(parameters, typeof(Autodesk.Revit.UI.UIDocument).Assembly);
            AddReference(parameters, typeof(RevitAIProject.Logic.IRevitContext).Assembly);

            string wrapper = $@"
using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitAIProject.Logic;

public class DynamicRunner {{
    public void Run(IRevitContext context) {{
        Document doc = context.UIDoc.Document;
        UIDocument uidoc = context.UIDoc;
        string TargetAiName = {targetValue};
        string AssignAiName = {assingValue};
        List<ElementId> ids = new List<ElementId>(); 

        if (!context.Storage.StorageValue(TargetAiName, out ids)) {{
            ids = uidoc.Selection.GetElementIds().ToList() ?? new List<ElementId>();
        }}                
        
        #line 1 ""AI_GENERATED_CODE"" 
        {fixedCode}
    }}
}}";

            var results = provider.CompileAssemblyFromSource(parameters, wrapper);

            if (results.Errors.HasErrors)
            {
                var errors = results.Errors.Cast<System.CodeDom.Compiler.CompilerError>()
                    .Select(e => $"[Line {e.Line}]: {e.ErrorText}");
                throw new Exception("Compilation Failed:\n" + string.Join("\n", errors));
            }

            var type = results.CompiledAssembly.GetType("DynamicRunner");
            var instance = Activator.CreateInstance(type);
            var method = type.GetMethod("Run");

            return (ctx) =>
            {
                try { method.Invoke(instance, new object[] { ctx }); }
                catch (System.Reflection.TargetInvocationException ex) { throw ex.InnerException ?? ex; }
            };
        }
    }

    private string RepairAiCodeSyntax(string code, string targetAiName)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;

        // Очистка от мусора
        code = code.Replace("\\n", "\n").Replace("\\\"", "\"").Trim();
        code = System.Text.RegularExpressions.Regex.Replace(code, @"^```csharp\s*|```$", "", System.Text.RegularExpressions.RegexOptions.Multiline);

        // Подстраховка: замена $a1 на ids, чтобы компилятор не падал
        if (!string.IsNullOrEmpty(targetAiName) && targetAiName.StartsWith("$"))
        {
            code = code.Replace(targetAiName, "ids");
        }

        // Автоматическая проверка на наличие элементов (ids)
        if (code.Contains("ids") && !code.Contains("ids != null"))
        {
            code = $@"if (ids != null && ids.Count > 0) {{ {code} }}";
        }

        return code;
    }

    private void AddReference(CompilerParameters parameters, Assembly assembly)
    {
        if (assembly != null && !parameters.ReferencedAssemblies.Contains(assembly.Location))
            parameters.ReferencedAssemblies.Add(assembly.Location);
    }
}