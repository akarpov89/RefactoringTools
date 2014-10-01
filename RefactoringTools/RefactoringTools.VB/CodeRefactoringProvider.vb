Imports Microsoft.CodeAnalysis.Rename

<ExportCodeRefactoringProvider(CodeRefactoringProvider.RefactoringId, LanguageNames.VisualBasic)>
Friend Class CodeRefactoringProvider
    Implements ICodeRefactoringProvider

    Public Const RefactoringId As String = "RefactoringTools.VB"

    Public Async Function GetRefactoringsAsync(document As Document, textSpan As TextSpan, cancellationToken As CancellationToken) As Task(Of IEnumerable(Of CodeAction)) _
        Implements ICodeRefactoringProvider.GetRefactoringsAsync

        ' TODO: Replace the following code with your own analysis, generating a CodeAction for each refactoring to offer

        Dim root = Await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(False)

        ' Find the node at the selection.
        Dim node = root.FindNode(textSpan)

        ' Only offer a refactoring if the selected node is a type statement node.
        Dim typeDecl = TryCast(node, TypeStatementSyntax)
        If typeDecl Is Nothing Then
            Return Nothing
        End If

        ' For any type statement node, create a code action to reverse the identifier text.
        Dim action = CodeAction.Create("Reverse type name", Function(c) ReverseTypeNameAsync(document, typeDecl, c))

        ' Return this code action.
        Return {action}
    End Function

    Private Async Function ReverseTypeNameAsync(document As Document, typeStmt As TypeStatementSyntax, cancellationToken As CancellationToken) As Task(Of Solution)
        ' Produce a reversed version of the type statement's identifier token.
        Dim identifierToken = typeStmt.Identifier
        Dim newName = New String(identifierToken.Text.Reverse().ToArray())

        ' Get the symbol representing the type to be renamed.
        Dim semanticModel = Await document.GetSemanticModelAsync(cancellationToken)
        Dim typeSymbol = semanticModel.GetDeclaredSymbol(typeStmt, cancellationToken)

        ' Produce a new solution that has all references to that type renamed, including the declaration.
        Dim originalSolution = document.Project.Solution
        Dim optionSet = originalSolution.Workspace.Options
        Dim newSolution = Await Renamer.RenameSymbolAsync(document.Project.Solution, typeSymbol, newName, optionSet, cancellationToken).ConfigureAwait(False)

        ' Return the new solution with the now-uppercase type name.
        Return newSolution
    End Function
End Class
