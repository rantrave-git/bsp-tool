using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class RetryIncinclusiveAttribute : NUnitAttribute, IRepeatTest
    {
        private readonly int _tryCount;
        private readonly int _repeatCount;

        public RetryIncinclusiveAttribute(int tryCount, int repeatCount = 1)
        {
            _tryCount = tryCount;
            _repeatCount = repeatCount;
        }

        public TestCommand Wrap(TestCommand command) => new RetryCommand(command, _tryCount, _repeatCount);
        public class RetryCommand : DelegatingTestCommand
        {
            private readonly int _tryCount;
            private readonly int _repeatCount;
            public RetryCommand(TestCommand innerCommand, int tryCount, int repeatCount)
                : base(innerCommand)
            {
                _tryCount = tryCount;
                _repeatCount = repeatCount;
            }

            public override TestResult Execute(TestExecutionContext context)
            {
                int count = _tryCount;
                // var results = Enumerable.Range(0, _repeatCount).Select(_ => context.CurrentTest.MakeTestResult());
                // var result = context.CurrentTest.MakeTestResult();
                for (int i = 0; i < _repeatCount; ++i)
                {
                    // context.CurrentResult.Re
                    TestResult? result = null;
                    while (count-- > 0)
                    {
                        try
                        {
                            result = innerCommand.Execute(context);
                            // context.CurrentResult.Asse
                        }
                        catch (Exception ex)
                        {
                            if (result == null) result = context.CurrentTest.MakeTestResult();
                            result.RecordException(ex);
                        }

                        if (result.ResultState != ResultState.Inconclusive)
                            break;

                        // Clear result for retry
                        if (count > 0)
                        {
                            // result = context.CurrentTest.MakeTestResult();
                            context.CurrentRepeatCount++;
                        }
                    }
                    if (result == null)
                    {
                        context.CurrentResult = context.CurrentTest.MakeTestResult();
                        context.CurrentResult.RecordAssertion(new AssertionResult(AssertionStatus.Inconclusive, "Unlucky or wrong test", null));
                        return context.CurrentResult;
                    }
                    if (result.ResultState != ResultState.Success)
                    {
                        context.CurrentResult = context.CurrentTest.MakeTestResult();
                        return context.CurrentResult;
                    }
                    context.CurrentRepeatCount++;
                    // context.CurrentTest.Id = (string)typeof(Test).GetMethod("GetNextId", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
                }
                return context.CurrentResult;
            }
        }
    }
}