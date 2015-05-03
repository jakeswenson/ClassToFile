using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClassToFile
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ClassToFileCodeFixProvider)), Shared]
    public class ClassToFileCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(ClassToFileAnalyzer.DiagnosticId); }
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var classesInDocument = root.DescendantNodesAndSelf().OfType<ClassDeclarationSyntax>().Count();

            if (classesInDocument == 1)
            {
                // rename document
                var diagnostic = context.Diagnostics.Single();
                AddRenameDocumentFix(context, root, diagnostic);
            }
            else
            {
                var allDiagnostics = context.Diagnostics;

                var allClassesDontMatch = allDiagnostics.Length == classesInDocument;

                int start = allClassesDontMatch ? 2 : 1;

                // The last will be a rename (but only if all classes don't match file name)
                for (int i = allDiagnostics.Length - start; i >= 0; i--)
                {
                    var diagnostic = allDiagnostics[i];
                    ClassDeclarationSyntax declaration = GetClassDeclarationFromDiagnostic(root, diagnostic);

                    var codeAction = CodeAction.Create(
                        $"Create new file named '{declaration.Identifier.Text}.cs'",
                        token => AddDocumentToProject(context, declaration, token));

                    context.RegisterCodeFix(codeAction, diagnostic);
                }

                if (allClassesDontMatch)
                {
                    AddRenameDocumentFix(context, root, allDiagnostics.Last());
                }
            }
        }

        private void AddRenameDocumentFix(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic)
        {
            ClassDeclarationSyntax declaration = GetClassDeclarationFromDiagnostic(root, diagnostic);

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Rename Document to {declaration.Identifier.Text}.cs",
                    token => RenameDocument(context, declaration, token)),
                diagnostic);
        }

        private static ClassDeclarationSyntax GetClassDeclarationFromDiagnostic(SyntaxNode root, Diagnostic diagnostic)
        {
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var declaration =
                root.FindToken(diagnosticSpan.Start).Parent as ClassDeclarationSyntax;
            return declaration;
        }

        private async Task<Solution> AddDocumentToProject(CodeFixContext context, ClassDeclarationSyntax declaration, CancellationToken token)
        {
            var document = context.Document;
            var root = await document.GetSyntaxRootAsync(token).ConfigureAwait(false);

            var newDocumentRoot = root.RemoveNode(declaration, SyntaxRemoveOptions.KeepNoTrivia);
            document = document.WithSyntaxRoot(newDocumentRoot);

            var project = document.Project;
            project = AddNewClassDocument(context, declaration, project);

            return project.Solution;
        }

        private static Project AddNewClassDocument(CodeFixContext context, ClassDeclarationSyntax declaration, Project project)
        {
            var containingNamespace = declaration.FirstAncestorOrSelf<NamespaceDeclarationSyntax>();
            var existingRoot = containingNamespace.FirstAncestorOrSelf<CompilationUnitSyntax>();

            var newNamespace = containingNamespace.WithMembers(
                SyntaxFactory.SingletonList<MemberDeclarationSyntax>(declaration));

            var newRoot = 
                existingRoot.Update(
                    existingRoot.Externs, 
                    existingRoot.Usings, 
                    existingRoot.AttributeLists, 
                    SyntaxFactory.SingletonList<MemberDeclarationSyntax>(newNamespace), 
                    existingRoot.EndOfFileToken);

            project = project.AddDocument(
                $"{declaration.Identifier.Text}.cs",
                newRoot,
                folders: context.Document.Folders).Project;
            return project;
        }

        private async Task<Solution> RenameDocument(CodeFixContext context, ClassDeclarationSyntax declaration, CancellationToken token)
        {
            var project = context.Document.Project;

            project = project.AddDocument(
                $"{declaration.Identifier.Text}.cs",
                await context.Document.GetSyntaxRootAsync(token).ConfigureAwait(false),
                folders: context.Document.Folders).Project;

            project = project.RemoveDocument(context.Document.Id);

            return project.Solution;
        }
    }
}