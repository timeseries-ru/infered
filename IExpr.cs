using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;

using Microsoft.ML.Probabilistic.Models;
using Microsoft.ML.Probabilistic;
using System.Data;
using System.Reflection;

namespace infered
{
    public class ModelParameters
    {
        public List<Variable<double>> Scope = new List<Variable<double>>();

        public bool HasNamed(string name)
        {
          foreach (var variable in Scope)
          {
            var index = variable.Name.IndexOf("[");
            var check = index >= 0 ? variable.Name.Substring(0, index) : variable.Name;  
            if (check == name)
              return true;
          }
          return false;
        }

        public Variable<double> GetNamed(string name)
        {
            foreach (var variable in Scope)
            {
              var index = variable.Name.IndexOf("[");
              var check = index >= 0 ? variable.Name.Substring(0, index) : variable.Name;
              if (check == name)
                return variable;
            }
            return null;
        }

        public static List<double> ExtractValues(string name, DataTable table)
        {
            var values = new List<double>();
            var target = table.Columns.IndexOf(name);
            foreach (DataRow row in table.Rows)
            {
                values.Add(double.Parse(row[target].ToString()));
            }
            return values;
        }
    }

    public interface IExpr : IEquatable<IExpr> {}

    public interface IExprVar : IExpr
    {
        Variable<double> MakeVariable(ModelParameters parameters, DataTable data, Range range);
    }

    public class Identifier : IExprVar
    {
        public string Name { get; }

        public Identifier(string name)
        {
            Name = name;
        }

        public bool Equals(IExpr other)
            => other is Identifier i && this.Name == i.Name;

        public Variable<double> MakeVariable(ModelParameters parameters, DataTable data, Range range)
        {
            if (parameters.HasNamed(Name))
                return parameters.GetNamed(Name);
            var values = ModelParameters.ExtractValues(Name, data);
            var observed = VariableArray<double>.Observed(values.ToArray(), range);
            return observed[range];
        }
    }

    public class Literal : IExpr
    {
        public double Value { get; }

        public Literal(double value)
        {
            Value = value;
        }

        public bool Equals(IExpr other)
            => other is Literal l && this.Value == l.Value;
    }

    public class Call : IExprVar
    {
        public IExpr Expr { get; }
        public ImmutableArray<IExpr> Arguments { get; }

        public Call(IExpr expr, ImmutableArray<IExpr> arguments)
        {
            Expr = expr;
            Arguments = arguments;
        }

        public bool Equals(IExpr other)
            => other is Call c
            && this.Expr.Equals(c.Expr)
            && this.Arguments.SequenceEqual(c.Arguments);

        public Variable<double> MakeVariable(ModelParameters parameters, DataTable data, Range range)
        {
            var type = typeof(Variable<double>);
            var args = new List<object>();
            var types = new List<Type>();

            foreach (var arg in Arguments)
            {
                if (arg is IExprVar)
                {
                    args.Add(((IExprVar)arg).MakeVariable(parameters, data, range));
                    types.Add(type);
                }
                else
                {
                    args.Add(((Literal)arg).Value);
                    types.Add(typeof(double));
                }
            }

            var method = type.GetMethod(
                ((Identifier)Expr).Name,
                BindingFlags.Public | BindingFlags.FlattenHierarchy |
                BindingFlags.Static | BindingFlags.IgnoreCase,
                null,
                types.ToArray(),
                null
            );

            var result = method.Invoke(null, args.ToArray());            
            return result as Variable<double>;
        }
    }

    public enum UnaryOperatorType
    {
        Neg
    }

    public class UnaryOp : IExprVar
    {
        public UnaryOperatorType Type { get; }
        public IExpr Expr { get; }

        public UnaryOp(UnaryOperatorType type, IExpr expr)
        {
            Type = type;
            Expr = expr;
        }

        public bool Equals(IExpr other)
            => other is UnaryOp u
            && this.Type == u.Type
            && this.Expr.Equals(u.Expr);

        public Variable<double> MakeVariable(ModelParameters parameters, DataTable data, Range range)
        {
            if (Type == UnaryOperatorType.Neg)
            {
                if (Expr is IExprVar)
                {
                    return -(((IExprVar)Expr).MakeVariable(parameters, data, range) as Variable<double>);
                }
                return Variable<double>.Constant((Expr as Literal).Value);
            }

            throw new NotImplementedException();
        }
    }

    public enum BinaryOperatorType
    {
        Add,
        Mul,
        Sub,
        Div,
        Powered
    }
    public class BinaryOp : IExprVar
    {
        public BinaryOperatorType Type { get; }
        public IExpr Left { get; }
        public IExpr Right { get; }

        public BinaryOp(BinaryOperatorType type, IExpr left, IExpr right)
        {
            Type = type;
            Left = left;
            Right = right;
        }

        public bool Equals(IExpr other)
            => other is BinaryOp b
            && this.Type == b.Type
            && this.Left.Equals(b.Left)
            && this.Right.Equals(b.Right);

        public Variable<double> MakeVariable(ModelParameters parameters, DataTable data, Range range)
        {
            Variable<double> left = null;
            Variable<double> right = null;

            if (Left is Literal)
            {
                left = Variable<double>.Constant((double)((Literal)Left).Value);
            }
            else
            {
                left = ((IExprVar)Left).MakeVariable(parameters, data, range) as Variable<double>;
            }

            if (Right is Literal)
            {
                right = Variable<double>.Constant((double)((Literal)Right).Value);
            }
            else
            {
                right = ((IExprVar)Right).MakeVariable(parameters, data, range) as Variable<double>;
            }

            if (Type == BinaryOperatorType.Div)
            {
                return left / right;
            }
            else if (Type == BinaryOperatorType.Mul)
            {
                return left * right;
            }
            else if (Type == BinaryOperatorType.Add)
            {
                return left + right;
            }
            else if (Type == BinaryOperatorType.Sub)
            {
                return left - right;
            }
            else if (Type == BinaryOperatorType.Powered)
            {
                return left ^ right;
            }

            throw new NotImplementedException();
        }
    }
}
