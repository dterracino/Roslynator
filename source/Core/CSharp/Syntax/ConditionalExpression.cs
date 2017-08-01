// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslynator.CSharp.Syntax
{
    internal struct ConditionalExpression
    {
        public ConditionalExpression(
            ExpressionSyntax condition,
            ExpressionSyntax whenTrue,
            ExpressionSyntax whenFalse)
        {
            Condition = condition;
            WhenTrue = whenTrue;
            WhenFalse = whenFalse;
        }

        public ExpressionSyntax Condition { get; }

        public ExpressionSyntax WhenTrue { get; }

        public ExpressionSyntax WhenFalse { get; }

        public ConditionalExpressionSyntax Node
        {
            get { return Condition.FirstAncestor<ConditionalExpressionSyntax>(); }
        }

        public static bool TryCreate(
            SyntaxNode node,
            out ConditionalExpression result,
            bool allowNullOrMissing = false,
            bool walkDownParentheses = true)
        {
            ExpressionSyntax expression = (node as ExpressionSyntax)?.WalkDownParenthesesIf(walkDownParentheses);

            if (expression?.IsKind(SyntaxKind.ConditionalExpression) == true)
                return TryCreate((ConditionalExpressionSyntax)expression, out result, allowNullOrMissing: allowNullOrMissing, walkDownParentheses: walkDownParentheses);

            result = default(ConditionalExpression);
            return false;
        }

        public static bool TryCreate(
            ConditionalExpressionSyntax conditionalExpression,
            out ConditionalExpression result,
            bool allowNullOrMissing = false,
            bool walkDownParentheses = true)
        {
            if (conditionalExpression != null)
            {
                ExpressionSyntax condition = conditionalExpression.Condition?.WalkDownParenthesesIf(walkDownParentheses);

                if (allowNullOrMissing || condition?.IsMissing == false)
                {
                    ExpressionSyntax whenTrue = conditionalExpression.WhenTrue?.WalkDownParenthesesIf(walkDownParentheses);

                    if (allowNullOrMissing || whenTrue?.IsMissing == false)
                    {
                        ExpressionSyntax whenFalse = conditionalExpression.WhenFalse?.WalkDownParenthesesIf(walkDownParentheses);

                        if (allowNullOrMissing || whenFalse?.IsMissing == false)
                        {
                            result = new ConditionalExpression(condition, whenTrue, whenFalse);
                            return true;
                        }
                    }
                }
            }

            result = default(ConditionalExpression);
            return false;
        }

        public override string ToString()
        {
            return Node?.ToString() ?? base.ToString();
        }
    }
}
