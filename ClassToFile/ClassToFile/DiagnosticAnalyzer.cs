using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ClassToFile
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ClassToFileAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "ClassToFile";

        private static LocalizableString ResourceFromName(string name)
        {
            return new LocalizableResourceString(name, Resources.ResourceManager, typeof(Resources));
        }

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        internal static readonly LocalizableString Title = ResourceFromName(nameof(Resources.AnalyzerTitle));
        internal static readonly LocalizableString MessageFormat = ResourceFromName(nameof(Resources.AnalyzerMessageFormat));
        internal static readonly LocalizableString Description = ResourceFromName(nameof(Resources.AnalyzerDescription));
        internal const string Category = "Naming";

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Info, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxTreeAction(async c => await ProcessTree(c));
        }

        private async Task ProcessTree(SyntaxTreeAnalysisContext context)
        {
            var root = await context.Tree.GetRootAsync(context.CancellationToken);
            var allClassDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();

            var fileName = Path.GetFileNameWithoutExtension(context.Tree.FilePath);

            foreach (var classDecl in allClassDecls)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (!StringComparer.OrdinalIgnoreCase.Equals(classDecl.Identifier.Text, fileName) && 
                    !string.IsNullOrWhiteSpace(classDecl.Identifier.Text))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            Rule,
                            classDecl.GetLocation(),
                            classDecl.Identifier.Text));
                }
            }
        }
    }
}
