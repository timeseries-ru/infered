using System;
using System.IO;
using System.Data;
using System.Collections.Generic;
using System.Collections.Immutable;

using CsvHelper;

using Pidgin;
using static Pidgin.Parser;
using Pidgin.Expression;

using Microsoft.ML.Probabilistic.Models;
using Microsoft.ML.Probabilistic.Distributions;
using Microsoft.ML.Probabilistic.Algorithms;

using static infered.IExpr;

namespace infered
{
    class ModelParser
    {
        public static Parser<char, T> Tok<T>(Parser<char, T> token)
            => Try(token).Before(SkipWhitespaces);
        public static Parser<char, string> Tok(string token)
            => Tok(String(token));

        public static Parser<char, T> Parenthesised<T>(Parser<char, T> parser)
            => parser.Between(Tok("("), Tok(")"));

        public static Parser<char, Func<IExpr, IExpr, IExpr>> Binary(Parser<char, BinaryOperatorType> op)
            => op.Select<Func<IExpr, IExpr, IExpr>>(type => (l, r) => new BinaryOp(type, l, r));
        public static Parser<char, Func<IExpr, IExpr>> Unary(Parser<char, UnaryOperatorType> op)
            => op.Select<Func<IExpr, IExpr>>(type => o => new UnaryOp(type, o));

        public static readonly Parser<char, Func<IExpr, IExpr, IExpr>> Add
            = Binary(Tok("+").ThenReturn(BinaryOperatorType.Add));
        public static readonly Parser<char, Func<IExpr, IExpr, IExpr>> Mul
            = Binary(Tok("*").ThenReturn(BinaryOperatorType.Mul));
        public static readonly Parser<char, Func<IExpr, IExpr>> Neg
            = Unary(Tok("-").ThenReturn(UnaryOperatorType.Neg));
        public static readonly Parser<char, Func<IExpr, IExpr, IExpr>> Sub
            = Binary(Tok("-").ThenReturn(BinaryOperatorType.Sub));
        public static readonly Parser<char, Func<IExpr, IExpr, IExpr>> Div
            = Binary(Tok("/").ThenReturn(BinaryOperatorType.Div));
        public static readonly Parser<char, Func<IExpr, IExpr, IExpr>> Powered
            = Binary(Tok("^").ThenReturn(BinaryOperatorType.Powered));

        public static readonly Parser<char, IExpr> Identifier
            = Tok(Letter.Then(LetterOrDigit.ManyString(), (h, t) => h + t))
                .Select<IExpr>(name => new Identifier(name))
                .Labelled("identifier");

        public static readonly Parser<char, IExpr> Floating
            = Map((left, middle, right) => Double.Parse(left + "." + right), 
                   DecimalNum, Tok("."), Digit.ManyString()) 
             .Select<IExpr>(value => new Literal(value))
             .Labelled("literal");

        public static Parser<char, IExpr> CreateExpressionParser()
        {
            Parser<char, IExpr> expr = null;
            
            var call = Parenthesised(Rec(() => expr).Separated(Tok(",")))
                .Select<Func<IExpr, IExpr>>(
                    args => method => new Call(method, args.ToImmutableArray())
                ).Labelled("function call");
            
            var term = OneOf(
                Identifier,
                Floating,
                Parenthesised(Rec(() => expr)).Labelled("parenthesised expression")
            );

            expr = ExpressionParser.Build(
                term,
                new[]
                {                    
                    Operator.PostfixChainable(call),                                        
                    Operator.InfixL(Powered),
                    Operator.Prefix(Neg),                    
                    Operator.InfixL(Div),
                    Operator.InfixL(Mul),
                    Operator.InfixL(Sub),
                    Operator.InfixL(Add)
                }
            ).Labelled("expression");

            return expr;
        }

        public static Parser<char, IExpr> Expr = CreateExpressionParser();

        public static IExpr ParseOrThrow(string input)
            => Expr.ParseOrThrow(input);
    }

    class ModelTrain
    {
        private ModelParameters parameters = new ModelParameters();

        public void CreateModel(string modelText, DataTable data)
        {
            var range = new Range(data.Rows.Count);

            foreach (var line in modelText.Split(Environment.NewLine))
            {
                var splitted = line.ToLower().Replace(" ", "").Split("~");
                if (splitted.Length < 2)
                {
                    continue;
                }
                var expressed = ModelParser.ParseOrThrow(splitted[1]);
                var variable = (((IExprVar)expressed).MakeVariable(parameters, data, range));
                if (data.Columns.IndexOf(splitted[0]) >= 0)
                {
                    var values = ModelParameters.ExtractValues(splitted[0], data);
                    var observed = Variable.Observed(values.ToArray(), range);
                    observed[range] = variable;
                }

                parameters.Scope.Add(variable.Named(splitted[0]));
            }
        }

        public void InferModel(string[] varsToInfer)
        {
            var engine = new InferenceEngine();
            engine.Compiler.RecommendedQuality =
                Microsoft.ML.Probabilistic.Factors.Attributes.QualityBand.Experimental;
            foreach (var variable in varsToInfer)
            {
              var varToInfer = parameters.GetNamed(variable);
              var infered = engine.Infer(varToInfer);
              Console.WriteLine(variable + ": " + infered);
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: infered data.csv definitions.model variableToInfer1 ... variableToInferN");
            }
            else
            {
                var datafile = args[0];
                var defitions = args[1];
                var data = new DataTable();
                var model = "";

                var variablesToInfer = new List<string>(args);
                variablesToInfer.RemoveAt(0);
                variablesToInfer.RemoveAt(0);

                try
                {
                    using (var reader = new StreamReader(datafile))
                    using (var csv = new CsvReader(reader))
                    {
                        using (var dr = new CsvDataReader(csv))
                        {
                            data.Load(dr);
                        }
                    }

                    foreach (DataColumn col in data.Columns)
                    {
                        col.ColumnName = col.ColumnName.ToLower();
                    }

                    using (var reader = new StreamReader(defitions))
                    {
                        model = reader.ReadToEnd();
                    }

                    var trainer = new ModelTrain();
                    trainer.CreateModel(model, data);
                    trainer.InferModel(variablesToInfer.ToArray());

                } catch (Exception exception) {
                    Console.WriteLine(exception.Message);
                    // Console.WriteLine(exception.StackTrace);
                }
            }
        }
    }
}
