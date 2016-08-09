﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Pihrtsoft.CodeAnalysis.CSharp.Refactoring
{
    internal static class BlockRefactoring
    {
        public static async Task ComputeRefactoringAsync(RefactoringContext context, BlockSyntax block)
        {
            ReplaceBlockWithEmbeddedStatementRefactoring.ComputeRefactoring(context, block);

            if (context.Settings.IsAnyRefactoringEnabled(
                RefactoringIdentifiers.MergeIfStatements,
                RefactoringIdentifiers.CollapseToInitializer,
                RefactoringIdentifiers.WrapStatementsInIfStatement,
                RefactoringIdentifiers.WrapStatementsInTryCatch))
            {
                var blockSpan = new BlockSpan(block, context.Span);

                if (context.Settings.IsRefactoringEnabled(RefactoringIdentifiers.CollapseToInitializer)
                    && blockSpan.HasSelectedStatement)
                {
                    await CollapseToInitializerRefactoring.ComputeRefactoringsAsync(context, blockSpan);
                }

                using (IEnumerator<StatementSyntax> en = GetSelectedStatements(block, context.Span).GetEnumerator())
                {
                    if (en.MoveNext())
                    {
                        if (context.Settings.IsRefactoringEnabled(RefactoringIdentifiers.MergeIfStatements)
                            && en.Current.IsKind(SyntaxKind.IfStatement))
                        {
                            var statements = new List<IfStatementSyntax>();
                            statements.Add((IfStatementSyntax)en.Current);

                            while (en.MoveNext())
                            {
                                if (en.Current.IsKind(SyntaxKind.IfStatement))
                                {
                                    statements.Add((IfStatementSyntax)en.Current);
                                }
                                else
                                {
                                    statements = null;
                                    break;
                                }
                            }

                            if (statements?.Count > 1)
                            {
                                context.RegisterRefactoring(
                                    "Merge if statements",
                                    cancellationToken =>
                                    {
                                        return MergeIfStatementsRefactoring.RefactorAsync(
                                            context.Document,
                                            block,
                                            statements.ToImmutableArray(),
                                            cancellationToken);
                                    });
                            }
                        }

                        if (context.Settings.IsRefactoringEnabled(RefactoringIdentifiers.MergeAssignmentExpressionWithReturnStatement)
                            && en.Current.IsKind(SyntaxKind.ExpressionStatement))
                        {
                            var statement = (ExpressionStatementSyntax)en.Current;

                            if (statement.Expression?.IsKind(SyntaxKind.SimpleAssignmentExpression) == true
                                && en.MoveNext()
                                && en.Current.IsKind(SyntaxKind.ReturnStatement))
                            {
                                var returnStatement = (ReturnStatementSyntax)en.Current;

                                if (returnStatement.Expression?.IsMissing == false
                                    && !en.MoveNext())
                                {
                                    var assignment = (AssignmentExpressionSyntax)statement.Expression;

                                    if (assignment.Left?.IsMissing == false
                                        && assignment.Right?.IsMissing == false
                                        && assignment.Left.IsEquivalentTo(returnStatement.Expression, topLevel: false))
                                    {
                                        context.RegisterRefactoring(
                                            "Merge statements",
                                            cancellationToken =>
                                            {
                                                return MergeAssignmentExpressionWithReturnStatementRefactoring.RefactorAsync(
                                                    context.Document,
                                                    statement,
                                                    returnStatement,
                                                    cancellationToken);
                                            });
                                    }
                                }
                            }
                        }

                        if (context.Settings.IsRefactoringEnabled(RefactoringIdentifiers.WrapStatementsInIfStatement))
                        {
                            context.RegisterRefactoring(
                                "Wrap in if statement",
                                cancellationToken =>
                                {
                                    var refactoring = new WrapStatementsInIfStatementRefactoring();

                                    return refactoring.RefactorAsync(
                                        context.Document,
                                        block,
                                        context.Span,
                                        cancellationToken);
                                });
                        }

                        if (context.Settings.IsRefactoringEnabled(RefactoringIdentifiers.WrapStatementsInTryCatch))
                        {
                            context.RegisterRefactoring(
                                "Wrap in try-catch",
                                cancellationToken =>
                                {
                                    var refactoring = new WrapStatementsInTryCatchRefactoring();

                                    return refactoring.RefactorAsync(
                                        context.Document,
                                        block,
                                        context.Span,
                                        cancellationToken);
                                });
                        }
                    }
                }
            }
        }

        public static IEnumerable<StatementSyntax> GetSelectedStatements(BlockSyntax block, TextSpan span)
        {
            return block.Statements
                .SkipWhile(f => span.Start > f.Span.Start)
                .TakeWhile(f => span.End >= f.Span.End);
        }
    }
}