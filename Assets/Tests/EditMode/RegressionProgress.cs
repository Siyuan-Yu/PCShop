using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestRunner;

[assembly:TestRunCallback(typeof(PCShopSell.Tests.RegressionProgress))]
namespace PCShopSell.Tests
{
    /// <summary>Keep batch regressions diagnosable when a time-advancement case stalls.</summary>
    public sealed class RegressionProgress:ITestRunCallback
    {
        public void RunStarted(ITest tests)=>Debug.Log("REGRESSION START "+tests.TestCaseCount);
        public void RunFinished(ITestResult result)=>Debug.Log("REGRESSION RESULT "+result.ResultState);
        public void TestStarted(ITest test){if(!test.IsSuite)Debug.Log("REGRESSION CASE "+test.FullName);}
        public void TestFinished(ITestResult result){if(!result.Test.IsSuite)Debug.Log("REGRESSION DONE "+result.Test.FullName+" "+result.ResultState);}
    }
}
