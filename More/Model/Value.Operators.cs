using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Compiler;

namespace More.Model
{
    enum Operator
    {
        Mult,
        Div,
        Mod,
        Plus,
        Minus,
        Take_Exists
    }
    
    partial class Value
    {
        // Anything not in the this list is not a convertable unit, the ratios are to millimeters
        internal static ReadOnlyDictionary<Unit, decimal> ConvertableUnits = new ReadOnlyDictionary<Unit, decimal>(
            new Dictionary<Unit, decimal>()
            {
                { Unit.MM, 1m },
                { Unit.IN, 25.4m },
                { Unit.CM, 10m },
                { Unit.PT, 0.353m },
                { Unit.PC, 4.233m }
            }
        );

        private static bool TryConvertBetweenUnits(decimal from, Unit fromUnit, Unit toUnit, out decimal converted)
        {
            if (fromUnit == toUnit)
            {
                converted = from;
                return true;
            }

            decimal fromRatio, toRatio;

            if (!ConvertableUnits.TryGetValue(fromUnit, out fromRatio) || !ConvertableUnits.TryGetValue(toUnit, out toRatio))
            {
                converted = 0;
                return false;
            }

            converted = (from / fromRatio) * toRatio;
            return true;
        }

        private static Type[] AddableTypes = new Type[] { typeof(NumberValue), typeof(NumberWithUnitValue), typeof(QuotedStringValue), typeof(MathValue) };
        public static Value operator +(Value lhs, Value rhs)
        {
            if (lhs == ExcludeFromOutputValue.Singleton || rhs == ExcludeFromOutputValue.Singleton) return ExcludeFromOutputValue.Singleton;
            if (lhs is NotFoundValue || rhs is NotFoundValue) return NotFoundValue.Default.BindToPosition(lhs.Start, rhs.Stop, lhs.FilePath);

            if (!AddableTypes.Contains(lhs.GetType()) || !AddableTypes.Contains(rhs.GetType()))
            {
                var from = Math.Min(lhs.Start, rhs.Start);
                var to = Math.Max(lhs.Stop, rhs.Stop);

                Current.RecordError(ErrorType.Compiler, Position.Create(from, to, lhs.FilePath), "Interpreted as addition, but did not find numbers or strings at runtime");
                return ExcludeFromOutputValue.Singleton;
            }

            if (lhs is MathValue || rhs is MathValue)
            {
                return new MathValue(lhs, Operator.Plus, rhs);
            }

            if((lhs.GetType() == typeof(QuotedStringValue) && rhs.GetType() != typeof(QuotedStringValue)) ||
               (rhs.GetType() == typeof(QuotedStringValue) && lhs.GetType() != typeof(QuotedStringValue)))
            {
                var from = Math.Min(lhs.Start, rhs.Start);
                var to = Math.Max(lhs.Stop, rhs.Stop);

                Current.RecordWarning(ErrorType.Compiler, Position.Create(from, to, lhs.FilePath), "Interpreted as string concatenation, but did not find two strings at runtime");
                return new CompoundValue(new List<Value>(){lhs, new StringValue("+"), rhs});
            }

            // String concat
            if (lhs.GetType() == typeof(QuotedStringValue))
            {
                return new QuotedStringValue(
                    ((QuotedStringValue)lhs).Value +
                    ((QuotedStringValue)rhs).Value
                );
            }

            if (lhs.GetType() == typeof(NumberValue) && rhs.GetType() == typeof(NumberValue))
            {
                return new NumberValue(
                    ((NumberValue)lhs).Value +
                    ((NumberValue)rhs).Value
                );
            }

            // Adding two numbers, only one of which has a unit
            if((lhs.GetType() == typeof(NumberValue) && rhs.GetType() != typeof(NumberValue)) ||
               (rhs.GetType() == typeof(NumberValue) && lhs.GetType() != typeof(NumberValue)))
            {
                Unit unit;
                if (lhs.GetType() == typeof(NumberWithUnitValue))
                {
                    unit = ((NumberWithUnitValue)lhs).Unit;
                }
                else
                {
                    unit = ((NumberWithUnitValue)rhs).Unit;
                }

                return new NumberWithUnitValue(
                    ((NumberValue)lhs).Value +
                    ((NumberValue)rhs).Value,
                    unit
                );
            }

            var lhsAsUnit = (NumberWithUnitValue)lhs;
            var rhsAsUnit = (NumberWithUnitValue)rhs;

            decimal rhsConverted;
            if (!TryConvertBetweenUnits(rhsAsUnit.Value, rhsAsUnit.Unit, lhsAsUnit.Unit, out rhsConverted))
            {
                var from = Math.Min(lhs.Start, rhs.Start);
                var to = Math.Max(lhs.Stop, rhs.Stop);

                Current.RecordError(ErrorType.Compiler, Position.Create(from, to, lhs.FilePath), "No conversion exists between [" + lhsAsUnit.Unit + "] and [" + rhsAsUnit.Unit + "]");
                throw new StoppedCompilingException();
            }

            return new NumberWithUnitValue(
                lhsAsUnit.Value + rhsConverted,
                lhsAsUnit.Unit
            );
        }

        private static Type[] GeneralMathTypes = new Type[] { typeof(NumberValue), typeof(NumberWithUnitValue), typeof(MathValue) };
        public static Value operator -(Value lhs, Value rhs)
        {
            if (lhs == ExcludeFromOutputValue.Singleton || rhs == ExcludeFromOutputValue.Singleton) return ExcludeFromOutputValue.Singleton;
            if (lhs is NotFoundValue || rhs is NotFoundValue) return NotFoundValue.Default.BindToPosition(lhs.Start, rhs.Stop, lhs.FilePath);

            if (!GeneralMathTypes.Contains(lhs.GetType()) || !GeneralMathTypes.Contains(rhs.GetType()))
            {
                var from = Math.Min(lhs.Start, rhs.Start);
                var to = Math.Max(lhs.Stop, rhs.Stop);

                Current.RecordError(ErrorType.Compiler, Position.Create(from, to, lhs.FilePath), "Interpreted as subtraction, but did not find numbers at runtime");
                return ExcludeFromOutputValue.Singleton;
            }

            if (lhs is MathValue || rhs is MathValue)
            {
                return new MathValue(lhs, Operator.Minus, rhs);
            }

            if (lhs.GetType() == typeof(NumberValue) && rhs.GetType() == typeof(NumberValue))
            {
                return new NumberValue(
                    ((NumberValue)lhs).Value -
                    ((NumberValue)rhs).Value
                );
            }

            // Subtracting two numbers, only one of which has a unit
            if ((lhs.GetType() == typeof(NumberValue) && rhs.GetType() != typeof(NumberValue)) ||
               (rhs.GetType() == typeof(NumberValue) && lhs.GetType() != typeof(NumberValue)))
            {
                Unit unit;
                if (lhs.GetType() == typeof(NumberWithUnitValue))
                {
                    unit = ((NumberWithUnitValue)lhs).Unit;
                }
                else
                {
                    unit = ((NumberWithUnitValue)rhs).Unit;
                }

                return new NumberWithUnitValue(
                    ((NumberValue)lhs).Value -
                    ((NumberValue)rhs).Value,
                    unit
                );
            }

            var lhsAsUnit = (NumberWithUnitValue)lhs;
            var rhsAsUnit = (NumberWithUnitValue)rhs;

            decimal rhsConverted;
            if (!TryConvertBetweenUnits(rhsAsUnit.Value, rhsAsUnit.Unit, lhsAsUnit.Unit, out rhsConverted))
            {
                var from = Math.Min(lhs.Start, rhs.Start);
                var to = Math.Max(lhs.Stop, rhs.Stop);

                Current.RecordError(ErrorType.Compiler, Position.Create(from, to, lhs.FilePath), "No conversion exists between [" + lhsAsUnit.Unit + "] and [" + rhsAsUnit.Unit + "]");
                throw new StoppedCompilingException();
            }

            return new NumberWithUnitValue(
                lhsAsUnit.Value - rhsConverted,
                lhsAsUnit.Unit
            );
        }

        public static Value operator *(Value lhs, Value rhs)
        {
            if (lhs == ExcludeFromOutputValue.Singleton || rhs == ExcludeFromOutputValue.Singleton) return ExcludeFromOutputValue.Singleton;
            if (lhs is NotFoundValue || rhs is NotFoundValue) return NotFoundValue.Default.BindToPosition(lhs.Start, rhs.Stop, lhs.FilePath);

            if (!GeneralMathTypes.Contains(lhs.GetType()) || !GeneralMathTypes.Contains(rhs.GetType()))
            {
                var from = Math.Min(lhs.Start, rhs.Start);
                var to = Math.Max(lhs.Stop, rhs.Stop);

                Current.RecordError(ErrorType.Compiler, Position.Create(from, to, lhs.FilePath), "Interpreted as multiplication, but did not find numbers at runtime");
                return ExcludeFromOutputValue.Singleton;
            }

            if (lhs is MathValue || rhs is MathValue)
            {
                return new MathValue(lhs, Operator.Mult, rhs);
            }

            if (lhs.GetType() == typeof(NumberValue) && rhs.GetType() == typeof(NumberValue))
            {
                return new NumberValue(
                    ((NumberValue)lhs).Value *
                    ((NumberValue)rhs).Value
                );
            }

            // Multiplying two numbers, only one of which has a unit
            if ((lhs.GetType() == typeof(NumberValue) && rhs.GetType() != typeof(NumberValue)) ||
               (rhs.GetType() == typeof(NumberValue) && lhs.GetType() != typeof(NumberValue)))
            {
                Unit unit;
                if (lhs.GetType() == typeof(NumberWithUnitValue))
                {
                    unit = ((NumberWithUnitValue)lhs).Unit;
                }
                else
                {
                    unit = ((NumberWithUnitValue)rhs).Unit;
                }

                return new NumberWithUnitValue(
                    ((NumberValue)lhs).Value *
                    ((NumberValue)rhs).Value,
                    unit
                );
            }

            var lhsAsUnit = (NumberWithUnitValue)lhs;
            var rhsAsUnit = (NumberWithUnitValue)rhs;

            decimal rhsConverted;
            if (!TryConvertBetweenUnits(rhsAsUnit.Value, rhsAsUnit.Unit, lhsAsUnit.Unit, out rhsConverted))
            {
                var from = Math.Min(lhs.Start, rhs.Start);
                var to = Math.Max(lhs.Stop, rhs.Stop);

                Current.RecordError(ErrorType.Compiler, Position.Create(from, to, lhs.FilePath), "No conversion exists between [" + lhsAsUnit.Unit + "] and [" + rhsAsUnit.Unit + "]");
                throw new StoppedCompilingException();
            }

            return new NumberWithUnitValue(
                lhsAsUnit.Value * rhsConverted,
                lhsAsUnit.Unit
            );
        }

        public static Value operator /(Value lhs, Value rhs)
        {
            if (lhs == ExcludeFromOutputValue.Singleton || rhs == ExcludeFromOutputValue.Singleton) return ExcludeFromOutputValue.Singleton;
            if (lhs is NotFoundValue || rhs is NotFoundValue) return NotFoundValue.Default.BindToPosition(lhs.Start, rhs.Stop, lhs.FilePath);

            if (!GeneralMathTypes.Contains(lhs.GetType()) || !GeneralMathTypes.Contains(rhs.GetType()))
            {
                var from = Math.Min(lhs.Start, rhs.Start);
                var to = Math.Max(lhs.Stop, rhs.Stop);

                Current.RecordError(ErrorType.Compiler, Position.Create(from, to, lhs.FilePath), "Interpreted as division, but did not find numbers at runtime");
                return ExcludeFromOutputValue.Singleton;
            }

            if (lhs is MathValue || rhs is MathValue)
            {
                return new MathValue(lhs, Operator.Div, rhs);
            }

            if (lhs.GetType() == typeof(NumberValue) && rhs.GetType() == typeof(NumberValue))
            {
                return new NumberValue(
                    ((NumberValue)lhs).Value /
                    ((NumberValue)rhs).Value
                );
            }

            // Dividing two numbers, only one of which has a unit
            if ((lhs.GetType() == typeof(NumberValue) && rhs.GetType() != typeof(NumberValue)) ||
               (rhs.GetType() == typeof(NumberValue) && lhs.GetType() != typeof(NumberValue)))
            {
                Unit unit;
                if (lhs.GetType() == typeof(NumberWithUnitValue))
                {
                    unit = ((NumberWithUnitValue)lhs).Unit;
                }
                else
                {
                    unit = ((NumberWithUnitValue)rhs).Unit;
                }

                return new NumberWithUnitValue(
                    ((NumberValue)lhs).Value /
                    ((NumberValue)rhs).Value,
                    unit
                );
            }

            var lhsAsUnit = (NumberWithUnitValue)lhs;
            var rhsAsUnit = (NumberWithUnitValue)rhs;

            decimal rhsConverted;
            if (!TryConvertBetweenUnits(rhsAsUnit.Value, rhsAsUnit.Unit, lhsAsUnit.Unit, out rhsConverted))
            {
                var from = Math.Min(lhs.Start, rhs.Start);
                var to = Math.Max(lhs.Stop, rhs.Stop);

                Current.RecordError(ErrorType.Compiler, Position.Create(from, to, lhs.FilePath), "No conversion exists between [" + lhsAsUnit.Unit + "] and [" + rhsAsUnit.Unit + "]");
                throw new StoppedCompilingException();
            }

            return new NumberWithUnitValue(
                lhsAsUnit.Value / rhsConverted,
                lhsAsUnit.Unit
            );
        }

        public static Value operator %(Value lhs, Value rhs)
        {
            if (lhs == ExcludeFromOutputValue.Singleton || rhs == ExcludeFromOutputValue.Singleton) return ExcludeFromOutputValue.Singleton;
            if (lhs is NotFoundValue || rhs is NotFoundValue) return NotFoundValue.Default.BindToPosition(lhs.Start, rhs.Stop, lhs.FilePath);

            if (!GeneralMathTypes.Contains(lhs.GetType()) || !GeneralMathTypes.Contains(rhs.GetType()))
            {
                var from = Math.Min(lhs.Start, rhs.Start);
                var to = Math.Max(lhs.Stop, rhs.Stop);

                Current.RecordError(ErrorType.Compiler, Position.Create(from, to, lhs.FilePath), "Interpreted as mod, but did not find numbers at runtime");
                return ExcludeFromOutputValue.Singleton;
            }

            if (lhs is MathValue || rhs is MathValue)
            {
                return new MathValue(lhs, Operator.Mod, rhs);
            }

            if (lhs.GetType() == typeof(NumberValue) && rhs.GetType() == typeof(NumberValue))
            {
                var lhsInt = (int)((NumberValue)lhs).Value;
                var rhsInt = (int)((NumberValue)lhs).Value;

                return new NumberValue(
                    lhsInt % rhsInt
                );
            }

            // Multiplying two numbers, only one of which has a unit
            if ((lhs.GetType() == typeof(NumberValue) && rhs.GetType() != typeof(NumberValue)) ||
               (rhs.GetType() == typeof(NumberValue) && lhs.GetType() != typeof(NumberValue)))
            {
                Unit unit;
                if (lhs.GetType() == typeof(NumberWithUnitValue))
                {
                    unit = ((NumberWithUnitValue)lhs).Unit;
                }
                else
                {
                    unit = ((NumberWithUnitValue)rhs).Unit;
                }

                var lhsInt = (int)((NumberValue)lhs).Value;
                var rhsInt = (int)((NumberValue)lhs).Value;

                return new NumberWithUnitValue(
                    lhsInt % rhsInt,
                    unit
                );
            }

            var lhsAsUnit = (NumberWithUnitValue)lhs;
            var rhsAsUnit = (NumberWithUnitValue)rhs;

            decimal rhsConverted;
            if (!TryConvertBetweenUnits(rhsAsUnit.Value, rhsAsUnit.Unit, lhsAsUnit.Unit, out rhsConverted))
            {
                var from = Math.Min(lhs.Start, rhs.Start);
                var to = Math.Max(lhs.Stop, rhs.Stop);

                Current.RecordError(ErrorType.Compiler, Position.Create(from, to, lhs.FilePath), "No conversion exists between [" + lhsAsUnit.Unit + "] and [" + rhsAsUnit.Unit + "]");
                throw new StoppedCompilingException();
            }

            return new NumberWithUnitValue(
                ((int)lhsAsUnit.Value) % (int)rhsConverted,
                lhsAsUnit.Unit
            );
        }
    }
}
