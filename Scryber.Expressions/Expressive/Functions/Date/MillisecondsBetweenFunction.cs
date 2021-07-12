﻿using Scryber.Expressive.Expressions;
using System;

namespace Scryber.Expressive.Functions.Date
{
    internal sealed class MillisecondsBetweenFunction : FunctionBase
    {
        #region FunctionBase Members

        public override string Name => "MillisecondsBetween";

        public override object Evaluate(IExpression[] parameters, Context context)
        {
            this.ValidateParameterCount(parameters, 2, 2);

            var startObject = parameters[0].Evaluate(this.Variables);
            var endObject = parameters[1].Evaluate(this.Variables);

            if (startObject is null || endObject is null) { return null; }

            var start = Convert.ToDateTime(startObject, context.CurrentCulture);
            var end = Convert.ToDateTime(endObject, context.CurrentCulture);

            return (end - start).TotalMilliseconds;
        }

        #endregion
    }
}
