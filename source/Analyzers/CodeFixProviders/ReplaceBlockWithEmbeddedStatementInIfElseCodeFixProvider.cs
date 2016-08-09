﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pihrtsoft.CodeAnalysis.CSharp.Analysis;
using Pihrtsoft.CodeAnalysis.CSharp.Refactoring;

namespace Pihrtsoft.CodeAnalysis.CSharp.CodeFixProviders
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ReplaceBlockWithEmbeddedStatementInIfElseCodeFixProvider))]
    [Shared]
    public class ReplaceBlockWithEmbeddedStatementInIfElseCodeFixProvider : BaseCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
            => ImmutableArray.Create(DiagnosticIdentifiers.ReplaceBlockWithEmbeddedStatementInIfElse);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            IfStatementSyntax ifStatement = root
                .FindNode(context.Span, getInnermostNodeForTie: true)?
                .FirstAncestorOrSelf<IfStatementSyntax>();

            if (ifStatement == null)
                return;

            ifStatement = IfElseChainAnalysis.GetTopmostIf(ifStatement);

            CodeAction codeAction = CodeAction.Create(
                "Replace block with embedded statement (in if-else).",
                cancellationToken => ReplaceBlockWithEmbeddedStatementInIfElseRefactoring.RefactorAsync(context.Document, ifStatement, cancellationToken),
                DiagnosticIdentifiers.ReplaceBlockWithEmbeddedStatementInIfElse + EquivalenceKeySuffix);

            context.RegisterCodeFix(codeAction, context.Diagnostics);
        }
    }
}