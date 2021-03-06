﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Roslynator.CSharp.CSharpFactory;

namespace Roslynator.CSharp.Refactorings
{
    internal static class UseListInsteadOfYieldRefactoring
    {
        public static void ComputeRefactoring(RefactoringContext context, YieldStatementSyntax yieldStatement, SemanticModel semanticModel)
        {
            ExpressionSyntax expression = yieldStatement.Expression;
            if (expression != null)
            {
                ITypeSymbol typeSymbol = semanticModel.GetTypeSymbol(expression, context.CancellationToken);

                if (typeSymbol?.IsErrorType() == false
                    && !typeSymbol.IsVoid())
                {
                    BlockSyntax block = GetContainigBlock(yieldStatement);
                    if (block != null)
                    {
                        context.RegisterRefactoring(
                            "Use List<T> instead of yield",
                            cancellationToken => RefactorAsync(context.Document, yieldStatement, block, block.Statements, cancellationToken));
                    }
                }
            }
        }

        private static BlockSyntax GetContainigBlock(YieldStatementSyntax yieldStatement)
        {
            SyntaxNode current = yieldStatement.Parent;

            while (current != null)
            {
                switch (current.Kind())
                {
                    case SyntaxKind.MethodDeclaration:
                        return ((MethodDeclarationSyntax)current).Body;
                    case SyntaxKind.GetAccessorDeclaration:
                        return ((AccessorDeclarationSyntax)current).Body;
                    case SyntaxKind.LocalFunctionStatement:
                        return ((LocalFunctionStatementSyntax)current).Body;
                    case SyntaxKind.SimpleLambdaExpression:
                    case SyntaxKind.ParenthesizedLambdaExpression:
                    case SyntaxKind.AnonymousMethodExpression:
                        return null;
                }

                current = current.Parent;
            }

            return null;
        }

        private static async Task<Document> RefactorAsync(
            Document document,
            YieldStatementSyntax yieldStatement,
            BlockSyntax block,
            SyntaxList<StatementSyntax> statements,
            CancellationToken cancellationToken)
        {
            SemanticModel semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            int position = statements[0].SpanStart;

            string name = NameGenerator.Default.EnsureUniqueLocalName("items", semanticModel, position);

            IdentifierNameSyntax identifierName = IdentifierName(name);

            ITypeSymbol typeSymbol = semanticModel.GetTypeSymbol(yieldStatement.Expression, cancellationToken);

            TypeSyntax listType = semanticModel
                .GetTypeByMetadataName(MetadataNames.System_Collections_Generic_List_T)
                .Construct(typeSymbol)
                .ToMinimalTypeSyntax(semanticModel, position);

            LocalDeclarationStatementSyntax localDeclarationStatement = LocalDeclarationStatement(
                    VarType(),
                    name,
                    EqualsValueClause(ObjectCreationExpression(listType, ArgumentList())));

            localDeclarationStatement = localDeclarationStatement.WithFormatterAnnotation();

            SyntaxList<StatementSyntax> newStatements = InsertLocalDeclarationStatement(statements, localDeclarationStatement);

            ReturnStatementSyntax returnStatement = ReturnStatement(identifierName).WithFormatterAnnotation();

            newStatements = InsertReturnStatement(newStatements, returnStatement);

            BlockSyntax newBlock = block.WithStatements(newStatements);

            var rewriter = new YieldRewriter(identifierName, typeSymbol, semanticModel);

            newBlock = (BlockSyntax)rewriter.Visit(newBlock);

            return await document.ReplaceNodeAsync(block, newBlock, cancellationToken).ConfigureAwait(false);
        }

        private static SyntaxList<StatementSyntax> InsertLocalDeclarationStatement(
            SyntaxList<StatementSyntax> statements,
            LocalDeclarationStatementSyntax localDeclarationStatement)
        {
            int insertIndex = 0;

            while (insertIndex < statements.Count
                && IsParameterCheck(statements[insertIndex]))
            {
                insertIndex++;
            }

            return statements.Insert(insertIndex, localDeclarationStatement);
        }

        private static SyntaxList<StatementSyntax> InsertReturnStatement(SyntaxList<StatementSyntax> newStatements, ReturnStatementSyntax returnStatement)
        {
            int insertIndex = newStatements.Count;

            while (insertIndex > 0
                && newStatements[insertIndex - 1].IsKind(SyntaxKind.LocalFunctionStatement))
            {
                insertIndex--;
            }

            return newStatements.Insert(insertIndex, returnStatement);
        }

        private static bool IsParameterCheck(StatementSyntax statement)
        {
            if (statement.IsKind(SyntaxKind.IfStatement))
            {
                var ifStatement = (IfStatementSyntax)statement;

                return ifStatement.GetSingleStatementOrDefault()?.IsKind(SyntaxKind.ThrowStatement) == true;
            }

            return false;
        }

        private class YieldRewriter : CSharpSyntaxRewriter
        {
            private static readonly IdentifierNameSyntax _addName = IdentifierName("Add");

            private readonly IdentifierNameSyntax _identifierName;
            private readonly ITypeSymbol _typeSymbol;
            private readonly SemanticModel _semanticModel;

            public YieldRewriter(IdentifierNameSyntax identifierName, ITypeSymbol typeSymbol, SemanticModel semanticModel)
            {
                _identifierName = identifierName;
                _typeSymbol = typeSymbol;
                _semanticModel = semanticModel;
            }

            public override SyntaxNode VisitYieldStatement(YieldStatementSyntax node)
            {
                SyntaxToken keyword = node.ReturnOrBreakKeyword;
                ExpressionSyntax expression = node.Expression;

                SyntaxKind kind = node.Kind();

                if (kind == SyntaxKind.YieldReturnStatement)
                {
                    ParenthesizedExpressionSyntax parenthesizedExpression = expression.Parenthesize();

                    CastExpressionSyntax castExpression = CastExpression(
                        _typeSymbol.ToMinimalTypeSyntax(_semanticModel, node.SpanStart),
                        parenthesizedExpression);

                    InvocationExpressionSyntax invocationExpression = SimpleMemberInvocationExpression(
                        _identifierName,
                        _addName,
                        Argument(castExpression.WithSimplifierAnnotation()));

                    return ExpressionStatement(invocationExpression.WithoutTrivia())
                        .WithTriviaFrom(node)
                        .AppendToLeadingTrivia(node.DescendantTrivia(TextSpan.FromBounds(keyword.Span.End, expression.Span.Start)));
                }
                else if (kind == SyntaxKind.YieldBreakStatement)
                {
                    return ReturnStatement(
                        Token(keyword.LeadingTrivia, SyntaxKind.ReturnKeyword, keyword.TrailingTrivia),
                        _identifierName,
                        node.SemicolonToken);
                }

                Debug.Fail(node.Kind().ToString());

                return base.VisitYieldStatement(node);
            }

            public override SyntaxNode VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
            {
                return node;
            }

            public override SyntaxNode VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
            {
                return node;
            }

            public override SyntaxNode VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
            {
                return node;
            }

            public override SyntaxNode VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
            {
                return node;
            }
        }
    }
}
