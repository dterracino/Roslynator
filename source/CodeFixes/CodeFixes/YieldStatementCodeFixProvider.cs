﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Roslynator.CSharp.CodeFixes
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(YieldStatementCodeFixProvider))]
    [Shared]
    public class YieldStatementCodeFixProvider : BaseCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(CompilerDiagnosticIdentifiers.YieldStatementCannotBeUsedInsideAnonymousMethodOrLambdaExpression); }
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            if (!Settings.IsCodeFixEnabled(CodeFixIdentifiers.RemoveYieldKeyword))
                return;

            SyntaxNode root = await context.GetSyntaxRootAsync().ConfigureAwait(false);

            YieldStatementSyntax yieldStatement = root
                .FindNode(context.Span, getInnermostNodeForTie: true)?
                .FirstAncestorOrSelf<YieldStatementSyntax>();

            Debug.Assert(yieldStatement != null, $"{nameof(yieldStatement)} is null");

            if (yieldStatement == null)
                return;

            foreach (Diagnostic diagnostic in context.Diagnostics)
            {
                switch (diagnostic.Id)
                {
                    case CompilerDiagnosticIdentifiers.YieldStatementCannotBeUsedInsideAnonymousMethodOrLambdaExpression:
                        {
                            CodeAction codeAction = CodeAction.Create(
                                "Remove 'yield'",
                                cancellationToken =>
                                {
                                    SyntaxToken yieldKeyword = yieldStatement.YieldKeyword;
                                    SyntaxToken returnKeyword = yieldStatement.ReturnOrBreakKeyword;
                                    ExpressionSyntax expression = yieldStatement.Expression;

                                    SyntaxTriviaList leadingTrivia = yieldKeyword.LeadingTrivia
                                        .AddRange(yieldKeyword.TrailingTrivia.EmptyIfWhitespace())
                                        .AddRange(returnKeyword.LeadingTrivia.EmptyIfWhitespace());

                                    ReturnStatementSyntax newNode = ReturnStatement(
                                        returnKeyword.WithLeadingTrivia(leadingTrivia),
                                        yieldStatement.Expression,
                                        yieldStatement.SemicolonToken);

                                    return context.Document.ReplaceNodeAsync(yieldStatement, newNode, cancellationToken);
                                },
                                GetEquivalenceKey(diagnostic));

                            context.RegisterCodeFix(codeAction, diagnostic);
                            break;
                        }
                }
            }
        }
    }
}
