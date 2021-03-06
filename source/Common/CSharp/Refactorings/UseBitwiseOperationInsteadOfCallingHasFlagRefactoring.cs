﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Roslynator.CSharp.CSharpFactory;

namespace Roslynator.CSharp.Refactorings
{
    internal static class UseBitwiseOperationInsteadOfCallingHasFlagRefactoring
    {
        public const string Title = "Use bitwise operation instead of calling 'HasFlag'";

        public static bool CanRefactor(
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (invocation.Expression?.IsKind(SyntaxKind.SimpleMemberAccessExpression) == true
                && invocation.ArgumentList?.Arguments.Count == 1)
            {
                MemberAccessExpressionSyntax memberAccess = GetTopmostMemberAccessExpression((MemberAccessExpressionSyntax)invocation.Expression);

                if (memberAccess.Name.Identifier.ValueText == "HasFlag")
                {
                    MethodInfo info;
                    if (semanticModel.TryGetMethodInfo(memberAccess, out info, cancellationToken)
                        && info.IsName("HasFlag")
                        && !info.IsExtensionMethod
                        && info.IsReturnType(SpecialType.System_Boolean)
                        && info.Symbol.SingleParameterOrDefault()?.Type.SpecialType == SpecialType.System_Enum
                        && info.IsContainingType(SpecialType.System_Enum))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static Task<Document> RefactorAsync(
            Document document,
            InvocationExpressionSyntax invocation,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ParenthesizedExpressionSyntax parenthesizedExpression = ParenthesizedExpression(
                BitwiseAndExpression(
                    ((MemberAccessExpressionSyntax)invocation.Expression).Expression,
                    invocation.ArgumentList.Arguments[0].Expression));

            var binaryExpressionKind = SyntaxKind.NotEqualsExpression;
            SyntaxNode nodeToReplace = invocation;

            SyntaxNode parent = invocation.Parent;

            if (!parent.SpanContainsDirectives())
            {
                SyntaxKind parentKind = parent.Kind();

                if (parentKind == SyntaxKind.LogicalNotExpression)
                {
                    binaryExpressionKind = SyntaxKind.EqualsExpression;
                    nodeToReplace = parent;
                }
                else if (parentKind == SyntaxKind.EqualsExpression)
                {
                    ExpressionSyntax right = ((BinaryExpressionSyntax)parent).Right;

                    if (right != null)
                    {
                        SyntaxKind rightKind = right.Kind();

                        if (rightKind == SyntaxKind.TrueLiteralExpression)
                        {
                            binaryExpressionKind = SyntaxKind.NotEqualsExpression;
                            nodeToReplace = parent;
                        }
                        else if (rightKind == SyntaxKind.FalseLiteralExpression)
                        {
                            binaryExpressionKind = SyntaxKind.EqualsExpression;
                            nodeToReplace = parent;
                        }
                    }
                }
            }

            ParenthesizedExpressionSyntax newNode = BinaryExpression(binaryExpressionKind, parenthesizedExpression, NumericLiteralExpression(0))
                .WithTriviaFrom(nodeToReplace)
                .Parenthesize()
                .WithFormatterAnnotation();

            return document.ReplaceNodeAsync(nodeToReplace, newNode, cancellationToken);
        }

        private static MemberAccessExpressionSyntax GetTopmostMemberAccessExpression(MemberAccessExpressionSyntax memberAccess)
        {
            while (memberAccess.IsParentKind(SyntaxKind.SimpleMemberAccessExpression))
                memberAccess = (MemberAccessExpressionSyntax)memberAccess.Parent;

            return memberAccess;
        }
    }
}
