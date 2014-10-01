Imports Microsoft.CodeAnalysis.Rename

<ExportCodeRefactoringProvider(TupleCreateRefactoringProvider.RefactoringId, LanguageNames.VisualBasic)>
Friend Class TupleCreateRefactoringProvider
    Implements ICodeRefactoringProvider

    Public Const RefactoringId As String = "RefactoringTools.VB.TupleRefactoringProvider"

    Public Async Function GetRefactoringsAsync(document As Document, span As TextSpan, cancellationToken As CancellationToken) _
        As Task(Of IEnumerable(Of CodeAction)) Implements ICodeRefactoringProvider.GetRefactoringsAsync

        Dim root = Await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(False)

        Dim node = root.FindNode(span)

        Dim objectCreationSyntax As ObjectCreationExpressionSyntax

        If (node.IsKind(SyntaxKind.ObjectCreationExpression)) Then
            objectCreationSyntax = CType(node, ObjectCreationExpressionSyntax)
        Else
            objectCreationSyntax =
                    node.TryFindParentWithinStatement(Of ObjectCreationExpressionSyntax)(SyntaxKind.ObjectCreationExpression)

            If (objectCreationSyntax Is Nothing) Then
                Return Nothing
            End If
        End If


        Dim semanticModel = Await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(False)

        Dim typeSymbol = TryCast(semanticModel.GetSymbolInfo(objectCreationSyntax.Type).Symbol, INamedTypeSymbol)

        If (typeSymbol Is Nothing) Then
            Return Nothing
        End If

        If (Not typeSymbol.IsGenericType) Then
            Return Nothing
        End If

        If (Not typeSymbol.ToDisplayString().StartsWith("System.Tuple")) Then
            Return Nothing

            Dim argumentsExpressions =
                objectCreationSyntax.ArgumentList.Arguments _
                .Cast(Of SimpleArgumentSyntax) _
                .Select(Function(argument) argument.Expression) _
                .ToArray()

            If (argumentsExpressions.Any(Function(e) e.IsKind(SyntaxKind.NothingLiteralExpression))) Then
                Return Nothing
            End If


            For Each argument In argumentsExpressions

                Dim typeInfo = semanticModel.GetTypeInfo(argument)

                If (typeInfo.Type IsNot typeInfo.ConvertedType) Then
                    Return Nothing
                End If
            Next
        End If

        Return Nothing

    End Function
End Class
