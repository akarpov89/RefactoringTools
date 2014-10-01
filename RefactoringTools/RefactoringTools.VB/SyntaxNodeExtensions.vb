Imports System.Runtime.CompilerServices
Imports Microsoft.CodeAnalysis.Formatting

Module SyntaxNodeExtensions

    <Extension()>
    Private Function IsMethodOrClassOrNamespace(node As SyntaxNode) As Boolean

        Return node.IsKind(SyntaxKind.FunctionBlock) OrElse
               node.IsKind(SyntaxKind.SubBlock) OrElse
               node.IsKind(SyntaxKind.ClassBlock) OrElse
               node.IsKind(SyntaxKind.ModuleBlock) OrElse
               node.IsKind(SyntaxKind.CompilationUnit)
    End Function

    <Extension()>
    Public Function Format(Of T As SyntaxNode)(node As T) As T

        Return node.WithAdditionalAnnotations(Formatter.Annotation)

    End Function

    <Extension()>
    Public Function IsWithin(node As SyntaxNode, span As TextSpan) As Boolean

        Return node.SpanStart >= span.Start AndAlso node.SpanStart <= span.End

    End Function

    <Extension()>
    Public Function TryFindParentWithinStatement(Of TSyntaxNode As SyntaxNode)(
        node As SyntaxNode, kind As SyntaxKind) As TSyntaxNode

        Dim t = New Tuple(Of Integer, String)(42, New String("a"))

        Do
            node = node.Parent

            If (node.IsStatement() OrElse node.IsMethodOrClassOrNamespace()) Then
                Return Nothing
            End If


            If (node.IsKind(kind)) Then

                Dim temp = CType(node, TSyntaxNode)
                Return temp

            End If

        Loop While True

        Return Nothing

    End Function


    '<Extension()>
    'Public Function TryFindParentBlock(node As SyntaxNode) As BlockSyntax
    '    Do
    '        If (node.IsMethodOrClassOrNamespace) Then
    '            Return Nothing
    '        End If

    '        node = node.Parent

    '    Loop While (Not node.IsStatement)

    '    Return node
    'End Function

    <Extension()>
    Public Function TryFindParentStatement(node As SyntaxNode) As StatementSyntax
        Do
            If (node.IsMethodOrClassOrNamespace) Then
                Return Nothing
            End If

            node = node.Parent

        Loop While (Not node.IsStatement)

        Return node
    End Function

    <Extension()>
    Public Function IsStatement(node As SyntaxNode) As Boolean

        Select Case node.VisualBasicKind
            Case SyntaxKind.EmptyStatement,
                 SyntaxKind.EndIfStatement,
                 SyntaxKind.EndUsingStatement,
                 SyntaxKind.EndWithStatement,
                 SyntaxKind.EndSelectStatement,
                 SyntaxKind.EndStructureStatement,
                 SyntaxKind.EndEnumStatement,
                 SyntaxKind.EndInterfaceStatement,
                 SyntaxKind.EndClassStatement,
                 SyntaxKind.EndModuleStatement,
                 SyntaxKind.EndNamespaceStatement,
                 SyntaxKind.EndSubStatement,
                 SyntaxKind.EndFunctionStatement,
                 SyntaxKind.EndGetStatement,
                 SyntaxKind.EndSetStatement,
                 SyntaxKind.EndPropertyStatement,
                 SyntaxKind.EndOperatorStatement,
                 SyntaxKind.EndEventStatement,
                 SyntaxKind.EndAddHandlerStatement,
                 SyntaxKind.EndRemoveHandlerStatement,
                 SyntaxKind.EndRaiseEventStatement,
                 SyntaxKind.EndWhileStatement,
                 SyntaxKind.EndTryStatement,
                 SyntaxKind.EndSyncLockStatement,
                 SyntaxKind.OptionStatement,
                 SyntaxKind.ImportsStatement,
                 SyntaxKind.NamespaceStatement,
                 SyntaxKind.InheritsStatement,
                 SyntaxKind.ImplementsStatement,
                 SyntaxKind.ModuleStatement,
                 SyntaxKind.StructureStatement,
                 SyntaxKind.InterfaceStatement,
                 SyntaxKind.ClassStatement,
                 SyntaxKind.EnumStatement,
                 SyntaxKind.SubStatement,
                 SyntaxKind.FunctionStatement,
                 SyntaxKind.SubNewStatement,
                 SyntaxKind.DeclareSubStatement,
                 SyntaxKind.DeclareFunctionStatement,
                 SyntaxKind.DelegateSubStatement,
                 SyntaxKind.DelegateFunctionStatement,
                 SyntaxKind.EventStatement,
                 SyntaxKind.OperatorStatement,
                 SyntaxKind.PropertyStatement,
                 SyntaxKind.GetAccessorStatement,
                 SyntaxKind.SetAccessorStatement,
                 SyntaxKind.AddHandlerAccessorStatement,
                 SyntaxKind.RemoveHandlerAccessorStatement,
                 SyntaxKind.RaiseEventAccessorStatement,
                 SyntaxKind.AttributesStatement,
                 SyntaxKind.ExpressionStatement,
                 SyntaxKind.PrintStatement,
                 SyntaxKind.LocalDeclarationStatement,
                 SyntaxKind.LabelStatement,
                 SyntaxKind.GoToStatement,
                 SyntaxKind.StopStatement,
                 SyntaxKind.EndStatement,
                 SyntaxKind.ExitDoStatement,
                 SyntaxKind.ExitForStatement,
                 SyntaxKind.ExitSubStatement,
                 SyntaxKind.ExitFunctionStatement,
                 SyntaxKind.ExitOperatorStatement,
                 SyntaxKind.ExitPropertyStatement,
                 SyntaxKind.ExitTryStatement,
                 SyntaxKind.ExitSelectStatement,
                 SyntaxKind.ExitWhileStatement,
                 SyntaxKind.ContinueWhileStatement,
                 SyntaxKind.ContinueDoStatement,
                 SyntaxKind.ContinueForStatement,
                 SyntaxKind.ReturnStatement,
                 SyntaxKind.IfStatement,
                 SyntaxKind.ElseIfStatement,
                 SyntaxKind.ElseStatement,
                 SyntaxKind.TryStatement,
                 SyntaxKind.CatchStatement,
                 SyntaxKind.FinallyStatement,
                 SyntaxKind.OnErrorGoToZeroStatement,
                 SyntaxKind.OnErrorGoToMinusOneStatement,
                 SyntaxKind.OnErrorGoToLabelStatement,
                 SyntaxKind.OnErrorResumeNextStatement,
                 SyntaxKind.ResumeStatement,
                 SyntaxKind.ResumeLabelStatement,
                 SyntaxKind.ResumeNextStatement,
                 SyntaxKind.SelectStatement,
                 SyntaxKind.CaseStatement,
                 SyntaxKind.CaseElseStatement,
                 SyntaxKind.SyncLockStatement,
                 SyntaxKind.DoStatement,
                 SyntaxKind.LoopStatement,
                 SyntaxKind.WhileStatement,
                 SyntaxKind.ForStatement,
                 SyntaxKind.ForEachStatement,
                 SyntaxKind.NextStatement,
                 SyntaxKind.UsingStatement,
                 SyntaxKind.ThrowStatement,
                 SyntaxKind.SimpleAssignmentStatement,
                 SyntaxKind.MidAssignmentStatement,
                 SyntaxKind.AddAssignmentStatement,
                 SyntaxKind.SubtractAssignmentStatement,
                 SyntaxKind.MultiplyAssignmentStatement,
                 SyntaxKind.DivideAssignmentStatement,
                 SyntaxKind.IntegerDivideAssignmentStatement,
                 SyntaxKind.ExponentiateAssignmentStatement,
                 SyntaxKind.LeftShiftAssignmentStatement,
                 SyntaxKind.RightShiftAssignmentStatement,
                 SyntaxKind.ConcatenateAssignmentStatement,
                 SyntaxKind.CallStatement,
                 SyntaxKind.AddHandlerStatement,
                 SyntaxKind.RemoveHandlerStatement,
                 SyntaxKind.RaiseEventStatement,
                 SyntaxKind.WithStatement,
                 SyntaxKind.ReDimStatement,
                 SyntaxKind.ReDimPreserveStatement
                Return True
            Case Else
                Return False

        End Select



    End Function

End Module
